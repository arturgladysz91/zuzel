using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Efficient147;
using Efficient152;
using LearnedCorner166;
using Safety153;
using SharedWorldSpeedway;
using Speed148;

namespace FastTwoCorners167;

internal sealed record Tuning(double FirstSpeedGain, double FirstMarginMeters, double BendSpeedScale,
    double BendMarginMeters, double PrepareMeters, double CoastMetersPerSecondSquared,
    double LateralBudgetShare, double Feedback, double EntryWidthMeters);

internal sealed class TwoCornerPolicy(Policy first, Fit fit, Weights weights, Tuning tuning,
    Course course, RiderSpec rider, TrackFrame track, TrackStateSnapshot surface,
    WorldParameters parameters, float mass, float inertia)
{
    private const double ReviewSeconds = .2;
    private const double PrepareBeforeExitMeters = 17;
    private const double PreviewStepMeters = 2;
    private const double MaximumPreviewMeters = 86;
    private readonly Boundary152 boundary = new(track);
    private readonly PredictionBoundary147 cache = new(track);
    public long ForecastContactQueries { get; private set; }
    public long Forecasts { get; private set; }

    public FreeRacingAction Choose(FreeRacingInput input, CrankClutch144 crank)
    {
        double s = input.Rider.TotalReferenceMeters;
        if (s < course.First.Exit - PrepareBeforeExitMeters)
            return first.Choose(input, crank);

        double delay = RiderController.ReactionSeconds(rider);
        double shaftOmega = crank.Omega;
        var delayPrediction = PredictorA153.Predict(input, rider, track, surface, parameters, mass, inertia,
            delay, powertrain: crank, boundaryCache: cache, fastBoundary: boundary, exactInitialWheel: true,
            observeCrank: (_, omega) => shaftOmega = omega);
        Forecasts++;
        ForecastContactQueries += delayPrediction.ForceEvaluations;
        var activation = delayPrediction.Points[^1];
        var bike = activation.Rider;
        var path = Target(bike);
        var k = Policy.Gains.At(bike.Speed);
        double courseError = BikeAngles.Wrap(Teacher.CourseError(bike) - path.Course);
        double headingError = BikeAngles.Wrap(bike.Heading - bike.Road.Heading - path.Course - path.Beta);
        double yawError = bike.AngularVelocity - path.Yaw;
        double offsetError = bike.Road.OffsetMeters - path.Offset;
        double gain = tuning.Feedback * (bike.Road.IsTurn ? 1 : .56);
        double correction = -gain * (k[0] * offsetError + k[1] * courseError * weights.Course +
            k[2] * headingError * weights.Heading + k[3] * yawError * weights.Yaw);
        double steer = path.Steer + Math.Clamp(correction, -.115, .115);
        var steeringLimit = TwoContactBikeParameters.Default.MaximumSteeringRadians;
        var steering = (float)Math.Clamp(steer, -steeringLimit, steeringLimit);

        var contact = ContactA152.Forces(bike, rider, track, surface, parameters, activation.Time, mass,
            ForecastFork148.Wheel(activation.Omega, parameters.RearWheel), new(activation.Steering), .01f,
            steeringOverride: steering, inertia: inertia, powertrain: new(crank.Parameters, shaftOmega), releaseAt: delay);
        ForecastContactQueries++;
        var loads = contact.TyreLoads!;
        double frontUse = Math.Abs(contact.FrontLateral) / loads.FrontCapacityNewtons;
        double rearUse = Math.Sqrt(Math.Pow(contact.RearLateral, 2) + Math.Pow(contact.RearLongitudinal, 2))
            / loads.RearCapacityNewtons;
        double betaDot = BikeAngles.Wrap(bike.SlideRadians - input.Rider.SlideRadians) / delay;
        double outwardSpeed = Vector2.Dot(bike.Velocity, bike.Road.Outward);
        double closingGap = outwardSpeed > 0 ? activation.Outer : activation.Inner;
        double clearancePressure = Math.Max(0, 1.05 - closingGap + ReviewSeconds * Math.Abs(outwardSpeed));

        double speedEnvelope = PreviewSpeed(bike, path.Offset, loads.TotalCapacityNewtons / mass);
        double feedForward = path.Drive + .18 * (path.Speed - bike.Speed);
        // Enter full drive whenever the pending-adjusted state can afford it.
        // The backward envelope reserves native coast distance to future curvature.
        double approach = (speedEnvelope - bike.Speed) * .44;
        double gas = bike.Road.IsTurn ? Math.Min(feedForward, .30 + approach) : .28 + approach;
        var slip = RearWheelDynamics.SlipRatio(bike.ForwardSpeed, activation.Omega, RearWheelParameters.Default);
        gas -= .75 * Math.Max(0, Math.Max(frontUse, rearUse) - .90);
        gas -= .32 * Math.Max(0, slip - .22);
        gas -= .35 * Math.Max(0, Math.Abs(bike.SlideRadians) + .20 * Math.Max(0, betaDot * Math.Sign(bike.SlideRadians)) - .20);
        gas -= .32 * clearancePressure;

        if (bike.TotalReferenceMeters < course.First.Exit)
        {
            // Blend the late first-bend action into the second-bend plan. This
            // starts the curvature preview before reaching the intervening straight.
            double mix = Smooth((bike.TotalReferenceMeters - course.First.Exit + PrepareBeforeExitMeters) /
                PrepareBeforeExitMeters);
            var original = first.Choose(input, crank);
            steering = (float)(original.SteeringRadians + (steering - original.SteeringRadians) * mix);
            gas = original.Drive + (gas - original.Drive) * mix;
        }

        return new(steering, (float)Math.Clamp(gas, 0, 1));
    }

    private double PreviewSpeed(RiderView bike, double targetOffset, double availableLateralAcceleration)
    {
        double envelopeSquared = double.PositiveInfinity;
        for (double ds = 0; ds <= MaximumPreviewMeters; ds += PreviewStepMeters)
        {
            var road = track.RoadAt(bike.TotalReferenceMeters + ds, targetOffset);
            if (!road.IsTurn || !double.IsFinite(road.RadiusMeters)) continue;
            double localSquared = tuning.LateralBudgetShare * availableLateralAcceleration * road.RadiusMeters;
            envelopeSquared = Math.Min(envelopeSquared, localSquared + 2 * tuning.CoastMetersPerSecondSquared * ds);
        }
        return Math.Sqrt(envelopeSquared);
    }

    private ProfilePoint Target(RiderView bike)
    {
        double s = bike.TotalReferenceMeters;
        var firstBend = course.First;
        var secondBend = course.Second;
        if (s < firstBend.Exit)
        {
            var first = Learned(s);
            return first with { Speed = first.Speed * weights.SpeedScale * tuning.FirstSpeedGain,
                Offset = first.Offset + tuning.FirstMarginMeters };
        }

        // The longitudinal coordinate wraps, while each transition uses its own
        // entry/exit from the frame. The reserve continues to plan the next turn.
        bool between = s < secondBend.Exit;
        var bend = between ? secondBend : new Bend(firstBend.Entry + track.LapMeters, firstBend.Exit + track.LapMeters);
        double previousExit = between ? firstBend.Exit : secondBend.Exit;
        double toEntry = bend.Entry - s;
        double bendFraction = Math.Clamp((s - bend.Entry) / (bend.Exit - bend.Entry), 0, 1);
        double reference = toEntry > 0 ? firstBend.Entry - toEntry :
            firstBend.Entry + bendFraction * (firstBend.Exit - firstBend.Entry);
        var learned = Learned(reference);
        var exit = Learned(firstBend.Exit);
        var widthRetention = 1 - Smooth(bendFraction / .38);
        var lane = learned.Offset + tuning.BendMarginMeters + tuning.EntryWidthMeters * widthRetention;

        if (toEntry <= 0)
            return learned with { Offset = lane, Speed = learned.Speed * tuning.BendSpeedScale };

        double afterExit = s - previousExit;
        double transition = Smooth(afterExit / (.82 * (bend.Entry - previousExit)));
        double settle = 1 - Smooth(afterExit / 10);
        double prepare = Smooth(1 - toEntry / tuning.PrepareMeters);
        double previousMargin = between ? tuning.FirstMarginMeters : tuning.BendMarginMeters;
        var preview = track.RoadAt(s + bike.Speed * .45, lane);
        double curvatureYaw = preview.IsTurn ? bike.Speed / preview.RadiusMeters : 0;
        return learned with
        {
            Offset = (exit.Offset + previousMargin) * (1 - transition) + lane * transition,
            Course = exit.Course * settle + learned.Course * prepare,
            Beta = exit.Beta * settle + learned.Beta * prepare,
            Yaw = exit.Yaw * settle + Math.Max(learned.Yaw, curvatureYaw * .65) * prepare,
            Steer = exit.Steer * settle + learned.Steer * prepare,
        };
    }

    private ProfilePoint Learned(double s) => Teacher.Blend(fit.A.At(s), fit.B.At(s), weights.Blend);
    private static double Smooth(double x)
    {
        double u = Math.Clamp(x, 0, 1);
        return u * u * (3 - 2 * u);
    }
}
