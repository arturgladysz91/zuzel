using CoreSim;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;

namespace TimeStepPhysics;

// All new controller / tyre / inertia assumptions are PROVISIONAL, not calibration.
internal sealed record PocParameters(double LaneResponseSeconds = .8, double SlipResponseSeconds = .25,
    double YawResponseSeconds = .2, double MaximumYawAcceleration = 4,
    double ControlPeriodSeconds = .1, double RecoverySeconds = .5, int MaximumLocalHalvings = 6, bool VisualResponse = true,
    double VisionRangeMeters = 8, double ThreatLookAheadSeconds = 1, double ThreatDistanceMeters = 2.2,
    double AvoidanceOffsetMeters = 1, int ConstraintIterations = 4)
{
    internal void Validate()
    {
        foreach (var value in new[] { LaneResponseSeconds, SlipResponseSeconds, YawResponseSeconds,
            MaximumYawAcceleration, ControlPeriodSeconds, RecoverySeconds, VisionRangeMeters,
            ThreatLookAheadSeconds, ThreatDistanceMeters, AvoidanceOffsetMeters })
            if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (MaximumLocalHalvings is < 0 or > 8 || ConstraintIterations is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(ConstraintIterations));
    }
}
internal sealed record DriverControl(double TargetOffset, double Throttle = 1, double SteeringRate = 0,
    double? PendingOffset = null, double ReactAt = double.PositiveInfinity);
internal sealed record DynamicBike(RiderSnapshot Rider, MeterPoint Position, MeterPoint Velocity,
    double Heading, double AngularVelocity, DriverControl Control, double ControlLoss = 0,
    double RecoveryUntil = 0, RiderRaceStatus Status = RiderRaceStatus.Racing, double Distance = 0)
{
    public double Speed => Velocity.Length;
    public double LongitudinalSpeed => MeterPoint.Dot(Velocity, TimeStepRace.Direction(Heading));
    public double LateralSpeed => MeterPoint.Cross(TimeStepRace.Direction(Heading), Velocity);
}
internal sealed record PocContact(double Time, int A, int B, string Kind, double Impulse,
    double EnergyBefore, double EnergyAfter, PhysicalContactSeverity SeverityA, PhysicalContactSeverity SeverityB, double ReleaseWitnessMeters);
internal sealed class PocWork
{
    public int MovementUpdates, PhysicsEvaluations, GeometryCalls, NarrowTests, BroadRejects,
        Decisions, LocalRetries, SupportImpulses;
    public double MaximumGripFraction;
    public double MaximumContactEnergyGain;
}
internal sealed record RoadSample(int Segment, double Progress, double Offset, double Tangent, double Radius, double Width);

/// <summary>Isolated 2D experiment. One clock, four values, six contact flags. No production callers.</summary>
internal sealed class TimeStepRace
{
    private readonly Track _track;
    private readonly TrackStateSnapshot _surface;
    private readonly TrackMetricEmbedding _map;
    private readonly PocParameters _p;
    private readonly PhysicalContactParameters _contact = new();
    private readonly bool[,] _active = new bool[4, 4];
    private readonly double[,] _released = new double[4, 4];
    private readonly bool _reverse;
    private int _controlTick;
    public double Time { get; private set; }
    public DynamicBike[] Bikes { get; private set; }
    public List<PocContact> Contacts { get; } = new();
    public PocWork Work { get; } = new();
    public string? StopReason { get; private set; }
    public string? LastRejectedWindow { get; private set; }
    public double MaximumPenetration { get; private set; }
    public double MaximumContactRefinementSeconds { get; private set; }
    internal TrackMetricEmbedding Map => _map;

