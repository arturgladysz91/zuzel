using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using LineThrottle156;
using SharedWorldSpeedway;

namespace LearnedCorner166;

internal sealed record ProfilePoint(double S, double Offset, double Course, double Beta, double Yaw,
    double Speed, double Steer, double Drive);

internal sealed record Teacher(string Source, ProfilePoint[] Knots)
{
    public ProfilePoint At(double s)
    {
        if (s <= Knots[0].S) return Knots[0];
        if (s >= Knots[^1].S) return Knots[^1];
        int upper = Array.FindIndex(Knots, k => k.S > s);
        return Blend(Knots[upper - 1], Knots[upper], (s - Knots[upper - 1].S) /
            (Knots[upper].S - Knots[upper - 1].S));
    }

    public static ProfilePoint Blend(ProfilePoint a, ProfilePoint b, double u)
    {
        double Mix(double x, double y) => x + (y - x) * u;
        return new(Mix(a.S, b.S), Mix(a.Offset, b.Offset), Mix(a.Course, b.Course),
            Mix(a.Beta, b.Beta), Mix(a.Yaw, b.Yaw), Mix(a.Speed, b.Speed),
            Mix(a.Steer, b.Steer), Mix(a.Drive, b.Drive));
    }

    public static Run144 ReadDemonstration(string label)
    {
        var path = $"docs/experiments/fast-corner-feasibility/evidence/native/{label}.zip";
        using var zip = ZipFile.OpenRead(path);
        using var source = zip.GetEntry("native.json")!.Open();
        return JsonSerializer.Deserialize<Run144>(source, Study156.Json)!;
    }

    public static Teacher Fit(string label, double exit)
    {
        var samples = ReadDemonstration(label).Riders.Single().Samples;
        var valid = samples.Where(q => q.Input.Rider.TotalReferenceMeters <= exit + 4).ToArray();
        // Spatial low-pass fit of measured state and realised controls. No request
        // list, clock, decision index, or data from the demonstrator's next bend.
        double[] positions = [0, 4, 8, 12, 17, 21, 25, 29, 33, 36, 39, 43, 47, 52,
            57, 62, 67, 72, 77, 82, 87, 92, 97, 102, 107, 112, 117, 122, 127, exit];
        var knots = positions.Select(s =>
        {
            var window = valid.Where(x => Math.Abs(x.Input.Rider.TotalReferenceMeters - s) <= 2.4)
                .Where(x => x.Input.Rider.Speed > .2).ToArray();
            if (window.Length == 0)
                throw new InvalidOperationException("Demonstration does not cover a fit knot");
            double Weight(StartSample144 x) =>
                Math.Exp(-.5 * Math.Pow((x.Input.Rider.TotalReferenceMeters - s) / 1.15, 2)) *
                Math.Max(1, x.Input.Rider.Speed);
            var denominator = window.Sum(Weight);
            double Mean(Func<StartSample144, double> f) => window.Sum(x => Weight(x) * f(x)) / denominator;
            return new ProfilePoint(s, Mean(x => x.Input.Rider.Road.OffsetMeters),
                Mean(x => CourseError(x.Input.Rider)),
                Mean(x => x.Input.Rider.SlideRadians), Mean(x => x.Input.Rider.AngularVelocity),
                Mean(x => x.Input.Rider.Speed), Mean(x => x.Input.Steering), Mean(x => x.Input.Active.Drive));
        }).ToArray();
        return new(label, knots);
    }

    public static double CourseError(RiderView rider) => BikeAngles.Wrap(
        Math.Atan2(rider.Velocity.Y, rider.Velocity.X) - rider.Road.Heading);
}

/// <summary>Gain schedule for a local two-contact Jacobian. This is a control
/// design approximation, never an evaluator or a force applied to the bike.</summary>
internal sealed class LateralGains
{
    private readonly double[][] bank;
    private const int MinimumSpeed = 4;
    private const int MaximumSpeed = 32;

    public LateralGains()
    {
        bank = Enumerable.Range(MinimumSpeed, MaximumSpeed - MinimumSpeed + 1)
            .Select(speed => Design(speed)).ToArray();
    }

    public double[] At(double speed) => bank[Math.Clamp((int)Math.Round(speed), MinimumSpeed, MaximumSpeed) - MinimumSpeed];

    private static double[] Design(double speed)
    {
        const int size = 4;
        const double mass = 147, inertia = 52.72097, frontArm = .70, rearArm = .60;
        var scale = Math.Sqrt(Math.Pow(.12 * speed, 2) + .4 * .4);
        var front = 854.9257 / scale * .72;
        var rear = 997.4133 / scale * .74;
        var cross = -frontArm * front + rearArm * rear;
        double[,] continuous = {
            { 0, -speed, 0, 0 },
            { 0, -(front + rear) / mass, (front + rear) / mass, cross / (mass * speed) },
            { 0, 0, 0, 1 },
            { 0, cross * speed / inertia, -cross * speed / inertia,
                -(frontArm * frontArm * front + rearArm * rearArm * rear) / inertia }
        };
        double[] forcing = [0, front / mass, 0, frontArm * front * speed / inertia];
        var a = new double[size, size];
        var b = new double[size];
        for (int i = 0; i < size; i++) a[i, i] = 1;
        // Zero-order hold over one 0.2 s decision interval.
        for (int step = 0; step < 20; step++)
        {
            var next = (double[,])a.Clone();
            var drive = (double[])b.Clone();
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                for (int k = 0; k < size; k++)
                    next[i, j] += .01 * continuous[i, k] * a[k, j];
                drive[i] += .01 * (forcing[i] + Enumerable.Range(0, size).Sum(k => continuous[i, k] * b[k]));
            }
            a = next;
            b = drive;
        }

        double[] weights = [1, 9, 2, .15];
        var p = new double[size, size];
        var kGain = new double[size];
        for (int iteration = 0; iteration < 80; iteration++)
        {
            var pb = Enumerable.Range(0, size).Select(i => Enumerable.Range(0, size)
                .Sum(j => p[i, j] * b[j])).ToArray();
            var denom = 7 + Enumerable.Range(0, size).Sum(i => b[i] * pb[i]);
            var pa = new double[size, size];
            for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
            for (int k = 0; k < size; k++)
                pa[i, j] += p[i, k] * a[k, j];
            var btpa = Enumerable.Range(0, size).Select(i => Enumerable.Range(0, size)
                .Sum(j => b[j] * pa[j, i])).ToArray();
            var next = new double[size, size];
            for (int i = 0; i < size; i++)
            {
                kGain[i] = btpa[i] / denom;
                for (int j = 0; j < size; j++)
                    next[i, j] = Enumerable.Range(0, size).Sum(k => a[k, i] * pa[k, j])
                        - btpa[i] * btpa[j] / denom + (i == j ? weights[i] : 0);
            }
            p = next;
        }
        return kGain;
    }
}
