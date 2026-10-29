// Generated mechanically from frozen StandingStart144/Predictor144.cs. See source-proof.json; no physical formula changes.
using SharedWorldSpeedway;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
namespace Efficient147;
public static class CachedPredictor147
{
    public const double MaximumHorizonSeconds = .9;
    public static FreeRacingPrediction Predict(FreeRacingInput input, RiderSpec spec, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters p, float mass, float inertia, double horizon,
        FreeRacingAction? candidate = null, FreeRacingPolicy? closedLoop = null,
        Func<FreeRacingInput, (FreeRacingAction Action, int Evaluations)>? causalPolicy = null, double reviewSeconds = .1,CrankClutch144? powertrain=null,Action<double,double>? observeCrank=null,PredictionBoundary147? boundaryCache=null)
    {
        var shaft=powertrain?.Copy();
        if(shaft is not null&&(closedLoop is not null||causalPolicy is not null))throw new ArgumentException("Launch prediction accepts a held candidate only.");
        if (!p.EnableFreeRacing) throw new InvalidOperationException("Free Racing is OFF by default.");
        if (!double.IsFinite(horizon) || horizon <= 0 || horizon > MaximumHorizonSeconds + 1e-10
            || !float.IsFinite(mass) || mass <= 0 || !float.IsFinite(inertia) || inertia <= 0
            || !double.IsFinite(input.Time) || !double.IsFinite(input.Omega) || input.Omega < 0
            || !double.IsFinite(input.NextReview) || input.NextReview < input.Time - 1e-8
            || candidate is not null && (closedLoop is not null || causalPolicy is not null)
            || closedLoop is not null && causalPolicy is not null || !double.IsFinite(reviewSeconds) || reviewSeconds < .1 || reviewSeconds > .4)
            throw new ArgumentException("Bounded causal prediction required.");
        input.Active.Validate(); candidate?.Validate();
        var reaction = RiderController.ReactionSeconds(spec);
        foreach (var command in input.Pending) {
            command.Action.Validate();
            if (!double.IsFinite(command.ObservedAt) || !double.IsFinite(command.ActivationAt)
                || command.ObservedAt > input.Time + 1e-8 || command.ActivationAt < input.Time - 1e-8
                || Math.Abs(command.ActivationAt - command.ObservedAt - reaction) > 1e-8)
                throw new ArgumentException("Only already observed pending actions with full reaction are causal.");
        }
        var queue = input.Pending.OrderBy(c => c.ActivationAt).ToList();
        if (candidate is { } action) queue.Add(new(input.Time, input.Time + reaction, action));
        var active = input.Active; var v = input.Rider;
        var wheel = new RearWheelState(input.Omega * (p.RearWheel ?? RearWheelParameters.Default).WheelRadiusMeters, p.RearWheel);
        var steering = new TwoContactBikeState(input.Steering); var boundary = boundaryCache?.For(frame)??new RacingLineGeometry(frame);
        var points = new List<FreeRacingPoint>(); var time = input.Time; var end = time + horizon;
        var nextReview = input.NextReview; var forces = 0; double distance = 0;
        void Activate() {
            while (queue.Count > 0 && queue[0].ActivationAt <= time + 1e-10) { active = queue[0].Action; queue.RemoveAt(0); }
            v = v with { Drive = active.Drive };
        }
        void Point() { observeCrank?.Invoke(time,shaft?.Omega??0); var gap = boundary.Clearance(v); points.Add(new(time, v, wheel.AngularVelocityRadPerSecond, steering.SteeringRadians, gap.Inner, gap.Outer)); }
        Activate(); Point();
        while (time < end - 1e-10) {
            Activate();
            if ((closedLoop is not null || causalPolicy is not null) && time >= nextReview - 1e-10) {
                var observed = new FreeRacingInput(v, time, wheel.AngularVelocityRadPerSecond, steering.SteeringRadians, active, queue.ToArray(), nextReview);
                if (causalPolicy is not null) {
                    var decision = causalPolicy(observed); forces += decision.Evaluations;
                    queue.Add(new(time, time + reaction, decision.Action)); nextReview += reviewSeconds;
                } else {
                    var decision = FreeRacingController.Choose(observed, closedLoop!, spec, frame, surface, p, mass, inertia);
                    forces += decision.ForceEvaluations; queue.Add(new(time, time + reaction, decision.Action));
                    nextReview += FreeRacingController.ReviewSeconds;
                }
            }
            points[^1] = points[^1] with { Rider = v };
            var nominal = (Math.Floor((time + 1e-8) / .01) + 1) * .01;
            var duration = Math.Min(end - time, nominal - time);
            if (queue.Count > 0 && queue[0].ActivationAt > time + 1e-10) duration = Math.Min(duration, queue[0].ActivationAt - time);
            if ((closedLoop is not null || causalPolicy is not null) && nextReview > time + 1e-10) duration = Math.Min(duration, nextReview - time);
            TwoContactForces Query(float dt) {
                forces++;
                return Contact144.Forces(v, spec, frame, surface, p, time, mass, wheel, steering, dt,
                    steeringOverride: active.SteeringRadians, inertia: inertia,powertrain:shaft,releaseAt:reaction);
            }
            // Identical curvature event estimate to WorldRunner/#126. No future control tape.
            if (frame.DistanceToCurvatureChange(v.Road) <= (v.Speed + 5) * duration + .001) {
                var probe = Query(0); var along = Vector2.Dot(v.Velocity, v.Road.Tangent);
                var acceleration = Vector2.Dot(probe.NetForce, v.Road.Tangent) / mass;
                var b = (p.SolverSubsteps + 1d) / (2 * p.SolverSubsteps);
                var s = frame.DistanceToCurvatureChange(v.Road); var discriminant = along * along + 4 * b * acceleration * s;
                if (along > 0 && discriminant >= 0 && s <= along * duration + b * acceleration * duration * duration) {
                    var crossing = 2 * s / (along + Math.Sqrt(discriminant));
                    duration = Math.Min(duration, Math.Max(1e-6, Math.Ceiling(crossing / 1e-6) * 1e-6));
                }
            }
            var h = (float)duration; var applied = Query(h);
            // #126 equations: N semi-implicit native substeps, forces held for the interval.
            var a = applied.NetForce / mass; var alpha = applied.NetTorque / inertia;
            var coefficient = (p.SolverSubsteps + 1f) / (2 * p.SolverSubsteps);
            var position = v.Position + v.Velocity * h + a * (coefficient * h * h);
            var velocity = v.Velocity + a * h; var spin = v.AngularVelocity + alpha * h;
            var heading = (float)BikeAngles.Wrap(v.Heading + v.AngularVelocity * h + coefficient * alpha * h * h);
            var road = frame.Project(position); var ds = Vector2.Distance(v.Position, position); distance += ds;
            v = v with { Position = position, Velocity = velocity, AngularVelocity = spin, Heading = heading, Road = road,
                TotalReferenceMeters = v.TotalReferenceMeters + frame.UnwrapDelta(v.Road.ReferenceMeters, road.ReferenceMeters), DistanceMeters = v.DistanceMeters + ds };
            wheel.Commit(); steering.Commit();shaft?.Commit(); time += duration; Activate(); Point();
        }
        return new(points.ToArray(), forces, distance);
    }
}