    internal TimeStepRace(Track track, TrackStateSnapshot surface, IEnumerable<DynamicBike> bikes,
        double time = 0, bool reverse = false, PocParameters? parameters = null, (int, int)? consumed = null)
    {
        _track = track; _surface = surface; _map = new(track, true); _p = parameters ?? new(); _reverse = reverse;
        _p.Validate();
        Bikes = bikes.OrderBy(b => b.Rider.RiderId).ToArray(); Time = time;
        if (Bikes.Length != 4 || Bikes.Select(b => b.Rider.RiderId).Distinct().Count() != 4
            || !double.IsFinite(time) || time < 0 || Bikes.Any(b => !double.IsFinite(b.Heading + b.AngularVelocity)
                || !double.IsFinite(b.Control.TargetOffset + b.Control.Throttle + b.Control.SteeringRate + b.RecoveryUntil)
                || b.Control.Throttle is < 0 or > 1 || !double.IsFinite(b.ControlLoss) || b.ControlLoss is < 0 or > 1)
            || surface.SegmentCount != track.Segments.Count) throw new ArgumentException("Four finite, distinct actual bike states required.");
        if (consumed is { } pair)
        {
            var a = Array.FindIndex(Bikes, b => b.Rider.RiderId == pair.Item1); var b = Array.FindIndex(Bikes, b => b.Rider.RiderId == pair.Item2);
            if (a < 0 || b < 0 || a == b) throw new ArgumentException("Consumed contact must name two present riders.");
            _active[Math.Min(a, b), Math.Max(a, b)] = true;
        }
        if (!CheckEndpoints(Bikes)) throw new ArgumentException("Initial footprints penetrate beyond #55 tolerance.");
    }

    public void Run(double duration, double stepSeconds = .01)
    {
        if (!double.IsFinite(duration) || duration < 0 || stepSeconds is < .005 or > .02 || !double.IsFinite(stepSeconds))
            throw new ArgumentOutOfRangeException(nameof(duration));
        var end = Time + duration;
        while (Time < end && StopReason is null)
        {
            // Close representational clock residue before scheduling the next
            // 100 ms command. It must not create an extra end-of-run decision.
            if (end - Time <= 1e-12) { Time = end; break; }
            var controlAt = _controlTick * _p.ControlPeriodSeconds;
            // Control schedule is relative to the run origin, not rider clocks.
            if (_controlTick == 0 || Time + 1e-12 >= _controlOrigin + controlAt)
            { if (_controlTick == 0) _controlOrigin = Time; UpdateControls(); _controlTick++; }
            var h = Math.Min(stepSeconds, Math.Min(end - Time, _controlOrigin + _controlTick * _p.ControlPeriodSeconds - Time));
            if (h <= 1e-12) { Time = Math.Min(end, _controlOrigin + _controlTick * _p.ControlPeriodSeconds); continue; }
            var advanced = false;
            for (var retry = 0; retry <= _p.MaximumLocalHalvings; retry++, h *= .5)
            {
                if (Slice(h)) { advanced = true; break; }
                Work.LocalRetries++;
            }
            if (!advanced) StopReason = "Unresolved continuous contact: no admissible motion within bounded local subdivision.";
        }
    }
    private double _controlOrigin;
    private IEnumerable<int> Order => _reverse ? Enumerable.Range(0, 4).Reverse() : Enumerable.Range(0, 4);
    internal static MeterPoint Direction(double angle) => new(Math.Cos(angle), Math.Sin(angle));
    private static MeterPoint Left(MeterPoint v) => new(-v.Y, v.X);
    private double Mass(DynamicBike b) => b.Rider.Profile.Gameplay.Physical.MassKg + _contact.ReferenceBikeMassKg;
    private double Inertia(DynamicBike b) => Mass(b) * (Math.Pow(SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters, 2)
        + Math.Pow(SpeedwayBikeDimensions.Reference.HandlebarWidthMeters, 2)) / 12;
    private PhysicalBikePose Pose(DynamicBike b, double time) => new(b.Rider.RiderId, _map.FrameId, b.Position,
        new(time, b.Speed > 0 ? Math.Atan2(b.Velocity.Y, b.Velocity.X) : b.Heading, b.Heading), SpeedwayBikeDimensions.Reference)
        { DeterministicArithmetic = true };

