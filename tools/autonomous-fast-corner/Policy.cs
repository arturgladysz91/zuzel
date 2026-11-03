using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Efficient147;
using Efficient152;
using Safety153;
using SharedWorldSpeedway;

namespace LearnedCorner166;

internal sealed record Weights(double Blend, double Margin, double Feedback, double Course, double Heading,
    double Yaw, double SpeedScale, double DriveFeedback, double StraightBoost, double PrepareMeters,
    double TerminalSpeedScale, double WheelSlipFeedback, double LaunchMergeMeters, double LaneRetention = 0);

internal sealed class Policy
{
    public static readonly LateralGains Gains = new();
    private readonly Teacher a, b;
    private readonly Weights w;
    private readonly RiderSpec rider;
    private readonly TrackFrame frame;
    private readonly TrackStateSnapshot surface;
    private readonly WorldParameters parameters;
    private readonly Boundary152 boundary;
    private readonly PredictionBoundary147 cache;
    private readonly double entry, exit, next;
    private readonly float mass, inertia;
    private readonly bool pilot;
    public long ForecastContactQueries { get; private set; }

    public Policy(Teacher a, Teacher b, Weights w, RiderSpec rider, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, bool pilot = false)
    {
        this.a = a; this.b = b; this.w = w; this.rider = rider; this.frame = frame;
        this.surface = surface; this.parameters = parameters; this.mass = mass; this.inertia = inertia; this.pilot = pilot;
        boundary = new(frame);
        cache = new(frame);
        entry = frame.SegmentStartMeters(1);
        exit = frame.SegmentStartMeters(4);
        next = frame.SegmentStartMeters(5);
    }

    private ProfilePoint Learned(double s) => Teacher.Blend(a.At(s), b.At(s), w.Blend);

    private static double Smooth(double x)
    {
        var u = Math.Clamp(x, 0, 1);
        return u * u * (3 - 2 * u);
    }

    private ProfilePoint Target(double s)
    {
        if (s <= exit)
        {
            var p = Learned(s);
            double merge = Math.Clamp((s - rider.ReferenceMeters) / w.LaunchMergeMeters, 0, 1);
            double blend = Smooth(merge);
            double shift = (rider.OffsetMeters - Learned(0).Offset) * (1 - blend);
            double slope = -(rider.OffsetMeters - Learned(0).Offset) * 6 * merge * (1 - merge) / w.LaunchMergeMeters;
            // An outer launch can use an outer line through the high-curvature
            // entry instead of merging several metres as speed builds.
            var taper = Math.Clamp((s - 80) / 45, 0, 1);
            var outer = Math.Max(0, rider.OffsetMeters - Learned(0).Offset - .8) * w.LaneRetention;
            shift += outer * blend * (1 - Smooth(taper));
            slope += outer * (6 * merge * (1 - merge) / w.LaunchMergeMeters *
                (1 - Smooth(taper)) - blend * 6 * taper * (1 - taper) / 45);
            double margin = w.Margin * Smooth((s - 22) / 13);
            return p with { Offset = p.Offset + shift + margin,
                Course = p.Course - Math.Atan(slope), Speed = p.Speed * w.SpeedScale };
        }

        double after = s - exit;
        double toNext = next - s;
        var end = Learned(exit);
        var upcoming = Learned(entry - toNext);
        var phase = Math.Clamp(after / 62, 0, 1);
        var mix = Smooth(after / 52);
        var settle = 1 - Smooth(after / 10);
        var prepare = Smooth(1 - toNext / w.PrepareMeters);
        var offset = end.Offset * (1 - mix) + upcoming.Offset * mix + w.Margin;
        var speed = end.Speed * w.SpeedScale * (1 - phase) +
            Learned(entry).Speed * w.TerminalSpeedScale * phase + w.StraightBoost * Math.Sin(Math.PI * phase);
        if (s >= next)
            speed = upcoming.Speed * w.TerminalSpeedScale;
        var gas = .22 + .16 * w.StraightBoost * Math.Cos(Math.PI * phase);
        return new(s, offset, end.Course * settle + upcoming.Course * prepare,
            end.Beta * settle + upcoming.Beta * prepare, end.Yaw * settle + upcoming.Yaw * prepare,
            speed, end.Steer * settle + upcoming.Steer * prepare, gas * (1 - prepare) + upcoming.Drive * prepare);
    }

    public FreeRacingAction Choose(FreeRacingInput input, CrankClutch144 crank)
    {
        if (input.Rider.Speed < 3)
            return new(0, 1);

        var forecast = PredictorA153.Predict(input, rider, frame, surface, parameters, mass, inertia,
            .24, powertrain: crank, boundaryCache: cache, fastBoundary: boundary, exactInitialWheel: true);
        ForecastContactQueries += forecast.ForceEvaluations;
        var end = forecast.Points[^1];
        var bike = end.Rider;
        var target = Target(bike.TotalReferenceMeters);
        var k = Gains.At(bike.Speed);
        double courseError = BikeAngles.Wrap(Teacher.CourseError(bike) - target.Course);
        double headingError = BikeAngles.Wrap(bike.Heading - bike.Road.Heading - target.Course - target.Beta);
        double yawError = bike.AngularVelocity - target.Yaw;
        double offsetError = bike.Road.OffsetMeters - target.Offset;
        var straightGain = pilot || bike.Road.IsTurn ? 1 : .56;
        double correction = -w.Feedback * straightGain * (k[0] * offsetError + k[1] * courseError * w.Course +
            k[2] * headingError * w.Heading + k[3] * yawError * w.Yaw);
        double wheelSlip = RearWheelDynamics.SlipRatio(bike.ForwardSpeed, end.Omega, RearWheelParameters.Default);
        double gas = target.Drive + w.DriveFeedback * (target.Speed - bike.Speed) -
            w.WheelSlipFeedback * Math.Max(0, wheelSlip - .25) * Math.Min(1, Math.Abs(courseError) * 5);
        // A bounded proposal avoids large alternating changes when the local linear model
        // is least accurate. The independent native actuator keeps its original limits.
        if (!pilot) correction = Math.Clamp(correction, -.115, .115);
        double available = TwoContactBikeParameters.Default.MaximumSteeringRadians;
        return new((float)Math.Clamp(target.Steer + correction, -available, available),
            (float)Math.Clamp(gas, 0, 1));
    }
}
