using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Seven driving parameters. No lateral destinations, XY anchors, or time-indexed actions.</summary>
public sealed record FreeRacingPolicy(double PreviewSeconds = 1.8, double DirectionGain = 1,
    double YawResponseSeconds = .25, double EdgeHorizonSeconds = 1, double GripFraction = .65,
    double ThrottleOpening = .22, double SteeringSpreadDegrees = .7)
{
    public double[] Values => new[] { PreviewSeconds, DirectionGain, YawResponseSeconds, EdgeHorizonSeconds, GripFraction, ThrottleOpening, SteeringSpreadDegrees };
    public static readonly string[] Names = { "PreviewSeconds", "DirectionGain", "YawResponseSeconds", "EdgeHorizonSeconds", "GripFraction", "ThrottleOpening", "SteeringSpreadDegrees" };
    public static readonly double[] Lower = { .8, .5, .12, .5, .35, .05, .25 }, Upper = { 2.8, 1.6, .45, 1.5, .85, .5, 1.5 };
    public static FreeRacingPolicy From(double[] x) {
        if (x.Length != 7 || x.Where((v, i) => !double.IsFinite(v) || v < Lower[i] || v > Upper[i]).Any()) throw new ArgumentException("Seven bounded policy parameters required.");
        return new(x[0], x[1], x[2], x[3], x[4], x[5], x[6]);
    }
    public void Validate() => From(Values);
}
public sealed record FreeRacingCandidate(FreeRacingAction Action, double Score, double ProgressMeters,
    double MinimumClearanceMeters, double ExitSpeed, double ExitBeta, double GeometrySpeedEnvelope);
public sealed record FreeRacingDecision(double Time, FreeRacingAction Action, FreeRacingCandidate[] Candidates, int ForceEvaluations);

/// <summary>One autonomous rider. Geometry supplies orientation and hazard anticipation, never a path.
/// Every candidate starts from the same accepted body, wheel, steering and pending-action queue.</summary>
public sealed class FreeRacingController
{
    public const double ReviewSeconds = .1;
    public FreeRacingPolicy Policy { get; }
    public FreeRacingAction Active { get; private set; } = new(0, 1);
    public double NextReview { get; private set; }
    private readonly List<FreeRacingCommand> pending = new();
    public IReadOnlyList<FreeRacingCommand> Pending => pending.AsReadOnly();
    public List<FreeRacingDecision> Decisions { get; } = new();
    public List<FreeRacingCommand> Activations { get; } = new();
    public int ForceEvaluations { get; private set; }
    public FreeRacingController(FreeRacingPolicy? policy = null) { Policy = policy ?? new(); Policy.Validate(); }
    public FreeRacingInput Input(RiderView v, double time, double omega, float steering) => new(v, time, omega, steering, Active, pending.ToArray(), NextReview);
    public void Advance(RiderView v, double time, double omega, float steering, RiderSpec spec, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters p, float mass, float inertia) {
        while (pending.Count > 0 && pending[0].ActivationAt <= time + 1e-10) { Active = pending[0].Action; Activations.Add(pending[0]); pending.RemoveAt(0); }
        if (time < NextReview - 1e-10) return;
        var decision = Choose(Input(v with { Drive = Active.Drive }, time, omega, steering), Policy, spec, frame, surface, p, mass, inertia);
        ForceEvaluations += decision.ForceEvaluations; Decisions.Add(decision);
        pending.Add(new(time, time + RiderController.ReactionSeconds(spec), decision.Action)); NextReview += ReviewSeconds;
    }
    public double Deadline => pending.Count > 0 ? Math.Min(NextReview, pending[0].ActivationAt) : NextReview;