    internal RoadSample Road(MeterPoint position)
    {
        RoadSample? best = null; var minimum = double.PositiveInfinity;
        foreach (var s in _map.Segments)
        {
            var tangent = Direction(s.StartTangentHeadingRadians); var outward = Left(tangent) * -1;
            double progress, offset;
            if (s.SegmentType == SegmentType.Straight)
            { progress = MeterPoint.Dot(position - s.StartReferencePosition, tangent) / s.StraightLengthMeters;
                offset = MeterPoint.Dot(position - s.StartReferencePosition, outward); }
            else
            {
                var center = s.StartReferencePosition - outward * s.InnerRadiusMeters; var radial = position - center;
                var angle = BikeAngles.Wrap(Math.Atan2(radial.Y, radial.X) - Math.Atan2(outward.Y, outward.X));
                progress = angle / s.TurnAngleRadians; offset = radial.Length - s.InnerRadiusMeters;
            }
            var width = LaneModel.UsableRacingWidthMeters(s.SegmentType, _track.Geometry);
            var foot = s.Map(Math.Clamp(progress, 0, 1), Math.Clamp(offset, 0, width));
            var error = (position - foot.Position).Length;
            if (error < minimum)
            { minimum = error; best = new(s.SegmentIndex, Math.Clamp(progress, 0, 1), offset, foot.TangentHeadingRadians,
                s.SegmentType == SegmentType.Straight ? double.PositiveInfinity : Math.Max(1, s.InnerRadiusMeters + offset), width); }
        }
        return best!; // Projection reads geometry; it never writes the physical position.
    }
    private TrackSurfaceState Surface(RoadSample r) => _surface.SampleSurface(r.Segment, (float)Math.Clamp(r.Offset / r.Width * 4, 0, 4));
    private double Grip(DynamicBike b, RoadSample road)
    {
        var radius = double.IsFinite(road.Radius) ? road.Radius : _track.Geometry.InnerRadiusMeters;
        var safe = SegmentPhysics.MaxSafeTurnSpeedForRadius((float)radius, Surface(road), b.Rider.Profile.Skills, b.Rider.ActiveSetup);
        return safe * (double)safe / radius;
    }
    private double Beta(RoadSample r)
    {
        var corner = _track.CornerTopology.CornerForSegment(r.Segment);
        return corner is null ? 0 : ReferenceBikeAttitude.Neutral.RelativeSlideAngle(
            Math.Clamp((r.Segment - corner.StartSegmentIndex + r.Progress) / corner.SegmentCount, 0, 1));
    }
    private void UpdateControls()
    {
        var next = (DynamicBike[])Bikes.Clone();
        foreach (var i in Order)
        {
            var b = Bikes[i]; var road = Road(b.Position); var control = b.Control;
            if (control.PendingOffset is { } pending && Time >= control.ReactAt)
                control = control with { TargetOffset = pending, PendingOffset = null, ReactAt = double.PositiveInfinity };
            if (_p.VisualResponse && b.Status == RiderRaceStatus.Racing && control.PendingOffset is null)
            {
                foreach (var other in Bikes.Where(o => o.Rider.RiderId != b.Rider.RiderId).OrderBy(o => o.Rider.RiderId))
                {
                    var d = other.Position - b.Position; var v = other.Velocity - b.Velocity;
                    var closestTime = v.Length == 0 ? 0 : Math.Clamp(-MeterPoint.Dot(d, v) / (v.Length * v.Length), 0, _p.ThreatLookAheadSeconds);
                    if (d.Length > _p.VisionRangeMeters || (d + v * closestTime).Length > _p.ThreatDistanceMeters) continue;
                    var side = MeterPoint.Dot(d, Left(Direction(road.Tangent)) * -1);
                    var target = Math.Clamp(road.Offset + (side >= 0 ? -_p.AvoidanceOffsetMeters : _p.AvoidanceOffsetMeters), .1, road.Width - .1);
                    control = control with { PendingOffset = target, ReactAt = Time + LongitudinalDynamics.CalculateStandingStartReactionTimeSeconds(b.Rider.Profile.Skills) };
                    break; // PROVISIONAL visual reaction proxy, no instantaneous avoidance.
                }
            }
            var targetLateralVelocity = (control.TargetOffset - road.Offset) / _p.LaneResponseSeconds;
            var desiredHeading = road.Tangent - Math.Atan2(targetLateralVelocity, Math.Max(1, b.Speed)) + Beta(road);
            var feedforward = double.IsFinite(road.Radius) ? b.Speed / road.Radius : 0;
            var rate = feedforward + BikeAngles.Wrap(desiredHeading - b.Heading) / _p.YawResponseSeconds;
            var limit = Grip(b, road) / Math.Max(1, b.Speed);
            next[i] = b with { Control = control with { SteeringRate = Math.Clamp(rate, -limit, limit) } };
            Work.Decisions++;
        }
        Bikes = next;
    }

