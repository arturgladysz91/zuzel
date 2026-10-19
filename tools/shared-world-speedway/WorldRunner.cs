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
    private const double CurvatureEventResolutionSeconds = .000001;
    public TrackFrame Frame { get; }
    public WorldParameters Parameters { get; }
    public WorldWork Work { get; } = new();
    public double TimeSeconds { get; private set; }
    private readonly RiderSpec[] specs;
    private readonly ContactAdapter physics;
    private readonly TrackStateSnapshot surface;
    private RiderView[] views;
    private readonly DecisionEvent?[] pending;
    private readonly DecisionEvent?[] pendingThrottle;
    private readonly double[] nextDecision, recoveryUntil;
    private readonly TacticalMemory[] tactics;
    private readonly List<DecisionEvent> decisions = new();
    private readonly List<ContactEvent> contacts = new();
    private readonly Dictionary<int, double> finishTimes = new();
    private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private readonly PhysicalContactParameters severityScale = new();
    private readonly WorldDiagnostics? diagnostics;
    private readonly ControlCommand[] controls;
    private int nextControl;
    private bool disposed;
    public WorldSnapshot Snapshot => new(TimeSeconds, Array.AsReadOnly((RiderView[])views.Clone()));
    public WorldRunner(IEnumerable<RiderSpec> riders, Track? track = null, WorldParameters? parameters = null,
        WorldDiagnostics? diagnostics = null, IEnumerable<ControlCommand>? controls = null)
    {
        this.diagnostics = diagnostics;
        Parameters = parameters ?? new();
        if (Parameters.EnableClosureAdaptiveFollowingGap && !Parameters.EnableProportionalTrafficThrottle)
            throw new ArgumentException("Closure adaptive gap requires proportional traffic throttle.", nameof(parameters));
        if (Parameters.EnableProportionalTrafficThrottle && !Parameters.EnableTacticalRacing)
            throw new ArgumentException("Proportional traffic throttle requires experimental tactical racing.", nameof(parameters));
        if (Parameters.EnableTacticalQualityScoring && (!Parameters.EnableTacticalRacing || !Parameters.EnableSideBySideContests))
            throw new ArgumentException("Quality scoring requires tactical racing and side-by-side contests.", nameof(parameters));
        if (Parameters.EnableSideBySideContests && !Parameters.EnableTacticalRacing)
            throw new ArgumentException("Side-by-side contests require experimental tactical racing.", nameof(parameters));
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
        if (specs.Length is < 1 or > 4 || specs.Select(s => s.Profile.Id).Distinct().Count() != specs.Length)
            throw new ArgumentException("One to four distinct riders are required.");
        if (specs.Any(s => !float.IsFinite(s.SpeedMetersPerSecond) || s.SpeedMetersPerSecond < 0
            || !float.IsFinite(s.InitialOutwardSpeed) || !float.IsFinite(s.OffsetMeters)
            || s.OffsetMeters < 0 || s.OffsetMeters > Frame.MinimumUsableWidthMeters || !float.IsFinite(s.TargetOffsetMeters)
            || s.TargetOffsetMeters < .5 || s.TargetOffsetMeters > Frame.MinimumUsableWidthMeters - .5 || !double.IsFinite(s.ReferenceMeters)))
            throw new ArgumentException("Invalid physical rider inputs.");
        this.controls = (controls ?? Array.Empty<ControlCommand>()).OrderBy(c => c.AtSeconds).ThenBy(c => c.RiderId).ToArray();
        if (this.controls.Any(c => !double.IsFinite(c.AtSeconds) || c.AtSeconds < 0
                || !float.IsFinite(c.Drive) || c.Drive is < 0 or > 1 || !float.IsFinite(c.TargetOffsetMeters)
                || c.TargetOffsetMeters < .5 || c.TargetOffsetMeters > Frame.MinimumUsableWidthMeters - .5
                || !specs.Any(s => s.Profile.Id == c.RiderId))
            || this.controls.Select(c => (c.AtSeconds, c.RiderId)).Distinct().Count() != this.controls.Length
            || this.controls.Length > 0 && specs.Any(s => s.TrafficEnabled))
            throw new ArgumentException("Use finite, unique authored commands with reactive traffic disabled.", nameof(controls));
        surface = TrackState.CreateDefault(Frame.Track).Snapshot(); // frozen surface; no duplicate wear or RNG
        physics = new(specs, Frame, Parameters);
        pending = new DecisionEvent?[specs.Length];
        pendingThrottle = Parameters.EnableProportionalTrafficThrottle ? new DecisionEvent?[specs.Length] : Array.Empty<DecisionEvent?>();
        nextDecision = new double[specs.Length]; recoveryUntil = new double[specs.Length];
        tactics = Parameters.EnableTacticalRacing ? specs.Select(s => new TacticalMemory { HomeOffsetMeters = s.TargetOffsetMeters }).ToArray()
            : Array.Empty<TacticalMemory>();
        views = specs.Select((s, i) =>
        {
            var b = physics.Read(i); var road = Frame.Project(b.Position);
            return new RiderView(s.Profile.Id, b.Position, b.Velocity, b.Heading, b.Spin, road,
                s.ReferenceMeters, 0, RiderActivity.Racing, 1, s.TargetOffsetMeters, 0, Frame.Context(road, s.ReferenceMeters));
        }).ToArray();
        Record();
    }
    public RunResult Run(string name, double stepSeconds, double maximumSeconds, int requiredLaps = 0,
        Action<WorldSnapshot>? acceptedObserver = null, bool stopOnCrash = false, bool lapsFromInitialPosition = false)
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
            if (subdivisions > 1) { Work.ProximityTicks++; Work.ProximitySeconds += stepSeconds; }
            Work.ProximityRequestedExtraCalls += subdivisions - 1;
            var end = (tick + 1) * stepSeconds;
            var nextTime = TimeSeconds;
            while (nextTime < end - 1e-12)
            {
                TimeSeconds = nextTime;
                Activate();
                var nominalDuration = Math.Min(stepSeconds / subdivisions, end - TimeSeconds);
                var duration = UntilControlDeadline(nominalDuration);
                var controlDuration = duration;
                var before = views;
                var passiveEnergy = Energy(before);
                for (var i = 0; i < specs.Length; i++)
                {
                    var force = RiderController.Forces(views[i], specs[i], Frame, surface, Parameters,
                        TimeSeconds, physics.Masses[i], physics.Inertias[i], diagnostics?.Force);
                    physics.Force(i, force.Force, force.Torque);
                    duration = UntilCurvatureChange(i, force.Force, duration);
                }
                if (duration < controlDuration) Work.CurvatureLimitedCalls++;
                else if (duration < nominalDuration) {
                    Work.ControlDeadlineLimitedCalls++;
                    if (pendingThrottle.Any(c => c is not null && Math.Abs(c.RequestedActivation - (TimeSeconds + duration)) < 1e-10))
                        Work.ThrottleDeadlineLimitedCalls++;
                }
                var watch = Stopwatch.GetTimestamp();
                physics.Step((float)duration, Parameters.SolverSubsteps);
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
                TimeSeconds = nextTime += duration;
                views = new RiderView[specs.Length];
                for (var i = 0; i < specs.Length; i++)
                {
                    var b = physics.Read(i); var road = Frame.Project(b.Position);
                    var delta = Frame.UnwrapDelta(before[i].Road.ReferenceMeters, road.ReferenceMeters);
                    var distance = System.Numerics.Vector2.Distance(before[i].Position, b.Position);
                    if (!float.IsFinite(b.Position.X) || !float.IsFinite(b.Position.Y) || !float.IsFinite(b.Velocity.Length())
                        || !float.IsFinite(b.Heading) || !float.IsFinite(b.Spin)) throw new InvalidOperationException("Non-finite solver state.");
                    if (distance > (before[i].Speed + 5) * duration + .1)
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
                    var finishMeters = requiredLaps * Frame.LapMeters + (lapsFromInitialPosition ? specs[i].ReferenceMeters : 0);
                    if (requiredLaps > 0 && views[i].Activity != RiderActivity.Crashed
                        && !finishTimes.ContainsKey(views[i].Id) && total >= finishMeters)
                    {
                        var fraction = (finishMeters - before[i].TotalReferenceMeters) / delta;
                        finishTimes.Add(views[i].Id, TimeSeconds - duration * (1 - fraction));
                        views[i] = views[i] with { Activity = RiderActivity.Finished };
                    }
                }
                watch = Stopwatch.GetTimestamp();
                physics.ReadContacts(views, Work, BeginContact, (a, b) => contacts.Add(new(views[a].Id, views[b].Id, TimeSeconds, "end", 0, 0, 0)),
                    diagnostics, before, TimeSeconds, (float)duration);
                Work.ContactProcessingMilliseconds += Stopwatch.GetElapsedTime(watch).TotalMilliseconds;
                if (specs.All(s => s.Passive)) Work.MaximumPassiveEnergyIncreaseJoules = Math.Max(
                    Work.MaximumPassiveEnergyIncreaseJoules, Energy(views) - passiveEnergy);
                Record();
                acceptedObserver?.Invoke(Snapshot);
                if (stopOnCrash && views.Any(v => v.Activity == RiderActivity.Crashed)) break;
            }
            Work.PhysicsUpdates++;
            if (stopOnCrash && views.Any(v => v.Activity == RiderActivity.Crashed)) break;
            if (requiredLaps > 0 && finishTimes.Count == specs.Length) break;
        }
        return new(name, stepSeconds, TimeSeconds, Work, Array.AsReadOnly(views), new Dictionary<int, double>(finishTimes),
            decisions.AsReadOnly(), contacts.AsReadOnly(), Convert.ToHexString(hash.GetHashAndReset()),
            requiredLaps == 0 || finishTimes.Count == specs.Length);
    }
    private void Decide()
    {
        var snapshot = Snapshot; // ALL observers see this same immutable accepted state
        if (Parameters.EnableTacticalRacing) {
            var started = Stopwatch.GetTimestamp(); DecideTactics(snapshot);
            Work.TacticalElapsedMilliseconds += Stopwatch.GetElapsedTime(started).TotalMilliseconds; return;
        }
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
    private void DecideTactics(WorldSnapshot snapshot)
    {
        var pairs = TrafficDecision.ObservePairs(snapshot, Work);
        for (var i = 0; i < specs.Length; i++)
        {
            Work.AiUpdates++;
            if (Parameters.EnableProportionalTrafficThrottle)
            {
                var plan = TrafficDecision.ObserveThrottle(snapshot, snapshot.Riders[i], specs[i], Parameters, Work,
                    tactics[i], pairs, Frame, surface, pending[i], diagnostics?.Contest, diagnostics?.Throttle);
                void Cancel(ref DecisionEvent? slot) {
                    if (slot is not { } old) return;
                    decisions[decisions.IndexOf(old)] = old with {CancelledAt = TimeSeconds};
                    slot = null; Work.CancelledCommands++;
                }
                if (plan.CancelLine) Cancel(ref pending[i]);
                if (plan.Emergency && pendingThrottle[i] is {SafetyReaction:false}) Cancel(ref pendingThrottle[i]);
                if (plan.Line is { } line) {
                    if (pending[i] is { } old && line.SafetyReaction && !old.SafetyReaction) Cancel(ref pending[i]);
                    if (pending[i] is null) { pending[i] = line; decisions.Add(line); Work.TacticalDecisions++; }
                }
                if (plan.Throttle is { } throttle) {
                    if (pendingThrottle[i] is { } old && throttle.SafetyReaction && !old.SafetyReaction) Cancel(ref pendingThrottle[i]);
                    // Ordinary corrections are latched until activation. New observations
                    // never move their deadline; only a fresh emergency replaces one.
                    if (pendingThrottle[i] is null) { pendingThrottle[i] = throttle; decisions.Add(throttle); Work.TacticalDecisions++; }
                }
                continue;
            }
            var decision = TrafficDecision.Observe(snapshot, snapshot.Riders[i], specs[i], Parameters, Work,
                tactics[i], pairs, Frame, surface, pending[i] is not null, diagnostics?.Contest);
            if (decision is null) continue;
            if (pending[i] is { } waiting)
            {
                // Only a newly observed immediate threat may cancel an unexecuted
                // tactical command. The replacement starts its OWN reaction delay.
                if (!decision.SafetyReaction || waiting.SafetyReaction) continue;
                decisions[decisions.IndexOf(waiting)] = waiting with { CancelledAt = TimeSeconds };
                pending[i] = null; Work.CancelledCommands++;
            }
            pending[i] = decision; decisions.Add(decision); Work.TacticalDecisions++;
        }
    }
    private void Activate()
    {
        while (nextControl < controls.Length && controls[nextControl].AtSeconds <= TimeSeconds + 1e-12)
        {
            var c = controls[nextControl++]; var i = Array.FindIndex(specs, s => s.Profile.Id == c.RiderId);
            views[i] = views[i] with { Drive = c.Drive, TargetOffsetMeters = c.TargetOffsetMeters };
            decisions.Add(new(c.RiderId, 0, TrafficAction.KeepIntent, 0, 0, c.AtSeconds, TimeSeconds, c.TargetOffsetMeters, c.Drive));
        }
        for (var i = 0; i < specs.Length; i++)
        {
            if (views[i].Activity == RiderActivity.Disturbed && TimeSeconds + 1e-12 >= recoveryUntil[i])
                views[i] = views[i] with { Activity = RiderActivity.Racing, Impairment = 0 };
            if (Parameters.EnableProportionalTrafficThrottle)
            {
                if (views[i].Activity is RiderActivity.Crashed or RiderActivity.Finished) {
                    void CancelTerminal(DecisionEvent? slot) { if (slot is not null) {
                        decisions[decisions.IndexOf(slot)] = slot with {CancelledAt = TimeSeconds}; Work.CancelledCommands++;
                    } }
                    CancelTerminal(pending[i]); CancelTerminal(pendingThrottle[i]);
                    pending[i] = null; pendingThrottle[i] = null; continue;
                }
                if (pending[i] is { } lateral && TimeSeconds + 1e-12 >= lateral.RequestedActivation) {
                    var memory = tactics[i];
                    if (views[i].TargetOffsetMeters != lateral.TargetOffsetMeters) Work.ControlChanges++;
                    if (memory.Intent != lateral.Intent) memory.ActivatedAt = TimeSeconds;
                    memory.Intent = lateral.Intent; if (lateral.OpponentId != 0) memory.OpponentId = lateral.OpponentId;
                    memory.NextTacticAt = TimeSeconds + TrafficDecision.IntentMinimumSeconds;
                    views[i] = views[i] with {TargetOffsetMeters = lateral.TargetOffsetMeters};
                    decisions[decisions.IndexOf(lateral)] = lateral with {ActualActivation = TimeSeconds}; pending[i] = null;
                }
                if (pendingThrottle[i] is { } throttle && TimeSeconds + 1e-12 >= throttle.RequestedActivation) {
                    Work.ActivatedThrottleCommands++;
                    if (views[i].Drive != throttle.Drive) Work.ControlChanges++;
                    views[i] = views[i] with {Drive = throttle.Drive};
                    decisions[decisions.IndexOf(throttle)] = throttle with {ActualActivation = TimeSeconds}; pendingThrottle[i] = null;
                }
                continue;
            }
            if (pending[i] is not { } command || TimeSeconds + 1e-12 < command.RequestedActivation) continue;
            if (Parameters.EnableTacticalRacing)
            {
                if (views[i].Drive != command.Drive || views[i].TargetOffsetMeters != command.TargetOffsetMeters) Work.ControlChanges++;
                var memory = tactics[i];
                if (memory.Intent != command.Intent) memory.ActivatedAt = TimeSeconds;
                memory.Intent = command.Intent;
                if (command.OpponentId != 0) memory.OpponentId = command.OpponentId;
                memory.NextTacticAt = TimeSeconds + TrafficDecision.IntentMinimumSeconds;
                if (command.Drive == 0) memory.SafetyUntil = TimeSeconds + TrafficDecision.IntentMinimumSeconds;
            }
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
    private double UntilControlDeadline(double duration)
    {
        void Before(double deadline)
        { if (deadline > TimeSeconds + 1e-12) duration = Math.Min(duration, deadline - TimeSeconds); }
        if (nextControl < controls.Length) Before(controls[nextControl].AtSeconds);
        for (var i = 0; i < specs.Length; i++)
        {
            if (specs[i].SpeedMetersPerSecond == 0) Before(RiderController.TapeReactionSeconds(specs[i]));
            // A logical Hold that leaves both actuators unchanged does not
            // justify repartitioning Box2D time. Acknowledge it at the first
            // existing accepted boundary AFTER its complete reaction delay.
            if (pending[i] is { } command && (Parameters.EnableProportionalTrafficThrottle
                ? command.TargetOffsetMeters != views[i].TargetOffsetMeters
                : !Parameters.EnableSideBySideContests || command.Drive != views[i].Drive || command.TargetOffsetMeters != views[i].TargetOffsetMeters))
                Before(command.RequestedActivation);
            if (Parameters.EnableProportionalTrafficThrottle && pendingThrottle[i] is { } throttle && throttle.Drive != views[i].Drive)
                Before(throttle.RequestedActivation);
            if (views[i].Activity == RiderActivity.Disturbed) Before(recoveryUntil[i]);
        }
        return duration;
    }
    private double UntilCurvatureChange(int i, System.Numerics.Vector2 force, double duration)
    {
        // Estimate only the next force discontinuity from the current accepted
        // state and held force. No body is predicted/committed or replayed.
        var beta = (Parameters.SolverSubsteps + 1d) / (2 * Parameters.SolverSubsteps);
        if (specs[i].Passive || views[i].Activity == RiderActivity.Crashed) return duration;
        var tangent = views[i].Road.Tangent;
        var v = System.Numerics.Vector2.Dot(views[i].Velocity, tangent);
        if (v <= 0) return duration;
        var acceleration = System.Numerics.Vector2.Dot(force, tangent) / physics.Masses[i];
        var distance = Frame.DistanceToCurvatureChange(views[i].Road);
        if (distance > v * duration + beta * acceleration * duration * duration) return duration;
        var discriminant = v * v + 4 * beta * acceleration * distance;
        if (discriminant < 0) return duration;
        var time = 2 * distance / (v + Math.Sqrt(discriminant));
        // One microsecond resolution avoids chasing float coordinate ulps.
        // Rounding upward crosses the boundary before observing its new force.
        time = Math.Max(CurvatureEventResolutionSeconds,
            Math.Ceiling(time / CurvatureEventResolutionSeconds) * CurvatureEventResolutionSeconds);
        return Math.Min(duration, time);
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
