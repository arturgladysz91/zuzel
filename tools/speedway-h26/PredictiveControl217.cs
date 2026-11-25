
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const double PredictionSeconds217 = .24;
    private const double ProposalStep217 = .005;
    private static TwoContactBikeParameters Steering217 => Physics.TwoContactBike ?? TwoContactBikeParameters.Default;

    private sealed record Horizon217(State215 State, RiderView Bike, double Reference, double Clearance,
        double MinimumSwept, Forces215? Forces, double PeakUtilization, double FrontFlightSeconds, string? Failure);
    private sealed record PreviewDecision217(Command215? Command, int Proposals, int Verified,
        double? PredictedProgress, double? PredictedMinimumSwept, double? PredictedPeakUtilization,
        double? PredictedFrontFlight, double? AvailableFrontNewtons, double? AvailableRearNewtons,
        double? DesiredLateralAcceleration, Dictionary<string, int> Rejections, string? Reason);
    private sealed record Candidate217(Command215 Command, Horizon217 Outcome, double Cost);

    private static PreviewDecision217 PredictiveControl217(CornerObservation217 o, UnifiedModel215 model,
        CornerCourse216 course)
    {
        var reasons = new Dictionary<string, int>(StringComparer.Ordinal);
        int proposals = 0, checkedCandidates = 0;
        var start = new Horizon217(o.State, o.Bike, o.Reference, o.Clearance, o.Clearance, o.Previous, 0, 0, null);
        double activation = o.State.Time + ReactionSeconds216;
        var lead = Integrate(start, o.Queue, activation, StepSeconds216);
        if (lead.Failure is { } committedFailure)
        {
            Count(committedFailure);
            return new(null, 0, 0, null, null, null, null, null, null, null, reasons, "pending controls: " + committedFailure);
        }

        var g = o.Geometry;
        var motion = g.Motion(lead.State);
        var unsteered = g.Contact(lead.State with { Steer = 0 }, motion, true, 0);
        double longitudinal = unsteered.Velocity.Dot(unsteered.Forward);
        double alignment = Math.Atan2(unsteered.Velocity.Dot(unsteered.Side), longitudinal);
        var velocity = lead.State.Momentum.Plane / g.Base.Mass;
        double speed = velocity.Norm;
        double distance = Math.Max(g.Base.FrameFront.X - g.Base.FrameRear.X, speed * (ReactionSeconds216 + PredictionSeconds217));
        var ahead = Frame.At(lead.Reference + distance, o.Tuning.Lane).Position - lead.Bike.Position;
        var target = new Vec214(ahead.X, ahead.Y);
        var lateral = velocity.Norm > 0 ? velocity.Left / speed : unsteered.Side.Plane;
        double desiredLateral = 2 * speed * speed * target.Dot(lateral) / Math.Max(1e-9, target.Dot(target));

        double forceTarget = alignment;
        if (lead.Forces is { Front.Capacity: > 0, Front.Normal: > 0 } loads)
        {
            var other = loads.Rear.Force + loads.Drag + (loads.LeftFoot?.Force ?? default);
            double projected = unsteered.Side.Plane.Dot(lateral);
            if (Math.Abs(projected) > 1e-9)
            {
                double desired = (g.Base.Mass * desiredLateral - other.Plane.Dot(lateral)) / projected;
                double utilization = Math.Clamp(desired / loads.Front.Capacity, -1, 1);
                double gain = HandlingContact.Scale(longitudinal, .115, Gravity216 * loads.Front.Capacity / loads.Front.Normal);
                double lateralVelocity = Math.Atan2(gain * utilization, speed * Math.Sqrt(1 - utilization * utilization));
                forceTarget += lateralVelocity;
            }
        }
        double maxAngle = Steering217.MaximumSteeringRadians;
        double angleTravel = Steering217.SteeringRateRadiansPerSecond * PredictionSeconds217;
        double low = Math.Max(-maxAngle, lead.State.Steer - angleTravel);
        double high = Math.Min(maxAngle, lead.State.Steer + angleTravel);
        var angles = new[] { low, high, (low + high) / 2, alignment, forceTarget, (alignment + forceTarget) / 2, 0d }
            .Select(a => (float)Math.Clamp(a, low, high)).Distinct().ToArray();
        var drives = new[] { 0f, .5f, 1f, lead.State.Command.Bike.Drive }.Distinct().ToArray();
        double end = activation + PredictionSeconds217;
        Activation215[] pending = o.Queue[lead.State.Next..];
        var afterDelay = lead with { State = lead.State with { Next = 0 } };
        var unique = new HashSet<Command215>();
        var safe = new List<Candidate217>();
        var all = new List<Candidate217>();
        var held = lead.State.Command;
        foreach (float angle in angles)
        foreach (float drive in drives)
            Evaluate(new Command215(new(angle, drive), held.Target) { LeftFoot = held.LeftFoot });

        // Retain two mechanically reachable steering/drive choices for posture refinement.
        // If all fail, refine the two longest valid prefixes as a recovery search.
        var ranked = (safe.Count > 0 ? safe.OrderBy(x => x.Cost) :
            all.OrderByDescending(x => x.Outcome.State.Time).ThenBy(x => x.Cost)).Take(2).ToArray();
        double riderTravel = .5 * Actuator215.Limited.MaxForce / g.Base.RiderMass * PredictionSeconds217 * PredictionSeconds217;
        var offset = new Vec215(Math.Min(Command215.MaximumForwardTarget, riderTravel),
            Math.Min(Command215.MaximumLateralTarget, riderTravel), Math.Min(Command215.MaximumVerticalTarget, riderTravel));
        foreach (var candidate in ranked)
        {
            var action = candidate.Command;
            foreach (var targetShift in new[] { default, held.Target + Vec215.Forward * offset.X,
                         held.Target - Vec215.Forward * offset.X, held.Target + Vec215.Left * offset.Y,
                         held.Target - Vec215.Left * offset.Y, held.Target - Vec215.Up * offset.Z })
                Evaluate(action with { Target = WithinTravel(targetShift) });

            if (g.Leg is { } leg)
            {
                Evaluate(action with { LeftFoot = 0 });
                if (ContactTarget(leg, candidate.Outcome.State) is { } reach)
                    Evaluate(action with { LeftFoot = reach });
            }
        }

        foreach (var candidate in safe.OrderBy(x => x.Cost).Take(3))
        {
            checkedCandidates++;
            // Recheck the selected command from the original snapshot with its original
            // pending queue and the execution timestep. No forecast state is committed.
            Activation215[] commands = [.. o.Queue, new(o.State.Time, activation, candidate.Command)];
            var verified = Integrate(start, commands, end, StepSeconds216);
            if (verified.Failure is { } failure) { Count("verification: " + failure); continue; }
            return new(candidate.Command, proposals, checkedCandidates, verified.Reference - o.Reference,
                verified.MinimumSwept, verified.PeakUtilization, verified.FrontFlightSeconds,
                lead.Forces?.Front.Capacity, lead.Forces?.Rear.Capacity, desiredLateral, reasons, null);
        }
        return new(null, proposals, checkedCandidates, null, null, null, null, lead.Forces?.Front.Capacity,
            lead.Forces?.Rear.Capacity, desiredLateral, reasons, "no verified continuation");

        void Evaluate(Command215 action)
        {
            if (!unique.Add(action)) return;
            proposals++;
            Activation215[] queue = [.. pending, new(o.State.Time, activation, action)];
            var horizon = Integrate(afterDelay, queue, end, ProposalStep217);
            var k = g.Motion(horizon.State);
            var v = horizon.Bike.Velocity;
            double driveHeading = Math.Atan2(v.Y, v.X);
            double headingError = BikeAngles.Wrap(horizon.Bike.Road.Heading - driveHeading);
            double laneError = horizon.Bike.Road.OffsetMeters - o.Tuning.Lane;
            double rollDrift = k.BodyRate.X * PredictionSeconds217;
            double displacement = horizon.Reference - lead.Reference;
            double balance = rollDrift * rollDrift * distance + horizon.FrontFlightSeconds * speed;
            double cost = -displacement + laneError * laneError / distance + distance * headingError * headingError +
                balance + CornerCourse216.RequiredClearance * distance / Math.Max(.1, horizon.MinimumSwept);
            var result = new Candidate217(action, horizon, cost);
            all.Add(result);
            if (horizon.Failure is not { } failure) safe.Add(result);
            else Count(failure);
        }

        Horizon217 Integrate(Horizon217 initial, Activation215[] queue, double until, double dt)
        {
            var current = initial;
            while (current.State.Time < until - 1e-10)
            {
                try
                {
                    var row = model.Step(current.State, queue, Math.Min(dt, until - current.State.Time));
                    if (row.After.Time <= current.State.Time)
                        return current with { Failure = "clock stalled" };
                    var body = course.Mark(row.After, o.Geometry);
                    double reference = current.Reference + Frame.UnwrapDelta(current.Bike.Road.ReferenceMeters, body.Road.ReferenceMeters);
                    double clearance = course.Clearance(body);
                    double swept = CornerCourse216.Swept(current.Clearance, clearance, row);
                    double peak = Math.Max(row.Forces.Front.Utilization, row.Forces.Rear.Utilization);
                    current = new(row.After, body, reference, clearance, Math.Min(current.MinimumSwept, swept),
                        row.Forces, Math.Max(current.PeakUtilization, peak),
                        current.FrontFlightSeconds + (row.Forces.Front.Normal == 0 ? Duration215(row) : 0),
                        CornerCourse216.Violation(body, swept));
                    if (current.Failure != null) return current;
                }
                catch (InvalidOperationException error) { return current with { Failure = error.Message }; }
            }
            return current;
        }

        double? ContactTarget(Leg216 leg, State215 state)
        {
            var kinematics = g.Motion(state);
            double left = 0;
            if (Gap(0) <= 0) return 0;
            for (int i = 1; i <= 16; i++)
            {
                double right = i / 16d;
                if (Gap(right) > 0) { left = right; continue; }
                for (int j = 0; j < 18; j++)
                {
                    double mid = (left + right) / 2;
                    if (Gap(mid) <= 0) right = mid;
                    else left = mid;
                }
                return right;
            }
            return null;

            double Gap(double q) => leg.Foot(g, state, kinematics, leg.At(g, q)).Gap;
        }

        static Vec215 WithinTravel(Vec215 v) =>
            new(Math.Clamp(v.X, -Command215.MaximumForwardTarget, Command215.MaximumForwardTarget),
                Math.Clamp(v.Y, -Command215.MaximumLateralTarget, Command215.MaximumLateralTarget),
                Math.Clamp(v.Z, -Command215.MaximumVerticalTarget, Command215.MaximumVerticalTarget));

        void Count(string failure) => reasons[failure] = reasons.GetValueOrDefault(failure) + 1;
    }
}
