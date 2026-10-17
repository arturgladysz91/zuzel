using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CoreSim;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using static Box2D.NET.B2Worlds;

namespace SharedWorldSpeedway;

/// <summary>One accepted world, one clock. Never runs or rewinds a segment traversal.</summary>
public sealed class WorldRunner : IDisposable
{
    public TrackFrame Frame { get; }
    public WorldParameters Parameters { get; }
    public WorldWork Work { get; } = new();
    public double TimeSeconds { get; private set; }
    private readonly RiderSpec[] specs;
    private readonly ContactAdapter physics;
    private readonly TrackStateSnapshot surface;
    private RiderView[] views;
    private readonly DecisionEvent?[] pending;
    private readonly double[] nextDecision, recoveryUntil;
    private readonly List<DecisionEvent> decisions = new();
    private readonly List<ContactEvent> contacts = new();
    private readonly Dictionary<int, double> finishTimes = new();
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly PhysicalContactParameters severityScale = new();
    private readonly WorldDiagnostics? diagnostics;
    private bool disposed;
    public WorldSnapshot Snapshot => new(TimeSeconds, Array.AsReadOnly((RiderView[])views.Clone()));
    public WorldRunner(IEnumerable<RiderSpec> riders, Track? track = null, WorldParameters? parameters = null,
        WorldDiagnostics? diagnostics = null)
    {
        this.diagnostics = diagnostics;
        Parameters = parameters ?? new();
        Frame = new(track ?? CoreSim.Analysis.MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        if (Parameters.AiIntervalSeconds != .1 || Parameters.SolverSubsteps is < 1 or > 8
            || new[] { Parameters.LateralResponseSeconds, Parameters.YawResponseSeconds, Parameters.SlideAngleRadians,
                Parameters.ContactFriction, Parameters.NearbyMaximumTravelMeters }.Any(x => !float.IsFinite(x))
            || !double.IsFinite(Parameters.PredictionSeconds) || !double.IsFinite(Parameters.RecoverySeconds)
            || Parameters.ContactFriction is < 0 or > 1 || Parameters.SlideAngleRadians is < 0 or > .8f
            || Parameters.NearbyMaximumTravelMeters <= 0 || Parameters.LateralResponseSeconds <= 0
            || Parameters.YawResponseSeconds <= 0 || Parameters.PredictionSeconds <= 0
            || Parameters.RecoverySeconds <= 0) throw new ArgumentOutOfRangeException(nameof(parameters));
        specs = riders.OrderBy(r => r.Profile.Id).ToArray();
        if (specs.Length is < 2 or > 4 || specs.Select(s => s.Profile.Id).Distinct().Count() != specs.Length)
            throw new ArgumentException("Two to four distinct riders are required.");
        if (specs.Any(s => !float.IsFinite(s.SpeedMetersPerSecond) || s.SpeedMetersPerSecond < 0
            || !float.IsFinite(s.InitialOutwardSpeed) || !float.IsFinite(s.OffsetMeters)
            || s.OffsetMeters < 0 || s.OffsetMeters > Frame.MinimumUsableWidthMeters || !float.IsFinite(s.TargetOffsetMeters)
            || s.TargetOffsetMeters < .5 || s.TargetOffsetMeters > Frame.MinimumUsableWidthMeters - .5 || !double.IsFinite(s.ReferenceMeters)))
            throw new ArgumentException("Invalid physical rider inputs.");
        surface = TrackState.CreateDefault(Frame.Track).Snapshot(); // frozen surface; no duplicate wear or RNG
        physics = new(specs, Frame, Parameters);
        pending = new DecisionEvent?[specs.Length]; nextDecision = new double[specs.Length]; recoveryUntil = new double[specs.Length];
        views = specs.Select((s, i) =>
        {
            var b = physics.Read(i); var road = Frame.Project(b.Position);
            return new RiderView(s.Profile.Id, b.Position, b.Velocity, b.Heading, b.Spin, road,
                s.ReferenceMeters, 0, RiderActivity.Racing, 1, s.TargetOffsetMeters, 0, Frame.Context(road, s.ReferenceMeters));
        }).ToArray();
        Record();
    }
    public RunResult Run(string name, double stepSeconds, double maximumSeconds, int requiredLaps = 0,
        Action<WorldSnapshot>? acceptedObserver = null)
    {
        if (TimeSeconds != 0 || stepSeconds is not (.005 or .01 or .02) || !double.IsFinite(maximumSeconds)
            || maximumSeconds <= 0 || requiredLaps < 0) throw new ArgumentException("Run once at 5/10/20 ms.");
        var aiEvery = (int)Math.Round(Parameters.AiIntervalSeconds / stepSeconds);
        var ticks = checked((int)Math.Ceiling(maximumSeconds / stepSeconds));
        for (var tick = 0; tick < ticks; tick++)
        {
            TimeSeconds = tick * stepSeconds;
            if (tick > 0 && tick % aiEvery == 0) Decide();
            var subdivisions = NearbySubdivisions(stepSeconds);
            for (var sub = 0; sub < subdivisions; sub++)
            {
                TimeSeconds = tick * stepSeconds + sub * stepSeconds / subdivisions;
                Activate();
                var before = views;
                var passiveEnergy = Energy(before);
                for (var i = 0; i < specs.Length; i++)
                {
                    var force = RiderController.Forces(views[i], specs[i], Frame, surface, Parameters,
                        TimeSeconds, physics.Masses[i], physics.Inertias[i], diagnostics?.Force);
                    physics.Force(i, force.Force, force.Torque);
                }
                var watch = Stopwatch.GetTimestamp();
                physics.Step((float)(stepSeconds / subdivisions), Parameters.SolverSubsteps);
                Work.SolverElapsedMilliseconds += Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
                Work.SolverCalls++;
                Work.SolverSubsteps += Parameters.SolverSubsteps;
                var counters = b2World_GetCounters(physics.World);
                Work.ContactCandidateSamples += counters.contactCount;
                Work.MaximumContactCandidates = Math.Max(Work.MaximumContactCandidates, counters.contactCount);
                var profile = b2World_GetProfile(physics.World);
                Work.SolverProfileMilliseconds += profile.step;
                Work.BroadPhaseProfileMilliseconds += profile.pairs;
                Work.NarrowPhaseProfileMilliseconds += profile.collide;
                Work.ConstraintProfileMilliseconds += profile.solveConstraints;
                TimeSeconds = tick * stepSeconds + (sub + 1) * stepSeconds / subdivisions;
                views = new RiderView[specs.Length];
                for (var i = 0; i < specs.Length; i++)
                {
                    var b = physics.Read(i); var road = Frame.Project(b.Position);
                    var delta = Frame.UnwrapDelta(before[i].Road.ReferenceMeters, road.ReferenceMeters);
                    var distance = System.Numerics.Vector2.Distance(before[i].Position, b.Position);
                    if (!float.IsFinite(b.Position.X) || !float.IsFinite(b.Position.Y) || !float.IsFinite(b.Velocity.Length())
                        || !float.IsFinite(b.Heading) || !float.IsFinite(b.Spin)) throw new InvalidOperationException("Non-finite solver state.");
                    if (distance > (before[i].Speed + 5) * stepSeconds / subdivisions + .1)
                        throw new InvalidOperationException("Unexpected transform discontinuity.");
                    var total = before[i].TotalReferenceMeters + delta;
                    views[i] = before[i] with { Position = b.Position, Velocity = b.Velocity, Heading = b.Heading,
                        AngularVelocity = b.Spin, Road = road, TotalReferenceMeters = total,
                        DistanceMeters = before[i].DistanceMeters + distance, Context = Frame.Context(road, total) };
                    Work.MaximumSpeed = Math.Max(Work.MaximumSpeed, views[i].Speed);
                    Work.MaximumStepDisplacementMeters = Math.Max(Work.MaximumStepDisplacementMeters, distance);
                    var width = LaneModel.UsableRacingWidthMeters(Frame.Track.Segments[road.Segment].Type, Frame.Track.Geometry);
                    var footprint = BikeFootprint.Create(new(b.Position.X, b.Position.Y), b.Heading, SpeedwayBikeDimensions.Reference);
                    var min = double.PositiveInfinity; var max = double.NegativeInfinity;
                    void Include(MechanicalCapsule part)
                    {
                        var center = new MeterPoint(b.Position.X, b.Position.Y);
                        var outward = new MeterPoint(road.Outward.X, road.Outward.Y);
                        var a = road.OffsetMeters + MeterPoint.Dot(part.Start - center, outward);
                        var z = road.OffsetMeters + MeterPoint.Dot(part.End - center, outward);
                        min = Math.Min(min, Math.Min(a, z) - part.RadiusMeters);
                        max = Math.Max(max, Math.Max(a, z) + part.RadiusMeters);
                    }
                    Include(footprint.Chassis); Include(footprint.Handlebar);
                    Work.MinimumTrackClearanceMeters = Math.Min(Work.MinimumTrackClearanceMeters, Math.Min(min + 1, width + 1 - max));
                    if (requiredLaps > 0 && views[i].Activity != RiderActivity.Crashed
                        && !finishTimes.ContainsKey(views[i].Id) && total >= requiredLaps * Frame.LapMeters)
                    {
                        var fraction = (requiredLaps * Frame.LapMeters - before[i].TotalReferenceMeters) / delta;
                        finishTimes.Add(views[i].Id, TimeSeconds - stepSeconds / subdivisions * (1 - fraction));
                        views[i] = views[i] with { Activity = RiderActivity.Finished };
                    }
                }
                watch = Stopwatch.GetTimestamp();
                physics.ReadContacts(views, Work, BeginContact, (a, b) => contacts.Add(new(views[a].Id, views[b].Id, TimeSeconds, "end", 0, 0, 0)),
                    diagnostics, before, TimeSeconds, (float)(stepSeconds / subdivisions));
                Work.ContactProcessingMilliseconds += Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
                if (specs.All(s => s.Passive)) Work.MaximumPassiveEnergyIncreaseJoules = Math.Max(
                    Work.MaximumPassiveEnergyIncreaseJoules, Energy(views) - passiveEnergy);
                Record();
                acceptedObserver?.Invoke(Snapshot);
            }
            Work.PhysicsUpdates++;
            if (requiredLaps > 0 && finishTimes.Count == specs.Length) break;
        }
        return new(name, stepSeconds, TimeSeconds, Work, Array.AsReadOnly(views), new Dictionary<int, double>(finishTimes),
            decisions.AsReadOnly(), contacts.AsReadOnly(), Convert.ToHexString(hash.GetHashAndReset()),
            requiredLaps == 0 || finishTimes.Count == specs.Length);
    }
    private void Decide()
    {
        var snapshot = Snapshot; // ALL observers see this same immutable accepted state
        for (var i = 0; i < specs.Length; i++)
        {
            Work.AiUpdates++;
            if (pending[i] is not null || TimeSeconds < nextDecision[i] || snapshot.Riders[i].Activity == RiderActivity.Finished) continue;
            var decision = TrafficDecision.Observe(snapshot, snapshot.Riders[i], specs[i], Parameters, Work, Frame.MinimumUsableWidthMeters - .5f);
            // Resume propulsion from a fresh observation, retaining the yielded/held line.
            // Returning across an occupied corridor requires a future tactical extension.
            if (decision is null && snapshot.Riders[i].Drive == 0 && snapshot.Riders[i].Activity == RiderActivity.Racing)
                decision = new(snapshot.Riders[i].Id, 0, TrafficAction.KeepIntent, TimeSeconds, TimeSeconds,
                    TimeSeconds + RiderController.ReactionSeconds(specs[i]), null, snapshot.Riders[i].TargetOffsetMeters, 1);
            if (decision is null) continue;
            pending[i] = decision; decisions.Add(decision); nextDecision[i] = TimeSeconds + 1;
        }
    }
    private void Activate()
    {
        for (var i = 0; i < specs.Length; i++)
        {
            if (views[i].Activity == RiderActivity.Disturbed && TimeSeconds >= recoveryUntil[i])
                views[i] = views[i] with { Activity = RiderActivity.Racing, Impairment = 0 };
            if (pending[i] is not { } command || TimeSeconds + 1e-12 < command.RequestedActivation) continue;
            views[i] = views[i] with { Drive = command.Drive, TargetOffsetMeters = command.TargetOffsetMeters };
            var index = decisions.IndexOf(command); decisions[index] = command with { ActualActivation = TimeSeconds };
            pending[i] = null;
        }
    }
    private int NearbySubdivisions(double step)
    {
        double travel = 0;
        for (var a = 0; a < views.Length; a++) for (var b = a + 1; b < views.Length; b++)
        {
            Work.ProximityPairChecks++;
            var relative = System.Numerics.Vector2.Distance(views[a].Velocity, views[b].Velocity)
                + (Math.Abs(views[a].AngularVelocity) + Math.Abs(views[b].AngularVelocity)) * SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
            if (System.Numerics.Vector2.Distance(views[a].Position, views[b].Position)
                <= 2 * SpeedwayBikeDimensions.Reference.BoundingRadiusMeters + relative * step + .1)
                travel = Math.Max(travel, relative * step);
        }
        var count = Math.Max(1, (int)Math.Ceiling(travel / Parameters.NearbyMaximumTravelMeters));
        if (count > 128) throw new InvalidOperationException("Motion exceeds the tested local substep budget.");
        return count;
    }
    private void BeginContact(int a, int b, double impulse, double closingSpeed)
    {
        double Apply(int i)
        {
            var v = views[i]; var reserve = PhysicalContactAnalyzer.Reserve(new(v.Id, specs[i].Profile.Gameplay, 1,
                surface.SampleSurface(v.Road.Segment, Math.Clamp((float)v.Road.OffsetMeters / 3, 0, 4)).EffectiveGrip), severityScale);
            // PROVISIONAL severity proxy: actual solver contact-point approach speed,
            // mass share and existing reserve. Not full #56C1 demand or another impulse.
            // Using first-step totalNormalImpulse here would make severity depend on
            // how much of an impact was processed in that particular microstep.
            var other = i == a ? b : a;
            var ratio = closingSpeed * physics.Masses[other] / (physics.Masses[i] + physics.Masses[other])
                / reserve.StabilityReserveMetersPerSecond;
            var severity = PhysicalContactAnalyzer.Classify(ratio, severityScale);
            diagnostics?.Consequence?.Invoke(new(TimeSeconds, v.Id, views[other].Id, closingSpeed, impulse,
                reserve.StabilityReserveMetersPerSecond, ratio, severity));
            var loss = (float)new PhysicalContactConsequenceParameters().ControlLoss(ratio, severityScale);
            if (v.Activity == RiderActivity.Crashed) return ratio;
            if (severity == PhysicalContactSeverity.Crash)
                views[i] = v with { Activity = RiderActivity.Crashed, Impairment = 1, Drive = 0 };
            else if (severity != PhysicalContactSeverity.Brush)
            {
                views[i] = v with { Activity = RiderActivity.Disturbed, Impairment = loss };
                recoveryUntil[i] = TimeSeconds + Parameters.RecoverySeconds;
            }
            return ratio;
        }
        var ratioA = Apply(a); var ratioB = Apply(b); Work.Consequences++;
        contacts.Add(new(views[a].Id, views[b].Id, TimeSeconds, "begin", impulse, ratioA, ratioB, closingSpeed));
    }
    private double Energy(RiderView[] state) => state.Select((v, i) => .5 * physics.Masses[i] * v.Velocity.LengthSquared()
        + .5 * physics.Inertias[i] * v.AngularVelocity * v.AngularVelocity).Sum();
    private void Record()
    {
        // Binary accepted states, not rounded reports. Independent of input collection order.
        Span<double> data = stackalloc double[14];
        foreach (var v in views)
        {
            data[0] = TimeSeconds; data[1] = v.Id; data[2] = v.Position.X; data[3] = v.Position.Y;
            data[4] = v.Velocity.X; data[5] = v.Velocity.Y; data[6] = v.Heading; data[7] = v.AngularVelocity;
            data[8] = v.TotalReferenceMeters; data[9] = (int)v.Activity; data[10] = v.Drive; data[11] = v.TargetOffsetMeters;
            data[12] = v.Impairment; data[13] = (int)v.Context;
            hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(data));
        }
    }
    public void Dispose()
    { if (disposed) return; physics.Dispose(); hash.Dispose(); disposed = true; }
}
