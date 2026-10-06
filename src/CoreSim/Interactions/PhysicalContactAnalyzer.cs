using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

/// <summary>Deterministic shadow analysis of frozen, verified contacts. Owns no gameplay state or randomness.</summary>
public static class PhysicalContactAnalyzer
{
    public static PhysicalContactPairAnalysis AnalyzePair(PhysicalContactInput input, PhysicalContactParameters? parameters = null)
    {
        var p = parameters ?? new(); p.Validate(); input.RiderA.Validate(); input.RiderB.Validate();
        if (input.RiderA.RiderId != input.Contact.RiderA || input.RiderB.RiderId != input.Contact.RiderB)
            throw new ArgumentException("Contact and canonical physiology must identify the same riders.");
        var row = Rejected(input, PhysicalContactStatus.IneligibleContact, "#55 contact is ineligible or ambiguous");
        if (!input.Contact.EligibleForFutureInteraction) return row;
        var manifold = PhysicalContactGeometry.AtVerifiedTouch(input, out var reason);
        if (manifold is null) return row with { Status = PhysicalContactStatus.GeometryUnresolved, Reason = reason };
        var ma = input.RiderA.Profile.Physical.MassKg + p.ReferenceBikeMassKg;
        var mb = input.RiderB.Profile.Physical.MassKg + p.ReferenceBikeMassKg;
        var reduced = ma * (mb / (ma + mb));
        var relative = input.VelocityB - input.VelocityA;
        var closing = Math.Max(0, -MeterPoint.Dot(relative, manifold.NormalAtoB));
        var referenceClosing = Math.Min(closing, p.MaximumReferenceClosingSpeedMetersPerSecond);
        var j = (1 + p.Restitution) * reduced * referenceClosing;
        var ib = manifold.NormalAtoB * j; var ia = ib * -1;
        var da = ia * (1 / ma); var db = ib * (1 / mb);
        var onset = input.Contact.FirstTouchCommonTimeSeconds!.Value - input.Contact.IntervalStartSeconds;
        var rotationAvailable = onset > GeometryNumerics.TimeToleranceSeconds;
        var rotation = rotationAvailable ? (Math.Max(0, input.Contact.Contributions.ARotationMeters)
            + Math.Max(0, input.Contact.Contributions.BRotationMeters)) / onset : 0;
        GeometryValidation.Nonnegative(rotation, nameof(rotation));
        var na = MeterPoint.Dot(input.VelocityA, manifold.NormalAtoB);
        var nb = MeterPoint.Dot(input.VelocityB, manifold.NormalAtoB);
        var postA = na - j / ma; var postB = nb + j / mb;
        var impulse = new PhysicalContactImpulse(ma, mb, reduced, relative, closing, referenceClosing,
            rotation, Math.Max(0,onset), rotationAvailable, j, ia, ib, da, db,
            .5 * ma * na * na + .5 * mb * nb * nb, .5 * ma * postA * postA + .5 * mb * postB * postB,
            ma * na + mb * nb, ma * postA + mb * postB);
        return row with { Status = PhysicalContactStatus.Analyzed, Reason = "Verified first-touch shadow reference",
            Manifold = manifold, Impulse = impulse,
            DemandA = Demand(input.PoseA!, da, manifold.LeverArmA, manifold.NormalAtoB, rotation, p),
            DemandB = Demand(input.PoseB!, db, manifold.LeverArmB, manifold.NormalAtoB, rotation, p) };
    }

    public static RiderStabilityReserve Reserve(PhysicalContactRiderInput rider, PhysicalContactParameters? parameters = null)
    {
        var p = parameters ?? new(); p.Validate(); rider.Validate();
        static double Factor(double value, double minimum, double maximum) => minimum + (maximum - minimum) * value;
        var technique = Factor((rider.Profile.Abilities.Technique - 1) / 98d,p.TechniqueFactorMinimum,p.TechniqueFactorMaximum);
        var strength = Factor((rider.Profile.Abilities.Strength - 1) / 98d,p.StrengthFactorMinimum,p.StrengthFactorMaximum);
        var condition = Factor(rider.Condition,p.ConditionFactorMinimum,p.ConditionFactorMaximum);
        var grip = Factor(rider.EffectiveGrip,p.GripFactorMinimum,p.GripFactorMaximum);
        return new(technique,strength,condition,grip,p.BaseStabilityReserveMetersPerSecond * technique * strength * condition * grip);
    }
    public static PhysicalContactSeverity Classify(double ratio, PhysicalContactParameters? parameters = null)
    {
        var p = parameters ?? new(); p.Validate(); GeometryValidation.Nonnegative(ratio,nameof(ratio));
        return ratio < p.BrushThreshold ? PhysicalContactSeverity.Brush : ratio < p.DisturbedThreshold ? PhysicalContactSeverity.Disturbed
            : ratio < p.LostRhythmThreshold ? PhysicalContactSeverity.LostRhythm : ratio < p.MajorSaveThreshold ? PhysicalContactSeverity.MajorSave
            : PhysicalContactSeverity.Crash;
    }

