using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

// Each bend has six geometry-anchored feedback pulses. Positive drive can recover
// any inherited clamp; negative drive leaves room for earlier preparation.
internal readonly record struct PacePulse210(double Drive, double Steer, double Line, double Alignment);
internal sealed record BendPace210(int Visit, PacePulse210[] Pulses);

internal sealed class ExitPace210
{
    internal const int PulseCount = 6;
    internal readonly record struct Gate(double Start, double Peak, double End);
    private readonly (Gate Gate, PacePulse210 Control, BendVisit Bend)[] active;

    internal ExitPace210(BendMap map, IEnumerable<BendPace210> edits)
    {
        active = edits.SelectMany(edit =>
        {
            var bend = map.At(edit.Visit / map.Count, edit.Visit % map.Count);
            if (edit.Pulses.Length != PulseCount)
                throw new ArgumentException("Each bend requires six geometry pulses.", nameof(edits));
            return Gates(bend).Zip(edit.Pulses, (gate, pulse) => (gate, pulse, bend));
        }).Where(x => x.pulse != default).ToArray();
    }

    internal static Gate[] Gates(BendVisit bend)
    {
        double entry = bend.Entry, exit = bend.Exit, apex = entry + bend.Length * .5;
        return [new(entry - 27, entry - 8, entry + 12), new(entry - 4, entry + 14, apex + 6),
            new(entry + 18, apex, exit - 14), new(apex, exit - 16, exit + 3),
            new(exit - 15, exit + 6, exit + 24), new(exit + 10, exit + 26, exit + 48)];
    }

    internal FreeRacingAction At(Start197 state, FreeRacingAction original, BendMap map)
    {
        var rider = state.State.Rider;
        double s = rider.TotalReferenceMeters;
        double steer = 0, drive = 0;
        foreach (var (gate, control, bend) in active)
        {
            if (s <= gate.Start || s >= gate.End) continue;
            double weight = s < gate.Peak
                ? ExitPolicy208.Gate((s - gate.Start) / (gate.Peak - gate.Start))
                : ExitPolicy208.Gate((gate.End - s) / (gate.End - gate.Peak));
            double alignment = 0;
            if (control.Alignment != 0)
            {
                var road = map.Locate(bend.Exit + 1).Road;
                var motion = ExitMotion208.From(rider, road, 1);
                alignment = Math.Clamp(motion.Course, -.35, .35) + .16 * rider.AngularVelocity;
            }
            double line = control.Line / (8 + .27 * rider.Speed);
            steer += weight * (control.Steer + control.Alignment * alignment - line);
            drive += weight * control.Drive;
        }
        if (steer == 0 && drive == 0) return original;
        double gas = original.Drive + drive * (drive > 0 ? 1 - original.Drive : original.Drive);
        var action = ExitPolicy208.Bound(original.SteeringRadians + steer, gas);
        ThrottleTrace209.Current?.Add("210 exit pace", original.Drive, action.Drive);
        return action;
    }
}