    private readonly record struct Force(MeterPoint Acceleration, double AngularAcceleration);
    private Force[] Forces()
    {
        var result = new Force[4];
        foreach (var i in Order)
        {
            var b = Bikes[i]; var road = Road(b.Position); var surface = Surface(road);
            var mass = Mass(b); var grip = Grip(b, road); var speed = b.Speed;
            var direction = speed > 0 ? b.Velocity * (1 / speed) : Direction(b.Heading - Beta(road));
            var loss = Time < b.RecoveryUntil ? b.ControlLoss : 0;
            var force = _track.Segments[road.Segment].Type == SegmentType.Straight
                ? LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(b.Rider.Profile.Skills, b.Rider.ActiveSetup, surface)
                : LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(b.Rider.Profile.Skills, b.Rider.ActiveSetup, surface);
            var drive = force * b.Control.Throttle * (1 - loss) / mass;
            var desiredDirection = b.Heading - Beta(road);
            var lateral = speed * Math.Sin(BikeAngles.Wrap(desiredDirection - Math.Atan2(direction.Y, direction.X))) / _p.SlipResponseSeconds;
            var alpha = Math.Clamp((b.Control.SteeringRate - b.AngularVelocity) / _p.YawResponseSeconds,
                -_p.MaximumYawAcceleration * (1 - loss), _p.MaximumYawAcceleration * (1 - loss));
            if (b.Status != RiderRaceStatus.Racing) { drive = -grip; lateral = 0; alpha = -b.AngularVelocity / _p.YawResponseSeconds; }
            else if (double.IsFinite(road.Radius))
            {
                var safe = Math.Sqrt(grip * road.Radius);
                if (speed > safe) drive = -Math.Min(grip, (speed - safe) / _p.LaneResponseSeconds);
            }
            var demand = Math.Sqrt(drive * drive + lateral * lateral); var scale = demand > grip ? grip / demand : 1;
            drive *= scale; lateral *= scale;
            Work.MaximumGripFraction = Math.Max(Work.MaximumGripFraction, Math.Sqrt(drive * drive + lateral * lateral) / grip);
            var drag = LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons
                + LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient * speed * speed;
            // Resistive force is not multiplied by recovery or the tyre allocation.
            var longitudinal = drive - drag / mass;
            if (b.Status != RiderRaceStatus.Racing) longitudinal = Math.Max(longitudinal, -speed / .02);
            result[i] = new(direction * longitudinal + Left(direction) * lateral, alpha); Work.PhysicsEvaluations++;
        }
        return result;
    }