    public static PhysicalContactAnalysis Analyze(IEnumerable<PhysicalContactInput> source, PhysicalContactParameters? parameters = null,
        PhysicalContactDiagnosticsLevel level = PhysicalContactDiagnosticsLevel.FullAudit)
    {
        if (level == PhysicalContactDiagnosticsLevel.None) return PhysicalContactAnalysis.Empty(level);
        if (!Enum.IsDefined(level)) throw new ArgumentOutOfRangeException(nameof(level));
        return AnalyzeEnabled(source,parameters,level);
    }
    private static PhysicalContactAnalysis AnalyzeEnabled(IEnumerable<PhysicalContactInput> source, PhysicalContactParameters? parameters,
        PhysicalContactDiagnosticsLevel level)
    {
        var p = parameters ?? new(); p.Validate();
        var all = source.Select(Canonical).ToArray();
        if (all.Length == 0) return PhysicalContactAnalysis.Empty(level);
        var inputs = all.GroupBy(i => (i.Contact.RiderA,i.Contact.RiderB)).Select(g => g
            .OrderByDescending(i => i.Contact.EligibleForFutureInteraction).ThenBy(i => i.Contact.FirstTouchCommonTimeSeconds ?? double.MaxValue)
            .ThenBy(i => i.Contact.IntervalStartSeconds).ThenBy(i => i.Contact.IntervalEndSeconds)
            .ThenBy(i => i.Contact.FrameId,StringComparer.Ordinal).First()).OrderBy(i => i.Contact.RiderA).ThenBy(i => i.Contact.RiderB).ToArray();
        var eligible = inputs.Where(i => i.Contact.EligibleForFutureInteraction).ToArray();
        // Connected components of this frozen step, including later contacts. No impact-modified path or iterative solve.
        var earliest = new Dictionary<int,double>(); var groups = 0;
        foreach (var id in eligible.SelectMany(i => new[] {i.Contact.RiderA,i.Contact.RiderB}).Distinct().Order())
        {
            if (earliest.ContainsKey(id)) continue;
            var members = new HashSet<int> {id}; bool changed;
            do { changed = false; foreach (var i in eligible)
                if (members.Contains(i.Contact.RiderA) || members.Contains(i.Contact.RiderB))
                { changed |= members.Add(i.Contact.RiderA); changed |= members.Add(i.Contact.RiderB); }
            } while (changed);
            var time = eligible.Where(i => members.Contains(i.Contact.RiderA)).Min(i => i.Contact.FirstTouchCommonTimeSeconds!.Value);
            foreach (var member in members) earliest[member] = time;
            groups++;
        }
        var pairs = inputs.Select(i => i.Contact.EligibleForFutureInteraction
            && i.Contact.FirstTouchCommonTimeSeconds!.Value > earliest[i.Contact.RiderA] + p.ContactSimultaneityWindowSeconds
                ? Rejected(i,PhysicalContactStatus.DeferredByEarlierContact,"Outside this component's earliest contact frontier")
                : AnalyzePair(i,p)).ToArray();
        // All pair calculations finish before aggregation. Numeric summation order does not depend on rider identity.
        static double Sum(IEnumerable<double> values) => values.OrderBy(Math.Abs).ThenBy(v => v).Sum();
        var contributions = pairs.Zip(inputs).Where(x => x.First.Status == PhysicalContactStatus.Analyzed).SelectMany(x => new[]
        {
            (Rider:x.Second.RiderA, Mass:x.First.Impulse!.MassAKg, Impulse:x.First.Impulse.ImpulseOnANewtonSeconds, Demand:x.First.DemandA!),
            (Rider:x.Second.RiderB, Mass:x.First.Impulse!.MassBKg, Impulse:x.First.Impulse.ImpulseOnBNewtonSeconds, Demand:x.First.DemandB!)
        });
        var riders = contributions.GroupBy(c => c.Rider.RiderId).OrderBy(g => g.Key).Select(g =>
        {
            if (g.Any(c => c.Rider.Profile != g.First().Rider.Profile || c.Rider.Condition != g.First().Rider.Condition))
                throw new ArgumentException("A frozen contact group requires one canonical physiology per rider.");
            var net = new MeterPoint(Sum(g.Select(c => c.Impulse.X)),Sum(g.Select(c => c.Impulse.Y)));
            var demand = Math.Sqrt(Sum(g.Select(c => c.Demand.PairDemandSquared)));
            // A rider can cross surface cells within the frontier. Use the smallest sampled reserve; never resample a displaced path.
            var reserve = g.Select(c => Reserve(c.Rider,p)).OrderBy(r => r.StabilityReserveMetersPerSecond).First();
            var ratio = demand / reserve.StabilityReserveMetersPerSecond;
            return new RiderContactAnalysis(g.Key,g.First().Mass,net,net*(1/g.First().Mass),demand,reserve,ratio,Classify(ratio,p));
        }).ToArray();
        var summaries = pairs.Zip(inputs).Select(x =>
        {
            var a = x.First.DemandA?.PairDemand / Reserve(x.Second.RiderA,p).StabilityReserveMetersPerSecond;
            var b = x.First.DemandB?.PairDemand / Reserve(x.Second.RiderB,p).StabilityReserveMetersPerSecond;
            return new PhysicalContactPairSummary(x.First.RiderA,x.First.RiderB,x.First.EpisodeId,x.First.FirstTouchCommonTimeSeconds,
                x.First.Status,x.First.Reason,x.First.LegacyFallbackAuthorized,x.First.DemandA?.PairDemand,x.First.DemandB?.PairDemand,
                a,b,a.HasValue?Classify(a.Value,p):null,b.HasValue?Classify(b.Value,p):null,x.First.LegacyResults);
        }).ToArray();
        return new(level,summaries,riders.Select(r => new RiderContactSummary(r.RiderId,r.CombinedStabilityDemand,
            r.Reserve.StabilityReserveMetersPerSecond,r.SeverityRatio,r.ProvisionalSeverity)).ToArray(),
            level == PhysicalContactDiagnosticsLevel.FullAudit ? pairs : Array.Empty<PhysicalContactPairAnalysis>(),
            level == PhysicalContactDiagnosticsLevel.FullAudit ? riders : Array.Empty<RiderContactAnalysis>(),
            new(all.Length,inputs.Length,groups,pairs.Count(r => r.Status is PhysicalContactStatus.Analyzed or PhysicalContactStatus.GeometryUnresolved),
                pairs.Count(r => r.Status == PhysicalContactStatus.Analyzed),pairs.Count(r => r.DeferredByEarlierContact),
                pairs.Count(r => r.Status is PhysicalContactStatus.IneligibleContact or PhysicalContactStatus.GeometryUnresolved)));
    }
    private static RiderContactDemand Demand(PhysicalBikePose pose, MeterPoint delta, MeterPoint lever, MeterPoint normal,
        double rotation, PhysicalContactParameters p)
    {
        var h = pose.Attitude.TravelHeadingRadians; var forward = new MeterPoint(Math.Cos(h),Math.Sin(h));
        var df = MeterPoint.Dot(delta,forward); var dl = MeterPoint.Dot(delta,new(-forward.Y,forward.X));
        var yawLever = Math.Abs(MeterPoint.Cross(lever,normal)) / pose.Dimensions.BoundingRadiusMeters;
        var yaw = delta.Length * yawLever;
        var f = p.ForwardDisturbanceWeight * Math.Abs(df); var l = p.LateralDisturbanceWeight * Math.Abs(dl);
        var y = p.YawDisturbanceWeight * yaw; var r = p.RotationDisturbanceWeight * rotation;
        var square = f*f+l*l+y*y+r*r;
        return new(pose.RiderId,df,dl,yawLever,yaw,f,l,y,r,square,Math.Sqrt(square));
    }
    private static PhysicalContactPairAnalysis Rejected(PhysicalContactInput input, PhysicalContactStatus status, string reason)
        => new(input.Contact.RiderA,input.Contact.RiderB,input.EpisodeId,input.OriginEpisodeIds?.Distinct().Order().ToArray() ?? Array.Empty<long>(),
            input.Contact.FirstTouchCommonTimeSeconds ?? input.Contact.IntervalStartSeconds,status,reason,input.LegacyFallbackAuthorized,
            input.LegacyResults?.OrderBy(r => r.RiderId).ToArray() ?? Array.Empty<RiderLegacyContactObservation>());
    public static PhysicalContactInput Canonical(PhysicalContactInput i)
    {
        if (i.Contact.RiderA < i.Contact.RiderB) return i;
        var c = i.Contact; var v = c.Contributions;
        return i with { Contact = c with { RiderA=c.RiderB,RiderB=c.RiderA,ComponentA=c.ComponentB,ComponentB=c.ComponentA,
            SourceA=c.SourceB,SourceB=c.SourceA,RelativePositionAtOnsetMeters=c.RelativePositionAtOnsetMeters*-1,
            RelativeVelocityAtOnsetMetersPerSecond=c.RelativeVelocityAtOnsetMetersPerSecond*-1,
            Contributions=new(v.BTranslationMeters,v.BRotationMeters,v.ATranslationMeters,v.ARotationMeters,v.ActualClosingMeters,v.NonadditiveResidualMeters) },
            PoseA=i.PoseB,PoseB=i.PoseA,VelocityA=i.VelocityB,VelocityB=i.VelocityA,RiderA=i.RiderB,RiderB=i.RiderA };
    }
}