    // This is explicitly a geometric hazard envelope, NOT a validated long Box2D rollout.
    // At measured offset, sample immutable curvature ahead; no offset is optimized or requested.
    public static double GeometrySpeedEnvelope(RiderView v, TrackFrame frame, FreeRacingPolicy policy, double accelerationCapacity) {
        var span = Math.Max(1, frame.ReferenceSpeed(v.Road, v.Speed) * policy.PreviewSeconds); double curvature = 0;
        for (var i = 0; i <= 12; i++) {
            var road = frame.RoadAt(v.TotalReferenceMeters + span * i / 12, v.Road.OffsetMeters);
            if (road.IsTurn && double.IsFinite(road.RadiusMeters)) curvature = Math.Max(curvature, 1 / road.RadiusMeters);
        }
        return curvature > 1e-6 ? Math.Sqrt(policy.GripFraction * accelerationCapacity / curvature) : 100;
    }
    public static FreeRacingDecision Choose(FreeRacingInput input, FreeRacingPolicy policy, RiderSpec spec, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters p, float mass, float inertia) {
        policy.Validate();
        // Account for unavoidable reaction and all earlier commands before forming a nominal request.
        var prefix = FreeRacingModel.Predict(input, spec, frame, surface, p, mass, inertia, RiderController.ReactionSeconds(spec));
        var at = prefix.Points[^1]; var v = at.Rider; var geometry = p.TwoContactBike ?? TwoContactBikeParameters.Default;
        var queries = prefix.ForceEvaluations;
        TwoContactForces Contact(float angle) {
            queries++; return TwoContactBikeDynamics.Forces(v, spec, frame, surface, p, at.Time, mass,
                new(at.Omega * (p.RearWheel ?? RearWheelParameters.Default).WheelRadiusMeters, p.RearWheel), new(at.Steering), .01f,
                steeringOverride: angle, inertia: inertia);
        }
        var current = Contact(at.Steering); var capacity = current.Budget / mass;
        var previewDistance = Math.Max(1, frame.ReferenceSpeed(v.Road, v.Speed) * policy.PreviewSeconds);
        var preview = frame.RoadAt(v.TotalReferenceMeters + previewDistance, v.Road.OffsetMeters);
        var chi = Math.Atan2(v.Velocity.Y, v.Velocity.X);
        var curvature = BikeAngles.Wrap(preview.Heading - v.Road.Heading) / previewDistance;
        var outwardVelocity = Vector2.Dot(v.Velocity, v.Road.Outward);
        // Only imminently closing edges cause repulsion. The interior has no preferred lateral position.
        const double edgeAllowance = .5;
        var outerThreat = Math.Max(0, edgeAllowance + Math.Max(0, outwardVelocity) * policy.EdgeHorizonSeconds - at.Outer);
        var innerThreat = Math.Max(0, edgeAllowance + Math.Max(0, -outwardVelocity) * policy.EdgeHorizonSeconds - at.Inner);
        var desiredRate = v.Speed * curvature + policy.DirectionGain * BikeAngles.Wrap(v.Road.Heading - chi)
            + (outerThreat - innerThreat) / Math.Max(v.Speed, 1);
        // Invert original contact torque at the predicted activation state. No #117 call or path input.
        double LocalScore(float angle) {
            var f = Contact(angle); var chiRate = TwoContactForces.Cross(v.Velocity, f.NetForce) / (mass * Math.Max(v.Speed * v.Speed, .25f));
            var yaw = v.AngularVelocity + policy.YawResponseSeconds * f.NetTorque / inertia;
            return Math.Pow(chiRate - desiredRate, 2) + Math.Pow(yaw - chiRate, 2);
        }
        var nominal = at.Steering; var best = LocalScore(nominal); var step = 2 * geometry.MaximumSteeringRadians / 32;
        void Consider(float angle) { var score = LocalScore(angle); if (score < best) { best = score; nominal = angle; } }
        for (var i = 0; i <= 32; i++) Consider(-geometry.MaximumSteeringRadians + step * i);
        for (var i = 0; i < 6; i++) { step /= 2; var center = nominal; Consider(Math.Max(-geometry.MaximumSteeringRadians, center - step)); Consider(Math.Min(geometry.MaximumSteeringRadians, center + step)); }
        var safeSpeed = GeometrySpeedEnvelope(v, frame, policy, capacity);
        var drive = (float)Math.Clamp(policy.ThrottleOpening + .15 * (safeSpeed - v.Speed), 0, 1);
        var spread = (float)(policy.SteeringSpreadDegrees * Math.PI / 180);
        var actions = (from angle in new[] { nominal - spread, nominal, nominal + spread }
                       from throttle in new[] { Math.Max(0, drive - .15f), drive, Math.Min(1, drive + .15f) }
                       select new FreeRacingAction(Math.Clamp(angle, -geometry.MaximumSteeringRadians, geometry.MaximumSteeringRadians), throttle)).Distinct().ToArray();
        var candidates = new List<FreeRacingCandidate>();
        foreach (var action in actions) {
            var prediction = FreeRacingModel.Predict(input, spec, frame, surface, p, mass, inertia, .9, action); queries += prediction.ForceEvaluations;
            var end = prediction.Points[^1]; var min = prediction.Points.Min(x => Math.Min(x.Inner, x.Outer));
            var progress = end.Rider.TotalReferenceMeters - input.Rider.TotalReferenceMeters;
            var envelope = GeometrySpeedEnvelope(end.Rider, frame, policy, capacity);
            var closing = Vector2.Dot(end.Rider.Velocity, end.Rider.Road.Outward);
            var futureGap = Math.Min(end.Inner - Math.Max(0, -closing) * policy.EdgeHorizonSeconds, end.Outer - Math.Max(0, closing) * policy.EdgeHorizonSeconds);
            var lossOfControl = Math.Max(0, Math.Abs(end.Rider.SlideRadians) - Math.PI / 4);
            // Safety over the complete rollout and future edge closure dominate fractional progress.
            // No lane/offset/path error, and no penalty for controlled beta itself.
            var score = -progress + 10000 * Math.Pow(Math.Max(0, -min), 2) + 20 * Math.Pow(Math.Max(0, edgeAllowance - futureGap), 2)
                + 2 * Math.Pow(Math.Max(0, end.Rider.Speed - envelope), 2) + 10000 * lossOfControl * lossOfControl;
            candidates.Add(new(action, score, progress, min, end.Rider.Speed, end.Rider.SlideRadians, envelope));
        }
        var chosen = candidates.OrderBy(c => c.Score).ThenBy(c => Math.Abs(c.Action.SteeringRadians - input.Active.SteeringRadians)).ThenBy(c => c.Action.Drive).First();
        return new(input.Time, chosen.Action, candidates.ToArray(), queries);
    }
}