    private bool Slice(double h)
    {
        var start = (DynamicBike[])Bikes.Clone(); var active = (bool[,])_active.Clone();
        var released = (double[,])_released.Clone(); var count = Contacts.Count;
        var time = Time; var remaining = h;
        for (var events = 0; events < 32 && remaining > 1e-12; events++)
        {
            // Persistent unilateral contact may carry normal reaction. It never
            // reapplies #56 severity, recovery, RNG or the imported first impact.
            // A bounded four-pass normal solve handles coupled active pairs;
            // correcting one velocity can otherwise close another contact.
            for (var pass = 0; pass < _p.ConstraintIterations; pass++)
            for (var a = 0; a < 4; a++) for (var b = a + 1; b < 4; b++) if (_active[a, b])
            {
                var gap = MechanicalSeparation.Between(Pose(Bikes[a], Time), Pose(Bikes[b], Time)).SignedMeters;
                if (gap > GeometryNumerics.MinimumSeparationToleranceMeters)
                { _active[a, b] = false; _released[a, b] = gap; continue; }
                if (gap > GeometryNumerics.ContactDistanceMeters) continue; // ownership is not a force across a gap
                var manifold = Manifold(a, b);
                if (manifold is not null) Impulse(a, b, manifold, support: true);
            }
            var forces = Forces();
            for (var pass = 0; pass < _p.ConstraintIterations; pass++)
            for (var a = 0; a < 4; a++) for (var b = a + 1; b < 4; b++) if (_active[a, b])
            {
                if (MechanicalSeparation.Between(Pose(Bikes[a], Time), Pose(Bikes[b], Time)).SignedMeters
                    > GeometryNumerics.ContactDistanceMeters) continue;
                var manifold = Manifold(a, b);
                if (manifold is not null)
                {
                    var n = manifold.NormalAtoB; var ra = manifold.LeverArmA; var rb = manifold.LeverArmB;
                    var normalVelocity = MeterPoint.Dot(Bikes[b].Velocity + Left(rb) * Bikes[b].AngularVelocity
                        - Bikes[a].Velocity - Left(ra) * Bikes[a].AngularVelocity, n);
                    if (normalVelocity > 1e-9) continue; // unilateral support cannot push separating bodies
                    var relativeA = forces[b].Acceleration - forces[a].Acceleration
                        + Left(rb) * forces[b].AngularAcceleration - Left(ra) * forces[a].AngularAcceleration
                        - rb * (Bikes[b].AngularVelocity * Bikes[b].AngularVelocity) + ra * (Bikes[a].AngularVelocity * Bikes[a].AngularVelocity);
                    var reaction = Math.Max(0, -MeterPoint.Dot(relativeA, n)) / EffectiveInverseMass(a, b, manifold);
                    forces[a] = new(forces[a].Acceleration - n * (reaction / Mass(Bikes[a])),
                        forces[a].AngularAcceleration - reaction * MeterPoint.Cross(ra, n) / Inertia(Bikes[a]));
                    forces[b] = new(forces[b].Acceleration + n * (reaction / Mass(Bikes[b])),
                        forces[b].AngularAcceleration + reaction * MeterPoint.Cross(rb, n) / Inertia(Bikes[b]));
                }
            }
            var motions = Enumerable.Range(0, 4).Select(i => new Motion(this, Bikes[i], forces[i], Time, Time + remaining)).ToArray();
            var report = Observe(motions, false);
            // A consumed pair can leave the contact skin and touch it again
            // without establishing the wider release witness. Split that motion
            // at its actual touch too; apply support, not another consequence.
            var first = report.Intervals.Where(r => r.HasConflict && (!_active[Index(r.RiderA), Index(r.RiderB)]
                    || r.FirstTouchCommonTimeSeconds > Time))
                .OrderBy(r => r.FirstTouchCommonTimeSeconds).ThenBy(r => r.RiderA).ThenBy(r => r.RiderB).FirstOrDefault();
            var end = first is null ? Time + remaining : RefineTouch(first, motions);
            var dt = end - Time;
            if (dt > 0)
            {
                var proof = Observe(motions.Select(m => new Motion(this, m.Bike, m.Force, Time, end)), true);
                if (proof.FrameCoverageGaps.Count > 0 || proof.Intervals.Any(r =>
                    r.Kind.HasFlag(SpaceConflictKind.BoundaryAmbiguous)
                    || !r.NumericallyResolved && !_active[Index(r.RiderA), Index(r.RiderB)]
                    || r.MinimumSeparationLowerBoundMeters < -GeometryNumerics.ContactDistanceMeters))
                {
                    var worst = proof.Intervals.OrderBy(r => r.MinimumSeparationLowerBoundMeters).First();
                    LastRejectedWindow = FormattableString.Invariant($"[{Time:R},{end:R}] R{worst.RiderA}-R{worst.RiderB}, sampled={worst.MinimumSeparationMeters:R} m, lower={worst.MinimumSeparationLowerBoundMeters:R} m, resolved={worst.NumericallyResolved}");
                    break;
                }
                var next = Enumerable.Range(0, 4).Select(i => Evolve(Bikes[i], forces[i], dt)).ToArray();
                if (!CheckEndpoints(next)) break;
                Bikes = next.Select(b => { var road = Road(b.Position); return road.Offset < -1 || road.Offset > road.Width + 1
                    ? b with { Status = RiderRaceStatus.Retired } : b; }).ToArray();
                Time = end; remaining -= dt; Work.MovementUpdates += 4;
            }
            if (first is null) return true;
            if (!first.NumericallyResolved && !_active[Index(first.RiderA), Index(first.RiderB)]
                || report.Intervals.Count(r => r.HasConflict && !_active[Index(r.RiderA), Index(r.RiderB)]
                && r.FirstTouchCommonTimeSeconds <= end + GeometryNumerics.TimeToleranceSeconds) > 1)
            { LastRejectedWindow = "Unresolved or simultaneous fresh contact group."; break; }
            var ia = Index(first.RiderA); var ib = Index(first.RiderB); var m = Manifold(ia, ib);
            if (m is null) break;
            Impulse(ia, ib, m, support: _active[ia, ib]); _active[ia, ib] = true;
        }
        // One small transaction retry; no retained forecasts, cursor graph or replay history.
        Bikes = start; Time = time; Array.Copy(active, _active, active.Length);
        Array.Copy(released, _released, released.Length);
        Contacts.RemoveRange(count, Contacts.Count - count); return false;
    }
    private int Index(int id) => Array.FindIndex(Bikes, b => b.Rider.RiderId == id);
    private double RefineTouch(ContestedSpaceEvent hit, Motion[] motions)
    {
        var original = hit.FirstTouchCommonTimeSeconds!.Value;
        var a = motions[Index(hit.RiderA)]; var b = motions[Index(hit.RiderB)];
        double Gap(double t) { Work.NarrowTests++; return MechanicalSeparation.Between(a.SampleFootprint(t), b.SampleFootprint(t)).SignedMeters; }
        var hi = original; var lo = Math.Max(Time, original - GeometryNumerics.TimeToleranceSeconds);
        if (Gap(hi) < 0 && Gap(lo) > 0)
            for (var i = 0; i < 48; i++)
            {
                var mid = (lo + hi) * .5; if (mid == lo || mid == hi) break;
                var gap = Gap(mid);
                if (gap >= 0 && gap <= GeometryNumerics.ContactDistanceMeters * .5) { hi = mid; break; }
                if (gap < 0) hi = mid; else lo = mid;
            }
        MaximumContactRefinementSeconds = Math.Max(MaximumContactRefinementSeconds, original - hi);
        return hi;
    }
    private ContestedSpaceReport Observe(IEnumerable<PhysicalPoseInterval> motions, bool floor)
    {
        var report = floor ? ContestedSpaceResolver.ObserveExecutedContactWindow(motions) : ContestedSpaceResolver.ObserveFirstContact(motions);
        Work.GeometryCalls++; Work.NarrowTests += report.Work.NarrowPhaseEvaluations; Work.BroadRejects += report.Work.BroadPhaseRejects;
        return report;
    }
    private DynamicBike Evolve(DynamicBike b, Force f, double dt)
    {
        var displacement = b.Velocity * dt + f.Acceleration * (.5 * dt * dt);
        var next = b with { Position = b.Position + displacement, Velocity = b.Velocity + f.Acceleration * dt,
            Heading = BikeAngles.Wrap(b.Heading + b.AngularVelocity * dt + .5 * f.AngularAcceleration * dt * dt),
            AngularVelocity = b.AngularVelocity + f.AngularAcceleration * dt, Distance = b.Distance + displacement.Length };
        return next;
    }
    private bool CheckEndpoints(DynamicBike[] bikes)
    {
        for (var a = 0; a < 4; a++) for (var b = a + 1; b < 4; b++)
        {
            var gap = MechanicalSeparation.Between(Pose(bikes[a], Time), Pose(bikes[b], Time)).SignedMeters;
            if (gap < -GeometryNumerics.ContactDistanceMeters) return false;
            MaximumPenetration = Math.Max(MaximumPenetration, -gap);
        }
        return true;
    }
    private MechanicalContactManifold? Manifold(int a, int b)
    {
        var pa = Pose(Bikes[a], Time); var pb = Pose(Bikes[b], Time); var gap = MechanicalSeparation.Between(pa, pb);
        var row = new ContestedSpaceEvent(pa.RiderId, pb.RiderId, _map.FrameId, Time, Time, Time, gap.SignedMeters,
            Time, gap.ComponentA, gap.ComponentB, SpaceConflictKind.MutualConvergence, new(0, 0, 0, 0, 0, 0),
            pb.Position - pa.Position, Bikes[b].Velocity - Bikes[a].Velocity, true, gap.SignedMeters);
        return PhysicalContactGeometry.AtVerifiedTouch(new(row, pa, pb, Bikes[a].Velocity, Bikes[b].Velocity,
            new(pa.RiderId, Bikes[a].Rider.Profile.Gameplay, Bikes[a].Rider.Condition, Surface(Road(pa.Position)).EffectiveGrip),
            new(pb.RiderId, Bikes[b].Rider.Profile.Gameplay, Bikes[b].Rider.Condition, Surface(Road(pb.Position)).EffectiveGrip)), out _);
    }
    private double EffectiveInverseMass(int a, int b, MechanicalContactManifold m) => 1 / Mass(Bikes[a]) + 1 / Mass(Bikes[b])
        + Math.Pow(MeterPoint.Cross(m.LeverArmA, m.NormalAtoB), 2) / Inertia(Bikes[a])
        + Math.Pow(MeterPoint.Cross(m.LeverArmB, m.NormalAtoB), 2) / Inertia(Bikes[b]);
    private double Energy(DynamicBike b) => .5 * Mass(b) * b.Speed * b.Speed + .5 * Inertia(b) * b.AngularVelocity * b.AngularVelocity;
    private void Impulse(int a, int b, MechanicalContactManifold m, bool support)
    {
        var ba = Bikes[a]; var bb = Bikes[b]; var n = m.NormalAtoB;
        var pointA = ba.Velocity + Left(m.LeverArmA) * ba.AngularVelocity;
        var pointB = bb.Velocity + Left(m.LeverArmB) * bb.AngularVelocity;
        var closing = MeterPoint.Dot(pointB - pointA, n);
        var j = Math.Max(0, -(1 + (support ? 0 : _contact.Restitution)) * closing / EffectiveInverseMass(a, b, m));
        var before = Energy(ba) + Energy(bb);
        DynamicBike Apply(DynamicBike bike, MeterPoint delta, double dw) => bike with
        { Velocity = bike.Velocity + delta, AngularVelocity = bike.AngularVelocity + dw };
        var afterA = Apply(ba, n * (-j / Mass(ba)), -j * MeterPoint.Cross(m.LeverArmA, n) / Inertia(ba));
        var afterB = Apply(bb, n * (j / Mass(bb)), j * MeterPoint.Cross(m.LeverArmB, n) / Inertia(bb));
        var after = Energy(afterA) + Energy(afterB);
        Work.MaximumContactEnergyGain = Math.Max(Work.MaximumContactEnergyGain, after - before);
        if (after > before + 1e-8 * Math.Max(1, before)) throw new InvalidOperationException("Contact creates kinetic energy.");
        if (support) { if (j > 0) Work.SupportImpulses++; Bikes[a] = afterA; Bikes[b] = afterB; return; }
        (DynamicBike Bike, PhysicalContactSeverity Severity) Consequence(DynamicBike old, DynamicBike changed)
        {
            var delta = changed.Velocity - old.Velocity; var axis = Direction(old.Heading);
            var demand = Math.Sqrt(Math.Pow(_contact.ForwardDisturbanceWeight * MeterPoint.Dot(delta, axis), 2)
                + Math.Pow(_contact.LateralDisturbanceWeight * MeterPoint.Cross(axis, delta), 2)
                + Math.Pow(_contact.YawDisturbanceWeight * (changed.AngularVelocity - old.AngularVelocity) * SpeedwayBikeDimensions.Reference.BoundingRadiusMeters, 2));
            var reserve = PhysicalContactAnalyzer.Reserve(new(old.Rider.RiderId, old.Rider.Profile.Gameplay, old.Rider.Condition,
                Surface(Road(old.Position)).EffectiveGrip), _contact).StabilityReserveMetersPerSecond;
            var ratio = demand / reserve; var severity = PhysicalContactAnalyzer.Classify(ratio, _contact);
            var loss = severity == PhysicalContactSeverity.Brush ? 0 : new PhysicalContactConsequenceParameters().ControlLoss(ratio, _contact);
            return (changed with { ControlLoss = loss, RecoveryUntil = Time + _p.RecoverySeconds,
                Status = severity == PhysicalContactSeverity.Crash ? RiderRaceStatus.Crashed : old.Status }, severity);
        }
        var ca = Consequence(ba, afterA); var cb = Consequence(bb, afterB); Bikes[a] = ca.Bike; Bikes[b] = cb.Bike;
        var angle = Math.Abs(MeterPoint.Dot(n, Direction(ba.Heading)));
        Contacts.Add(new(Time, ba.Rider.RiderId, bb.Rider.RiderId, angle > .8 ? "rear" : angle < .3 ? "side" : "oblique",
            j, before, after, ca.Severity, cb.Severity, _released[a, b]));
    }
    private sealed class Motion(TimeStepRace race, DynamicBike bike, Force force, double start, double end)
        : PhysicalPoseInterval(bike.Rider.RiderId, race._map.FrameId, start, end, SpeedwayBikeDimensions.Reference, false)
    {
        internal readonly DynamicBike Bike = bike;
        internal readonly Force Force = force;
        internal override bool DeterministicArithmetic => true;
        internal override BikePoseValue SampleValue(double time)
        {
            Fraction(time); var dt = time - StartTimeSeconds;
            var velocity = Bike.Velocity + Force.Acceleration * dt;
            var heading = Bike.Heading + Bike.AngularVelocity * dt + .5 * Force.AngularAcceleration * dt * dt;
            var travel = velocity.Length > 0 ? Math.Atan2(velocity.Y, velocity.X) : heading;
            return new(Bike.Position + Bike.Velocity * dt + Force.Acceleration * (.5 * dt * dt), travel,
                heading, Dimensions, travel, true);
        }
        public override PhysicalBikePose Sample(double time) { Fraction(time); return race.Pose(race.Evolve(Bike, Force, time - StartTimeSeconds), time); }
        public override PoseRateBounds RateBounds(double from, double through)
        {
            Fraction(from); Fraction(through); var mid = (from + through) * .5 - StartTimeSeconds;
            return new(Bike.Velocity + Force.Acceleration * mid, Force.Acceleration.Length,
                Math.Max(Math.Abs(Bike.AngularVelocity + Force.AngularAcceleration * (from - StartTimeSeconds)),
                    Math.Abs(Bike.AngularVelocity + Force.AngularAcceleration * (through - StartTimeSeconds))));
        }
    }
}
