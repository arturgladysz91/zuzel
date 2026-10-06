using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreSim.Analysis;

public sealed record PhysicalContactFixture(string Name, ContestedSpaceReport Verification,
    IReadOnlyList<PhysicalContactInput> Inputs, PhysicalContactAnalysis Analysis);
public sealed record PhysicalContactGridRow(string Population, string Geometry, double ClosingSpeed, int Technique, int Strength,
    double Condition, float RiderMassKg, double Grip, double NormalClosingSpeed, double Impulse, double Demand,
    double Reserve, double SeverityRatio, PhysicalContactSeverity Label);
public sealed record PhysicalContactPercentiles(double P10, double P25, double P50, double P75, double P90, double P95, double P99, double Max);

/// <summary>Offline frozen #55 fixtures and production comparison. No gameplay coefficient is calibrated here.</summary>
public static class PhysicalContactEvidence
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented=true,
        Converters = {new JsonStringEnumConverter()} };
    public static PhysicalContactRiderInput Physiology(int id, int technique=50, int strength=50, double condition=1,
        float mass=67.5f, double grip=.7, bool irrelevant=false) => new(id,
        new(new(irrelevant?99:50,irrelevant?1:50,technique,irrelevant?99:50,irrelevant?1:50,irrelevant?99:50,
            irrelevant?1:50,strength),new(mass),new(irrelevant?1:.5f,irrelevant?PreferredLine.Outside:PreferredLine.Neutral,irrelevant?0:.5f)),condition,grip);
    public static PhysicalPoseInterval Linear(int id, MeterPoint start, MeterPoint velocity, double duration=.15,
        double bikeHeading=0, double? endBikeHeading=null, SpeedwayBikeDimensions? dimensions=null, string frame="physical-contact-fixture",
        bool discontinuity=false)
    {
        var travel = velocity.Length == 0 ? bikeHeading : Math.Atan2(velocity.Y,velocity.X); var d = dimensions ?? SpeedwayBikeDimensions.Reference;
        return new LinearBikePoseInterval(new(id,frame,start,new(0,travel,bikeHeading),d),
            new(id,frame,start+velocity*duration,new(duration,travel,endBikeHeading??bikeHeading),d),discontinuity);
    }
    public static PhysicalContactFixture FromPoses(string name, IEnumerable<PhysicalPoseInterval> source)
    {
        var poses = source.ToArray(); var verified = ContestedSpaceResolver.Observe(poses);
        var inputs = verified.Intervals.Where(c => c.HasConflict).Select(c =>
        {
            var time = c.FirstTouchCommonTimeSeconds!.Value;
            var a = poses.Single(i => i.RiderId == c.RiderA && i.FrameId == c.FrameId && i.StartTimeSeconds<=time && i.EndTimeSeconds>=time);
            var b = poses.Single(i => i.RiderId == c.RiderB && i.FrameId == c.FrameId && i.StartTimeSeconds<=time && i.EndTimeSeconds>=time);
            return new PhysicalContactInput(c,a.Sample(time),b.Sample(time),a.RateBounds(time,time).CenterVelocityMetersPerSecond,
                b.RateBounds(time,time).CenterVelocityMetersPerSecond,Physiology(c.RiderA),Physiology(c.RiderB));
        }).ToArray();
        return new(name,verified,inputs,PhysicalContactAnalyzer.Analyze(inputs));
    }
    public static PhysicalContactFixture Side(string name, double closing=1, double forward=20, SpeedwayBikeDimensions? dimensions=null)
    {
        var d = dimensions ?? SpeedwayBikeDimensions.Reference;
        var width = Math.Max(d.ChassisBodyWidthMeters,d.HandlebarWidthMeters);
        return FromPoses(name,new[] {Linear(1,new(0,0),new(forward,closing*.5),dimensions:d),
            Linear(2,new(0,width),new(forward,-closing*.5),dimensions:d)});
    }
    public static PhysicalContactFixture Rear(string name, double closing=2) => FromPoses(name,new[]
        {Linear(1,new(0,0),new(20+closing,0)),Linear(2,new(2.10,0),new(20,0))});
    private static PhysicalContactFixture Variant(string name, PhysicalContactFixture basis, Func<PhysicalContactRiderInput,PhysicalContactRiderInput> physiology)
    {
        var inputs = basis.Inputs.Select(i => i with {RiderA=physiology(i.RiderA),RiderB=physiology(i.RiderB)}).ToArray();
        return new(name,basis.Verification,inputs,PhysicalContactAnalyzer.Analyze(inputs));
    }
    public static IReadOnlyList<PhysicalContactFixture> Fixtures()
    {
        var side = Side("B-moderate-side"); var fixtures = new List<PhysicalContactFixture>
        {
            Side("A-gentle-parallel",.05),side,Rear("C-rear-closing"),
            FromPoses("D-crossing",new[]{Linear(1,new(0,0),new(4,0),.6),Linear(2,new(1.3,1.3),new(0,-4),.6,-Math.PI/2)}),
            Side("E-center",dimensions:new(2.10,.30,.80,0,.04)),Side("E-lever"),
            Side("F-chassis",dimensions:new(2.10,.30,.15,.65,.04)),Side("F-handlebar"),
        };
        foreach(var value in new[]{20,50,80}) fixtures.Add(Variant($"G-technique-{value}",side,r => Physiology(r.RiderId,technique:value)));
        foreach(var value in new[]{20,50,80}) fixtures.Add(Variant($"H-strength-{value}",side,r => Physiology(r.RiderId,strength:value)));
        foreach(var value in new[]{1d,.7,.4}) fixtures.Add(Variant(FormattableString.Invariant($"I-condition-{value}"),side,r => Physiology(r.RiderId,condition:value)));
        foreach(var value in new[]{60f,67.5f,75f}) fixtures.Add(Variant(FormattableString.Invariant($"J-mass-{value}"),side,r => Physiology(r.RiderId,mass:r.RiderId==1?value:67.5f)));
        foreach(var value in new[]{.35,.6,.85}) fixtures.Add(Variant(FormattableString.Invariant($"K-grip-{value}"),side,r => Physiology(r.RiderId,grip:value)));
        fixtures.Add(Variant("L-irrelevant-abilities",side,r => Physiology(r.RiderId,irrelevant:true)));
        fixtures.Add(FromPoses("M-three-squeeze",new[]{Linear(1,new(0,-.8),new(20,1)),Linear(2,new(0,0),new(20,0)),Linear(3,new(0,.8),new(20,-1))}));
        fixtures.Add(FromPoses("N-four-frontier",Enumerable.Range(0,4).Select(i => Linear(i+1,new(0,.8*i),new(20,1.5-i)))));
        fixtures.Add(FromPoses("O-disjoint-groups",new[]{Linear(1,new(0,0),new(20,.5)),Linear(2,new(0,.8),new(20,-.5)),
            Linear(3,new(0,4),new(20,.5)),Linear(4,new(0,4.8),new(20,-.5))}));
        fixtures.Add(FromPoses("P-later-deferred",new[]{Linear(1,new(0,0),new(20,1)),Linear(2,new(0,.8),new(20,0)),Linear(3,new(0,1.8),new(20,-2))}));
        fixtures.Add(FromPoses("Q-boundary-ambiguous",new[]{Linear(1,new(0,0),new(20,.5),discontinuity:true),Linear(2,new(0,.7),new(20,-.5))}));
        fixtures.Add(FromPoses("R-coverage-gap",new[]{Linear(1,new(0,0),new(20,0),frame:"gap-A"),Linear(2,new(0,0),new(20,0),frame:"gap-B")}));
        fixtures.Add(FromPoses("rotation-onset",PhysicalSpaceEvidence.Controlled("I")));
        return fixtures;
    }
    public static IReadOnlyList<PhysicalContactGridRow> Grid()
    {
        var result = new List<PhysicalContactGridRow>();
        foreach (var population in new[]{"ordinary","deliberately-severe"})
        foreach (var closing in population == "ordinary" ? new[]{.05,.1,.25,.5,1,1.5} : new[]{3d,5,8})
        foreach (var geometry in new[]{"side","rear"})
        {
            // Only twelve ordinary and six severe #55 observations; physiology sweeps reuse their immutable verified contact.
            var basis = (geometry == "side" ? Side("grid",closing) : Rear("grid",closing)).Inputs.Single();
            foreach(var technique in new[]{20,50,80}) foreach(var strength in new[]{20,50,80})
            foreach(var condition in new[]{1d,.7,.4}) foreach(var mass in new[]{60f,67.5f,75f}) foreach(var grip in new[]{.35,.6,.85})
            {
                var input = basis with {RiderA=Physiology(1,technique,strength,condition,mass,grip),RiderB=Physiology(2,technique,strength,condition,mass,grip)};
                var analysis = PhysicalContactAnalyzer.Analyze(new[]{input}); var pair = analysis.AuditPairs.Single(); var rider = analysis.AuditRiders[0];
                result.Add(new(population,geometry,closing,technique,strength,condition,mass,grip,pair.Impulse!.NormalClosingSpeedMetersPerSecond,
                    pair.Impulse.ImpulseMagnitudeNewtonSeconds,rider.CombinedStabilityDemand,rider.Reserve.StabilityReserveMetersPerSecond,
                    rider.SeverityRatio,rider.ProvisionalSeverity));
            }
        }
        return result;
    }
    public static PhysicalContactPercentiles Percentiles(IEnumerable<double> source)
    {
        var sorted = source.Order().ToArray();
        double P(double q) { var at=q*(sorted.Length-1); var lo=(int)at; var hi=Math.Min(lo+1,sorted.Length-1); return sorted[lo]+(sorted[hi]-sorted[lo])*(at-lo); }
        return new(P(.10),P(.25),P(.50),P(.75),P(.90),P(.95),P(.99),sorted[^1]);
    }
    public static ResolvedSimulationStep Resolve(ContestedScenario scenario, PhysicalContactDiagnosticsLevel level, bool reverse=false)
    {
        var snapshot = ContestedSpaceResponseEvidence.Snapshot(scenario,reverse);
        var intents = scenario.Riders.Select(i => new RiderIntent(i.Id,new RiderDecision(i.Intent.TargetFor(snapshot.Segment.Type)) {Trajectory=i.Intent})).ToArray();
        return new SimulationEngine(new FrozenDecision()).Resolve(snapshot,reverse?intents.Reverse().ToArray():intents,
            new() {EnableContestedSpaceResponses=true,IncidentFrequency=scenario.IncidentFrequency,
                InteractionDiagnostics=InteractionDiagnosticsLevel.FullAudit,PhysicalContactDiagnostics=level});
    }
    public static IReadOnlyList<object> LegacyComparisons() => ContestedSpaceResponseEvidence.OwnershipScenarios()
        .Concat(ContestedSpaceResponseEvidence.Scenarios().Where(s => s.Name.StartsWith("I-",StringComparison.Ordinal)))
        .SelectMany(s => new[]{7,19,57}.Select(seed => s with {Seed=seed,IncidentFrequency=2})).Select(s =>
        {
            var step=Resolve(s,PhysicalContactDiagnosticsLevel.FullAudit);
            return (object)new {s.Name,s.Seed,s.IncidentFrequency,step.Changes,step.Events,
                Analysis=step.Interaction!.PhysicalContactAnalysis??PhysicalContactAnalysis.Empty(PhysicalContactDiagnosticsLevel.FullAudit)};
        }).ToArray();
    public static string DeterministicJson()
    {
        var fixtures=Fixtures(); var grid=Grid();
        var warnings = Warnings(fixtures,grid);
        return JsonSerializer.Serialize(new {Schema="physical-contact-analysis-v1",BaseMainSha="a25ab81b2d5c89d47ae6a4f33ba851eb47fc2be7",
            CalibrationStatus="PROVISIONAL — requires #56C1 review",ShadowOnly=true,Parameters=new PhysicalContactParameters(),
            Controlled=fixtures,Sensitivity=fixtures.Where(f=>f.Name[0] is >= 'G' and <= 'L').Select(f=>new{f.Name,f.Inputs,f.Analysis.Riders}),
            Distributions=grid.GroupBy(r => r.Population).Select(g => new {Population=g.Key,Count=g.Count(),
                NormalClosing=Percentiles(g.Select(r => r.NormalClosingSpeed)),Impulse=Percentiles(g.Select(r => r.Impulse)),
                Demand=Percentiles(g.Select(r => r.Demand)),Reserve=Percentiles(g.Select(r => r.Reserve)),Ratio=Percentiles(g.Select(r => r.SeverityRatio)),
                Histogram=Enum.GetValues<PhysicalContactSeverity>().Select(label => new {Label=label,Count=g.Count(r => r.Label==label)})}),
            RawGrid=grid,LegacyComparisons=LegacyComparisons(),Warnings=warnings},JsonOptions)+"\n";
    }
    public static IReadOnlyList<string> Warnings(IReadOnlyList<PhysicalContactFixture> fixtures, IReadOnlyList<PhysicalContactGridRow> grid)
    {
        var warnings=new List<string>();
        foreach(var f in fixtures) foreach(var pair in f.Analysis.AuditPairs.Where(p => p.Impulse is not null))
        {
            var i=pair.Impulse!;
            if ((i.ImpulseOnANewtonSeconds+i.ImpulseOnBNewtonSeconds).Length>1e-9) warnings.Add($"Unequal impulses: {f.Name}");
            if (i.PostNormalKineticEnergyJoules>i.PreNormalKineticEnergyJoules+1e-7) warnings.Add($"Energy increase: {f.Name}");
        }
        if(fixtures.Single(f=>f.Name=="A-gentle-parallel").Analysis.Riders.Any(r=>r.ProvisionalSeverity>=PhysicalContactSeverity.MajorSave))
            warnings.Add("Gentle brush classified as MajorSave/Crash");
        if(grid.Where(r=>r.Population=="deliberately-severe").Any(r=>r.Label==PhysicalContactSeverity.Brush)) warnings.Add("Severe fixture classified as Brush");
        foreach(var group in grid.GroupBy(r=>(r.Geometry,r.Technique,r.Strength,r.Condition,r.RiderMassKg,r.Grip)))
        {
            var sorted=group.OrderBy(r=>r.ClosingSpeed).ToArray();
            for(var i=1;i<sorted.Length;i++) if(sorted[i].SeverityRatio<sorted[i-1].SeverityRatio) warnings.Add("Severity decreases with greater closing");
        }
        foreach(var prefix in new[]{"G-technique-","H-strength-","I-condition-","K-grip-"})
        {
            var sorted=fixtures.Where(f=>f.Name.StartsWith(prefix,StringComparison.Ordinal))
                .OrderBy(f=>f.Analysis.Riders[0].StabilityReserve).ToArray();
            for(var i=1;i<sorted.Length;i++) if(sorted[i].Analysis.Riders[0].SeverityRatio>=sorted[i-1].Analysis.Riders[0].SeverityRatio)
                warnings.Add($"Reserve sweep is not monotonic: {prefix}");
        }
        var squeeze=fixtures.Single(f=>f.Name=="M-three-squeeze").Analysis.AuditRiders.Single(r=>r.RiderId==2);
        if(squeeze.NetImpulseNewtonSeconds.Length>1e-7||squeeze.CombinedStabilityDemand<=0) warnings.Add("Net cancellation incorrectly removes squeeze stability demand");
        if(JsonSerializer.Serialize(fixtures.Single(f=>f.Name=="B-moderate-side").Analysis,JsonOptions)
            !=JsonSerializer.Serialize(fixtures.Single(f=>f.Name=="L-irrelevant-abilities").Analysis,JsonOptions)) warnings.Add("Irrelevant abilities change physical analysis");
        foreach(var f in fixtures)
            if(JsonSerializer.Serialize(f.Analysis,JsonOptions)!=JsonSerializer.Serialize(PhysicalContactAnalyzer.Analyze(f.Inputs.Reverse()),JsonOptions))
                warnings.Add($"Contact order changes analysis: {f.Name}");
        return warnings;
    }
    private sealed class FrozenDecision : IRiderDecisionModel
    { public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane); }
}
