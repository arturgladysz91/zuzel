using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace FastCorner165;

internal sealed record Plan(string Family, int TargetKph, double StartMeters, float StartOffset,
    double[] Knobs)
{
    public bool Anchor => Family != "free" && Knobs.All(value => value == 0);
}

internal static class Guide
{
    public static FreeRacingAction Seed(Plan plan, RiderRun144 seed, FreeRacingInput input,
        Section section)
    {
        var p = plan.Knobs;
        double reference = input.Rider.TotalReferenceMeters;
        double progress = (reference - section.Entry) / (section.Exit - section.Entry);
        double clock = input.Time;
        var index = Math.Min((int)Math.Round(clock / .2), seed.Decisions.Length - 1);
        var observed = seed.Samples[Math.Min((int)(clock * 100), seed.Samples.Length - 1)].Input.Rider;
        if (!plan.Anchor)
        {
            // Rephase a perturbation with observed progress. The archived command is
            // a proposal, and all edited commands still pass through the native queue.
            clock += Math.Clamp((reference - observed.TotalReferenceMeters) /
                Math.Max(8, observed.Speed) * p[0], -.65, .65);
            index = Math.Clamp((int)Math.Round(clock / .2), 0, seed.Decisions.Length - 1);
        }
        var original = seed.Decisions[index].Choice.Action;
        double approach = progress < .06 ? p[1] * Math.Clamp((reference - 7) / 12, 0, 1) : 0;
        double bend = progress is >= .06 and < .87 ? p[2] : 0;
        double straight = progress >= .87 && reference < section.NextEntry - 5 ? p[3] : 0;
        double steering = original.SteeringRadians;
        if (progress is > -.19 and < .25)
            steering += p[4] * Math.Sin(Math.PI * (progress + .19) / .44);
        if (progress is > .50 and < 1.12)
            steering += p[5] * Math.Sin(Math.PI * (progress - .50) / .62);
        return Action(steering, original.Drive + approach + bend + straight);
    }

    public static FreeRacingAction Free(Plan plan, FreeRacingInput input, TrackFrame frame, Section section)
    {
        var p = plan.Knobs;
        var bike = input.Rider;
        double speed = bike.Speed;
        if (speed < 2)
            return new(0, 1);

        // Approximate a proposal at activation. Only the native step decides where
        // the body goes, and it applies every queued command in its original order.
        const double delay = .24;
        var course = Math.Atan2(bike.Velocity.Y, bike.Velocity.X);
        var futureCourse = course + bike.AngularVelocity * delay * .55;
        var position = bike.Position + bike.Velocity * (float)delay;
        var road = frame.Project(position);
        var reference = bike.TotalReferenceMeters +
            frame.UnwrapDelta(bike.Road.ReferenceMeters, road.ReferenceMeters);
        double preview = Math.Max(3.5, speed * p[0]);
        var target = frame.At(reference + preview, Offset(plan, section, reference + preview));
        var delta = target.Position - position;
        var angle = BikeAngles.Wrap(Math.Atan2(delta.Y, delta.X) - futureCourse);
        double curvature = 2 * Math.Sin(angle) / Math.Max(3, delta.Length());
        var yawTarget = speed * curvature;
        var futureHeading = bike.Heading + delay * bike.AngularVelocity;
        var geometry = TwoContactBikeParameters.Default;
        double steer = BikeAngles.Wrap(futureCourse - futureHeading) +
            Math.Atan(geometry.FrontAxleFromComMeters * bike.AngularVelocity / speed) +
            p[1] * curvature * speed * speed +
            p[2] * (yawTarget - bike.AngularVelocity);
        // A signed steering impulse can initiate or catch a slide. It is ranked
        // solely by the resulting complete native trajectory.
        var bendFraction = (reference - section.Entry) / (section.Exit - section.Entry);
        if (bendFraction is > -.10 and < .28)
            steer += p[3] * Math.Sin(Math.PI * (bendFraction + .10) / .38);

        double gas;
        if (reference < section.Entry + p[4])
        {
            double targetSpeed = plan.TargetKph / 3.6;
            gas = Math.Clamp(.28 + (targetSpeed - speed) * .42, 0, 1);
        }
        else if (reference < section.Entry + 20) gas = p[5];
        else if (reference < section.Exit - p[6]) gas = p[7];
        else if (reference < section.NextEntry - p[8]) gas = p[9];
        else gas = p[10];
        return Action(steer, gas);
    }

