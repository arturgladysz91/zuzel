using System.Diagnostics;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;

namespace Cyclic172;

// The frozen tracker reads this clock for diagnostics only.
internal static class Study172
{
    private static readonly Process Process = Process.GetCurrentProcess();
    public static double Cpu => Process.TotalProcessorTime.TotalSeconds;
}

internal sealed record Event173(string Kind, object Data);

internal static class Trace173
{
    public static readonly List<Event173> Events = [];
    public static double RootTime;
    public static bool Enabled = true;
    public static Action Guard = () => { };
    private static int calls;

    public static void Begin(double time)
    {
        Guard(); RootTime = time; Events.Clear();
    }

    public static void Event(string kind, object data)
    {
        if (Enabled) Events.Add(new(kind, data));
    }

    public static object State(FreeRacingPoint p) => new {
        p.Time, Reference = p.Rider.TotalReferenceMeters, p.Rider.Speed,
        p.Rider.Position, p.Rider.Velocity, p.Rider.Heading,
        Beta = p.Rider.SlideRadians, Yaw = p.Rider.AngularVelocity,
        p.Omega, p.Steering, ActiveDrive = p.Rider.Drive,
        p.Inner, p.Outer, p.Rider.Road.OffsetMeters, p.Rider.Road.Segment,
    };

    public static object Summary(FreeRacingPrediction prediction)
    {
        var points = prediction.Points;
        double minSweep = double.PositiveInfinity;
        var first = new double?[4];
        double[] levels = [.5, .55, .60, .65];
        for (int i = 1; i < points.Length; i++)
        {
            var a = points[i - 1]; var b = points[i];
            double sweep = Clearance153.SweepLowerBound(a.Rider, b.Rider, b.Time - a.Time,
                (a.Inner, a.Outer), (b.Inner, b.Outer));
            minSweep = Math.Min(minSweep, sweep);
            for (int j = 0; j < levels.Length; j++)
                if (first[j] is null && (Math.Abs(b.Rider.SlideRadians) > Math.PI / 4 || sweep < levels[j]))
                    first[j] = b.Time;
        }
        object? At(double seconds)
        {
            var point = points.FirstOrDefault(p => Math.Abs(p.Time - RootTime - seconds) < 1e-8);
            return point is null ? null : State(point);
        }
        var end = points[^1]; var previous = points[^2];
        return new { Start = State(points[0]), End = State(end), At024 = At(.24), At044 = At(.44), At084 = At(.84),
            BetaDot = BikeAngles.Wrap(end.Rider.SlideRadians - previous.Rider.SlideRadians) / (end.Time - previous.Time),
            MinimumPose = points.Min(p => Math.Min(p.Inner, p.Outer)), MinimumSweep = minSweep,
            MaximumBeta = points.Max(p => Math.Abs(p.Rider.SlideRadians)),
            FirstViolation050 = first[0], FirstViolation055 = first[1], FirstViolation060 = first[2], FirstViolation065 = first[3],
            prediction.ForceEvaluations };
    }

    public static void Prediction(string layer, FreeRacingInput input, double horizon,
        FreeRacingAction? candidate, FreeRacingPrediction result)
    {
        if ((++calls & 31) == 0) Guard();
        if (Enabled) Event("prediction", new { Layer = layer, input.Time, Horizon = horizon, candidate,
            Reconstruction = true, Summary = Summary(result) });
    }
}
