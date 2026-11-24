using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double RejectedLow211 = 3.4631920415553603, RejectedHigh211 = 3.463192042314308;

    private static Start197[] FailedNativeStates211(string root)
    {
        using var file = new GZipStream(File.OpenRead(Path.Combine(root, Evidence210,
            "first-freeze/native/H29-P-C.json.gz")), CompressionMode.Decompress);
        using var archive = JsonDocument.Parse(file);
        return archive.RootElement.GetProperty("Decisions").EnumerateArray().TakeLast(9)
            .Select(d => d.GetProperty("State").Deserialize<Start197>(Json)!).ToArray();
    }

    private static void AuditRoot211(string root, string output)
    {
        var source = SourceFor211(root);
        var failed = Read210(Path.Combine(root, Evidence210, "first-freeze/frozen-candidate.json"));
        var states = FailedNativeStates211(root);
        var model = RootFor210(root, "C");
        var law = Law210(source.Parent, failed);
        var fallback = Law210(source.Parent, failed, optionalRecovery: false, inheritedRecovery: false);
        var requested = law(states[^1]);
        var cursor = model.Model.BeginFeedback207(states[^1], at =>
            at.State.Time < states[^1].State.Time + .025 ? requested : fallback(at), model.Goal, .50, profile: true);
        cursor.Advance(40, states[^1].State.Time + .25);
        var failure = cursor.ContactFailure ?? throw new InvalidDataException("Missing archived forecast contact failure.");
        if (Math.Abs(failure.State.Time - 48.61) > 1e-8 || Math.Abs(failure.State.Rider.TotalReferenceMeters - 945.5082154274015) > 1e-9 ||
            !failure.Error.Contains("AmbiguousOrMultipleRoots", StringComparison.Ordinal))
            throw new InvalidDataException("The original failure did not reproduce.");

        var (_, spec) = CourseFixture208(root, "C");
        var grip = Surface.SampleSurface(0, 2);
        var settings = Settings202("H28-F");
        var bike = Physics.TwoContactBike ?? TwoContactBikeParameters.Default;
        var loads = LoadBasedTyreGrip.Budget(Mass, grip, bike, Physics.LoadBasedTyres);
        var posture = settings.PostureH27!.Prepare(Mass, spec.Profile.Gameplay.Physical.MassKg,
            settings.H26!.ComHeightMeters, failure.RiderHeightOffsetMeters, failure.Action.Drive, failure.Dt);
        double height = posture.CombinedMidpointHeightMeters;
        double lo = -LoadBasedTyreGrip.GravityMetersPerSecondSquared * bike.FrontAxleFromComMeters / height;
        double hi = LoadBasedTyreGrip.GravityMetersPerSecondSquared * bike.RearAxleFromComMeters / height;
        const double loadReserve = .001;
        var shaft = new CrankClutch144(omega: failure.State.Shaft, lossH26: settings.H26.EngineLoss);
        var wheel = Speed148.ForecastFork148.Wheel(failure.State.Wheel, Physics.RearWheel);

        HandlingContact.ContactQuery64 Query(FreeRacingAction command, bool fast) => new(
            settings with { FastCoupledCertificate = fast }, failure.State.Rider with { Drive = command.Drive }, spec,
            bike, loads, Mass, Inertia, height, failure.State.Time, failure.Dt, failure.State.Steer,
            command.SteeringRadians, failure.State.Lean, wheel, shaft, RiderController.ReactionSeconds(spec));

        var q = Query(failure.Action, true);
        var type = typeof(HandlingContact.ContactQuery64);
        var enclose = type.GetMethod("CombinedEnclosure", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<double, double, ContactResidual64<HandlingContact.Trial64>, ContactSlope64>>(q);
        var exclude = type.GetMethod("EarlyExclusion", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<ContactResidual64<HandlingContact.Trial64>, ContactResidual64<HandlingContact.Trial64>, bool>>(q);
        T Field<T>(string name) => (T)type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(q)!;
        double throttle = Field<double>("throttle");
        var front = Field<HandlingContact.Vec64>("frontVelocity");
        var forward = Field<HandlingContact.Vec64>("forward");
        var left = Field<HandlingContact.Vec64>("left");
        var arm = Field<HandlingContact.Vec64>("rearArm");
        var inputs = new
        {
            Mass, Height = height, Gravity = LoadBasedTyreGrip.GravityMetersPerSecondSquared,
            FrontAxle = Field<double>("frontAxle"), RearAxle = Field<double>("rearAxle"),
            Normal = Field<double>("normal"), TotalCapacity = Field<double>("total"),
            Grip = Field<LoadBasedTyreBudget>("original").EffectiveFrictionCoefficient,
            RearForward = Field<double>("rearForward"), RearNormalizedSide = Field<double>("rearNormalizedSide"),
            FrontU = HandlingContact.Vec64.Dot(front, forward), FrontV = HandlingContact.Vec64.Dot(front, left),
            FrontSpeed = front.Norm, FrontSlipRatio = Field<double>("frontSlipRatio"),
            RearMomentArm = HandlingContact.Vec64.Cross(arm, left),
            TargetYaw = q.DesiredYawMoment, Drag = Field<double>("dragProjection"),
            Dt = (double)failure.Dt, SteerBefore = Field<double>("steeringBefore"),
            SteerRequest = Field<double>("steeringRequest"), SteerTravel = Field<double>("steeringTravel"),
            MaxSteer = Field<double>("maxSteering"), Throttle = throttle,
            Ratio = Field<double>("ratio"), Efficiency = Field<double>("efficiency"), Released = Field<bool>("released"),
            Wheel = wheel.Parameters, WheelOmega = failure.State.Wheel, ShaftOmega = shaft.Omega,
            Clutch = shaft.Parameters, NetViscosity = shaft.LossH26!.NetViscosity(throttle), EngineKnots = Engine500.Curve
        };

        object Numbers(ContactInterval64 interval) => new { Low = double.IsFinite(interval.Low) ? interval.Low : (double?)null,
            High = double.IsFinite(interval.High) ? interval.High : (double?)null };
        object At(double ax)
        {
            var r = q.Evaluate(ax);
            return new { Ax = ax, r.Residual, r.Value.Loads, r.Value.TargetSteering, r.Value.AfterSteering,
                r.Value.MidSteering, r.Value.FrontFy, r.Value.RearFy, r.Value.Power };
        }
        var boxes = new Stack<(double Lo, double Hi)>();
        boxes.Push((lo + loadReserve, hi - loadReserve));
        var undecided = new List<object>();
        var crossings = new List<object>();
        int excluded = 0, visited = 0;
        while (boxes.TryPop(out var b) && ++visited <= 4096)
        {
            var a = q.Evaluate(b.Lo);
            var z = q.Evaluate(b.Hi);
            var mid = q.Evaluate(b.Lo + (b.Hi - b.Lo) / 2);
            var bound = enclose(b.Lo, b.Hi, mid);
            if (bound.Value.ExcludesZero || Math.Sign(a.Residual) == Math.Sign(z.Residual) &&
                a.Residual != 0 && exclude(a, z))
            {
                excluded++;
                continue;
            }
            if (bound.Slope.ExcludesZero)
            {
                if (Math.Sign(a.Residual) != Math.Sign(z.Residual) || a.Residual == 0)
                    crossings.Add(new { b.Lo, b.Hi, A = a.Residual, Z = z.Residual, Slope = Numbers(bound.Slope) });
                else excluded++;
                continue;
            }
            if (b.Hi - b.Lo <= 1e-9)
            {
                undecided.Add(new { b.Lo, b.Hi, A = a.Residual, Z = z.Residual,
                    Value = Numbers(bound.Value), Slope = Numbers(bound.Slope) });
                continue;
            }
            boxes.Push((mid.Ax, b.Hi));
            boxes.Push((b.Lo, mid.Ax));
        }
        object Certify(FreeRacingAction action, bool fast)
        {
            try
            {
                var solved = Query(action, fast).Solve(lo + loadReserve, hi - loadReserve, posture);
                return new { Action = action, Fast = fast, Status = "Converged", Info = (object?)solved.LoadH26 };
            }
            catch (ContactRootException e)
            {
                return new { Action = action, Fast = fast, Status = e.Status.ToString(), Info = (object?)e.Detail };
            }
        }
        double middle = (RejectedLow211 + RejectedHigh211) / 2;
        var atBox = enclose(RejectedLow211, RejectedHigh211, q.Evaluate(middle));
        var slivers = q.ReserveEvidence(lo + loadReserve, hi - loadReserve);
        var historical = states.Select(s => GuardFrom210(model, s, law, fallback)).ToArray();
        var perturbations = new List<object> { Certify(failure.Action, true), Certify(failure.Action, false) };
        foreach (float d in new[] { -.005f, -.0001f, -.00001f, .00001f, .0001f, .005f })
        {
            perturbations.Add(Certify(failure.Action with { Drive = failure.Action.Drive + d }, true));
            perturbations.Add(Certify(failure.Action with { SteeringRadians = failure.Action.SteeringRadians + d }, true));
        }
        var broad = Enumerable.Range(0, 1025).Select(i => At(lo + loadReserve + (hi - lo - 2 * loadReserve) * i / 1024)).ToArray();
        var local = Enumerable.Range(-128, 257).Select(i => At(middle + i * 1e-8)).ToArray();
        Save(Path.Combine(output, "root-audit.json.gz"), new { Decision = states[^1], Requested = requested, Failed = failure,
            ForecastState = cursor.Checkpoint(), cursor.StrictFailure206, Grip = grip, Spec = spec, Posture = posture,
            Inputs = inputs, Domain = new { lo, hi, loadReserve }, PublishedBox = new
            {
                Lo = At(RejectedLow211), Mid = At(middle), Hi = At(RejectedHigh211),
                Enclosure = Numbers(atBox.Value), Derivative = Numbers(atBox.Slope)
            },
            Certificate = new { Visited = visited, Excluded = excluded, Remaining = boxes.Count, Crossings = crossings,
                Undecided = undecided, Slivers = new { Rear = Numbers(slivers.RearUnloaded), Front = Numbers(slivers.FrontUnloaded) } },
            Historical = historical, Perturbations = perturbations, Broad = broad, Local = local, NativeWorlds = 0 }, true);
        Console.WriteLine($"Reproduced t={failure.State.Time:F3}; interval boxes {visited}; monotone brackets {crossings.Count}; unresolved boxes {undecided.Count}.");
    }
}
