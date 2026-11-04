using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace FullDrive169;

internal sealed class Stabilizer169(double targetOffset, PhysicsVariant variant, double variantFrom)
{
    private const double ReviewHorizonSeconds = .44;
    private const double LineLookaheadMeters = 10;
    private static readonly float[] CoarseRadians = [-.12f, -.08f, -.04f, -.02f, -.01f, -.004f, 0,
        .004f, .01f, .02f, .04f, .08f, .12f];
    private double previousTime = double.NaN;
    private double previousBeta;

    public void Observe(FreeRacingInput input)
    {
        previousTime = input.Time;
        previousBeta = input.Rider.SlideRadians;
    }

    public Decision144 Choose(FreeRacingInput input, CrankClutch144 crank, RiderSpec spec, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, float drive, double acceptedAx)
    {
        var measuredRate = double.IsNaN(previousTime) ? 0 :
            BikeAngles.Wrap(input.Rider.SlideRadians - previousBeta) / (input.Time - previousTime);
        Observe(input);
        var best = new FreeRacingAction(0, drive);
        double lowest = double.PositiveInfinity;
        int queries = 0, forecasts = 0;
        void Evaluate(float steer)
        {
            var action = new FreeRacingAction(steer, drive);
            var prediction = Predictor169.Predict(input, spec, frame, surface, parameters, mass, inertia,
                ReviewHorizonSeconds, candidate: action, powertrain: crank, experiment: variant,
                experimentFrom: variantFrom, acceptedAx: acceptedAx);
            queries += prediction.ForceEvaluations;
            forecasts++;
            var end = prediction.Points[^1];
            var before = prediction.Points[^2];
            var bike = end.Rider;
            var betaDot = BikeAngles.Wrap(bike.SlideRadians - before.Rider.SlideRadians) / (end.Time - before.Time);
            var offset = bike.Road.OffsetMeters - targetOffset;
            var chi = Math.Atan2(bike.Velocity.Y, bike.Velocity.X);
            var course = BikeAngles.Wrap(chi - bike.Road.Heading - Math.Atan(offset / LineLookaheadMeters));
            var outward = Vector2.Dot(bike.Velocity, bike.Road.Outward);
            var gap = Math.Min(end.Inner, end.Outer);
            double score = 2.2 * course * course + .015 * offset * offset + .009 * outward * outward
                + (2 + Math.Min(2, Math.Abs(measuredRate))) * bike.SlideRadians * bike.SlideRadians
                + .22 * bike.AngularVelocity * bike.AngularVelocity + .018 * betaDot * betaDot
                + .4 * steer * steer + 8 * Math.Pow(Math.Max(0, .9 - gap), 2);
            if (score < lowest)
            {
                lowest = score;
                best = action;
            }
        }

        foreach (float steer in CoarseRadians) Evaluate(steer);
        float coarse = best.SteeringRadians;
        foreach (float offset in new[] { -.003f, -.0015f, .0015f, .003f }) Evaluate(coarse + offset);
        // Cost is logged as queries/forecasts. Native process CPU is booked by the experiment.
        return new(new(input.Time, best, FreeActionSafety.Uncertain, false, [], queries, forecasts), 0);
    }
}