    public static FreeRacingAction Damped(Plan plan, FreeRacingInput input, TrackFrame frame, Section section)
    {
        var p = plan.Knobs;
        var bike = input.Rider;
        var speed = bike.Speed;
        if (speed < 2)
            return new(0, 1);

        const double delay = .24;
        var position = bike.Position + bike.Velocity * (float)delay;
        var road = frame.Project(position);
        var reference = bike.TotalReferenceMeters +
            frame.UnwrapDelta(bike.Road.ReferenceMeters, road.ReferenceMeters);
        var distance = Math.Max(5, speed * p[0]);
        var targetRef = reference + distance;
        var target = frame.At(targetRef, Offset(plan, section, targetRef));
        var aim = target.Position - position;
        var course = Math.Atan2(bike.Velocity.Y, bike.Velocity.X);
        var error = BikeAngles.Wrap(Math.Atan2(aim.Y, aim.X) - course - .45 * delay * bike.AngularVelocity);
        double curvature = 2 * Math.Sin(error) / Math.Max(4, aim.Length());
        var yaw = Math.Clamp(speed * curvature, -1.4, 1.4);
        var pathYaw = target.Road.IsTurn ? speed / target.Road.RadiusMeters : 0;
        var beta = Math.Clamp(p[1] * speed * speed * curvature, -.22, .28);
        var headingTarget = course + delay * yaw + beta;
        var headingError = BikeAngles.Wrap(headingTarget - bike.Heading - delay * bike.AngularVelocity);
        var steer = Math.Clamp(1.3 * curvature + p[2] * (yaw - bike.AngularVelocity) +
            .24 * headingError + .035 * (pathYaw - bike.AngularVelocity), -.22, .22);
        var progress = (reference - section.Entry) / (section.Exit - section.Entry);
        if (progress is > -.14 and < .25)
            steer += p[3] * Math.Sin(Math.PI * (progress + .14) / .39);
        if (reference < section.Entry - 15)
            steer = 0; // straight and aligned at launch, with no lateral repositioning yet

        double gas;
        if (reference < section.Entry + p[4])
        {
            var targetSpeed = plan.TargetKph / 3.6 + p[18];
            gas = Math.Clamp(.30 + (targetSpeed - speed) * .7, 0, 1);
        }
        else if (reference < section.Entry + 22) gas = p[5];
        else if (reference < section.Exit - p[6]) gas = p[7];
        else if (reference < section.NextEntry - p[8]) gas = p[9];
        else gas = p[10];
        return Action(steer, gas);
    }
    private static double Offset(Plan plan, Section section, double reference)
    {
        var p = plan.Knobs;
        var knots = new[] { plan.StartMeters, section.Entry - 10,
            section.Entry + (section.Exit - section.Entry) * p[11],
            section.Exit - 7, section.Exit + 17, section.NextEntry - 7, section.Goal + 12 };
        var offsets = new[] { (double)plan.StartOffset, p[12], p[13], p[14], p[15], p[16], p[17] };
        for (int index = 1; index < knots.Length; index++)
        {
            if (reference > knots[index])
                continue;
            var u = Math.Clamp((reference - knots[index - 1]) / (knots[index] - knots[index - 1]), 0, 1);
            return offsets[index - 1] + (offsets[index] - offsets[index - 1]) * u * u * (3 - 2 * u);
        }
        return offsets[^1];
    }

    private static FreeRacingAction Action(double steering, double gas)
    {
        var limit = TwoContactBikeParameters.Default.MaximumSteeringRadians;
        return new((float)Math.Clamp(steering, -limit, limit), (float)Math.Clamp(gas, 0, 1));
    }
}
