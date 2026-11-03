using System.Numerics;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;

namespace FastCorner165;

internal sealed record Mark(double Time, double Reference, double Distance, double Kph, double X, double Y);

internal sealed record Section(double Entry, double Exit, double NextEntry, double Goal)
{
    public static Section First(TrackFrame frame) => new(frame.SegmentStartMeters(1),
        frame.SegmentStartMeters(4), frame.SegmentStartMeters(5), frame.SegmentStartMeters(5) + 12);

    private static RiderView Rider(StartSample144 sample) => sample.Input.Rider;

    public static Mark? Cross(StartSample144[] samples, double reference)
    {
        for (int index = 1; index < samples.Length; index++)
        {
            var before = samples[index - 1];
            var after = samples[index];
            var a = Rider(before);
            var b = Rider(after);
            if (a.TotalReferenceMeters >= reference || b.TotalReferenceMeters < reference)
                continue;
            var u = (reference - a.TotalReferenceMeters) / (b.TotalReferenceMeters - a.TotalReferenceMeters);
            double Mix(double x, double y) => x + u * (y - x);
            return new(Mix(before.Input.Time, after.Input.Time), reference,
                Mix(a.DistanceMeters, b.DistanceMeters), Mix(a.Speed, b.Speed) * 3.6,
                Mix(a.Position.X, b.Position.X), Mix(a.Position.Y, b.Position.Y));
        }
        return null;
    }

    public object Metrics(StartSample144[] samples)
    {
        var entry = Cross(samples, Entry);
        var exit = Cross(samples, Exit);
        var goal = Cross(samples, Goal);
        var bend = samples.Where(s => Rider(s).TotalReferenceMeters >= Entry &&
            Rider(s).TotalReferenceMeters <= Exit).ToArray();
        var minimum = exit is not null && entry is not null
            ? bend.Select(s => (double)Rider(s).Speed * 3.6).Append(entry.Kph).Append(exit.Kph).Min()
            : (double?)null;
        // Apex is the minimum distance of the actual COM from the inside line
        // in the interior 80% of the bend. Exclude straight-to-bend seam effects.
        var apex = bend.Where(s => Rider(s).TotalReferenceMeters >= Entry + (Exit - Entry) * .1 &&
            Rider(s).TotalReferenceMeters <= Exit - (Exit - Entry) * .1)
            .MinBy(s => Rider(s).Road.OffsetMeters);
        return new
        {
            Entry = entry, Exit = exit, After20 = Cross(samples, Exit + 20), After40 = Cross(samples, Exit + 40),
            After62 = Cross(samples, NextEntry), Goal = goal, MinBendKph = minimum,
            BendMaxAbsBeta = bend.Length == 0 ? (double?)null : bend.Max(s => Math.Abs(Rider(s).SlideRadians)) * 180 / Math.PI,
            Apex = apex is null || exit is null ? null : new Mark(apex.Input.Time, Rider(apex).TotalReferenceMeters,
                Rider(apex).DistanceMeters, Rider(apex).Speed * 3.6, Rider(apex).Position.X, Rider(apex).Position.Y),
            BendSeconds = exit?.Time - entry?.Time,
            SectionSeconds = goal?.Time - Cross(samples, Entry - 15)?.Time,
            Terminal = samples[^1].Input,
            TerminalCrankOmega = samples[^1].CrankOmega,
            TerminalPowertrain = samples[^1].Powertrain
        };
    }

    public static Audit Inspect(Run144 run, TrackFrame frame, bool independent)
    {
        var samples = run.Riders.Single().Samples;
        var boundary = independent ? new RacingLineGeometry(frame) : null;
        var before = samples[0];
        var previous = boundary?.Clearance(Rider(before)) ?? (before.Inner, before.Outer);
        double pose = Math.Min(previous.Item1, previous.Item2);
        double sweep = double.PositiveInfinity;
        double beta = 0;
        double mismatch = 0;
        StartSample144? failed = null;
        string? reason = null;
        foreach (var after in samples.Skip(1))
        {
            var next = boundary?.Clearance(Rider(after)) ?? (after.Inner, after.Outer);
            var dt = after.Input.Time - before.Input.Time;
            var gap = Math.Min(next.Item1, next.Item2);
            var bound = independent
                ? Clearance153.SweepLowerBound(Rider(before), Rider(after), dt, previous, next)
                : after.Certificate.GetValueOrDefault(double.NegativeInfinity);
            var slide = Math.Abs(Rider(after).SlideRadians);
            pose = Math.Min(pose, gap);
            sweep = Math.Min(sweep, bound);
            beta = Math.Max(beta, slide);
            mismatch = Math.Max(mismatch, Math.Abs(bound - after.Certificate.GetValueOrDefault(bound)));
            if (failed is null)
            {
                reason = gap < .5 ? "pose below 0.5 m" :
                    bound < .5 ? "swept reserve below 0.5 m" :
                    slide > Math.PI / 4 ? "beta exceeds 45 degrees" :
                    after.Forces is { } forces && !FreeLearningExperiment.SteeringStepIsPhysical(forces, (float)dt)
                        ? "steering actuator" :
                    Vector2.Dot(Rider(after).Velocity, Rider(after).Road.Tangent) < -.001 ? "reverse" : null;
                if (reason is not null)
                    failed = after;
            }
            previous = next;
            before = after;
        }
        if (pose < .5 && failed is null)
        {
            failed = samples[0];
            reason = "initial pose below 0.5 m";
        }
        if (run.Contacts.Length > 0 && failed is null)
            reason = "contact";
        return new(reason, failed?.Input.Time, failed?.Input.Rider.TotalReferenceMeters,
            pose, double.IsFinite(sweep) ? sweep : null, beta * 180 / Math.PI, mismatch);
    }
}

internal sealed record Audit(string? Reason, double? FirstFailureSeconds, double? FirstFailureReference,
    double PoseMeters, double? SweptMeters, double MaxAbsBetaDegrees, double SweptMismatchMeters);
