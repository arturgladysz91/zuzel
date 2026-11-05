using CoreSim.PhysicalSpace;
using LearnedCorner166;
using SharedWorldSpeedway;
using WholeCorner171;

namespace Cyclic172;

internal sealed class CyclicReference172(Teacher a, Teacher b, Weights weights, RiderSpec spec, TrackFrame frame)
{
    public BendMap Map { get; } = new(frame);
    private ProfilePoint Learned(double s) => Teacher.Blend(a.At(s), b.At(s), weights.Blend);
    private double FirstEntry => Map.At(0, 0).Entry;
    private double FirstExit => Map.At(0, 0).Exit;

    public BendVisit Follow(double s)
    {
        if (s < FirstEntry) return Map.At(0, 0);
        var where = Map.Locate(s);
        if (where.Current is { } active) return active;
        var next = where.Next;
        return next.Index == 0 ? Map.At(next.Lap - 1, Map.Count - 1) : Map.At(next.Lap, next.Index - 1);
    }

    public ProfilePoint Target(double s, Func<double, ProfilePoint> launchTarget)
    {
        if (spec.SpeedMetersPerSecond == 0 && s <= FirstExit)
            return launchTarget(s);
        return Periodic(s);
    }

    public double FlyingEntrySpeed => Learned(FirstEntry).Speed * weights.TerminalSpeedScale;

    public ProfilePoint Stability(double s)
    {
        if (s <= FirstExit)
            return Learned(s) with { Offset = Learned(s).Offset + weights.Margin };
        return Periodic(s);
    }

    public ProfilePoint Periodic(double s)
    {
        var location = Map.Locate(s);
        if (location.Current is { } bend)
        {
            double fraction = (s - bend.Entry) / bend.Length;
            var point = Learned(FirstEntry + fraction * (FirstExit - FirstEntry));
            return point with { S = s, Offset = point.Offset + weights.Margin,
                Speed = point.Speed * weights.TerminalSpeedScale };
        }
        var next = location.Next;
        var previous = next.Index == 0 ? Map.At(next.Lap - 1, Map.Count - 1) : Map.At(next.Lap, next.Index - 1);
        double after = s - previous.Exit;
        double toNext = next.Entry - s;
        double length = next.Entry - previous.Exit;
        var end = Learned(FirstExit);
        var upcoming = Learned(FirstEntry - toNext);
        double phase = Math.Clamp(after / length, 0, 1);
        double mix = Smooth(after / (length * 52 / 62));
        double settle = 1 - Smooth(after / 10);
        double prepare = Smooth(1 - toNext / weights.PrepareMeters);
        double scale = previous.Entry == FirstEntry ? weights.SpeedScale : weights.TerminalSpeedScale;
        double speed = end.Speed * scale * (1 - phase) + FlyingEntrySpeed * phase +
            weights.StraightBoost * Math.Sin(Math.PI * phase);
        double drive = .22 + .16 * weights.StraightBoost * Math.Cos(Math.PI * phase);
        // Join the held exit drive continuously, while the physical queue remains irrevocable.
        drive = end.Drive + (drive - end.Drive) * Smooth(after / 3);
        return new(s, end.Offset * (1 - mix) + upcoming.Offset * mix + weights.Margin,
            end.Course * settle + upcoming.Course * prepare,
            end.Beta * settle + upcoming.Beta * prepare,
            end.Yaw * settle + upcoming.Yaw * prepare, speed,
            end.Steer * settle + upcoming.Steer * prepare,
            drive * (1 - prepare) + upcoming.Drive * prepare);
    }

    private static double Smooth(double x)
    {
        double u = Math.Clamp(x, 0, 1);
        return u * u * (3 - 2 * u);
    }
}

