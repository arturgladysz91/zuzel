using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace Cyclic172;

internal sealed record Line177(string Id, double Mix, double Entry, double Apex, double Exit, double ApexFraction);
internal sealed record Knot177(double S, double Offset, Vector2 XY, double Course, double K, double DK, double Beta,
    double Speed, double Limit, double FrontFy, double RearFy, double RearFx, double Moment, double Inner, double Outer);
internal sealed record Arc177(Line177 Line, BendVisit Bend, Knot177[] Points, double Distance, double Seconds,
    double Pose, double Sweep, double MaxK, bool Geometry, bool InitialReachable)
{
    public Knot177 At(double s)
    {
        int i = Math.Clamp((int)((s - Points[0].S) / LinePlanner177.Step), 0, Points.Length - 2);
        var a = Points[i];
        var b = Points[i + 1];
        double u = Math.Clamp((s - a.S) / (b.S - a.S), 0, 1);
        double Blend(double x, double y) => x + u * (y - x);
        return a with
        {
            S = s, Offset = Blend(a.Offset, b.Offset), Course = a.Course + u * BikeAngles.Wrap(b.Course - a.Course),
            K = Blend(a.K, b.K), Beta = Blend(a.Beta, b.Beta), Speed = Blend(a.Speed, b.Speed)
        };
    }
}

internal sealed class LinePlanner177(TrackFrame frame, TrackStateSnapshot surface, WorldParameters parameters,
    RiderSpec spec, float mass, float inertia, Nominal[][] profiles)
{
    internal const double Step = .5;
    internal const double ExitMeters = 20;
    internal static readonly Line177[] Family =
    [
        new("L0", 0, 0, 0, 0, .50),
        new("L1-late", .64, 5.1, 1.85, 4.2, .67),
        new("L2-smooth", .76, 4.75, 3.65, 4.75, .51),
        new("L3-open", .63, 4.2, 2.25, 5.0, .44),
        new("L4-wide", .83, 5.6, 4.1, 5.25, .54)
    ];
    private readonly RacingLineGeometry boundary = new(frame);
    private readonly Planner172 nominal = new(frame, surface, parameters, spec, mass, inertia, profiles);
    private readonly TwoContactBikeParameters bike = parameters.TwoContactBike ?? TwoContactBikeParameters.Default;
    private readonly RearWheelParameters wheel = parameters.RearWheel ?? RearWheelParameters.Default;
    private const double CapacityUse = .94;
    private const double CoastDecel = .75;
    private const double PreviousStraightOffset = 3.9;

    internal Arc177 Build(Line177 line, BendVisit bend, FreeRacingPoint activation, double shaft)
    {
        double start = activation.Rider.TotalReferenceMeters;
        double stop = bend.Exit + ExitMeters;
        int n = (int)Math.Ceiling((stop - start) / Step) + 1;
        double[] s = Enumerable.Range(0, n).Select(i => Math.Min(stop, start + i * Step)).ToArray();
        var observed = activation.Rider;
        bool launch = bend.Lap == 0 && bend.Index == 0;
        Nominal At(double x) => nominal.NominalAt(launch ? 0 : 1, x - bend.Entry);
        double Shape(double x)
        {
            // Quintic joins give matching offset, slope and second derivative.
            double apex = bend.Entry + line.ApexFraction * bend.Length;
            double entry = bend.Entry - 17;
            double exit = bend.Exit - 4;
            double d = x < entry ? Mix(PreviousStraightOffset, line.Entry, (x - (entry - 23)) / 23)
                : x < apex ? Mix(line.Entry, line.Apex, (x - entry) / (apex - entry))
                : x < exit ? Mix(line.Apex, line.Exit, (x - apex) / (exit - apex))
                : Mix(line.Exit, PreviousStraightOffset, (x - exit) / (stop - exit));
            return At(x).Offset * (1 - line.Mix) + d * line.Mix;
        }

        double referenceSpeed = frame.ReferenceSpeed(observed);
        double slope = referenceSpeed > .2 ? Vector2.Dot(observed.Velocity, observed.Road.Outward) / referenceSpeed : 0;
        double difference = observed.Road.OffsetMeters - Shape(start);
        double derivative = slope - (Shape(start + .1) - Shape(start - .1)) / .2;
        double join = Math.Clamp(observed.Speed * 1.45, 18, 35);
        double Offset(double x)
        {
            double t = Math.Min((x - start) / join, 1);
            double slopeBlend = t * Math.Pow(1 - t, 3) * (1 + 3 * t);
            return Shape(x) + difference * (1 - Smooth(t)) + derivative * join * slopeBlend;
        }
        Vector2 Pos(double x) => frame.At(x, Offset(x)).Position;
        var xy = s.Select(Pos).ToArray();
        double[] offsets = s.Select(Offset).ToArray();
        double[] ds = s.Select((_, i) => i == 0 ? 0d : Vector2.Distance(xy[i - 1], xy[i])).ToArray();
        double[] k = new double[n], course = new double[n], limits = new double[n], speed2 = new double[n];
        double[] beta = new double[n], dk = new double[n], inner = new double[n], outer = new double[n];
        var loads = new LoadBasedTyreBudget[n];
        double wheelbase = bike.FrontAxleFromComMeters + bike.RearAxleFromComMeters;
        double minPose = double.PositiveInfinity, minSweep = double.PositiveInfinity;
        for (int i = 0; i < n; i++)
        {
            var left = Pos(s[i] - Step);
            var right = Pos(s[i] + Step);
            var u = xy[i] - left;
            var v = right - xy[i];
            course[i] = Math.Atan2(right.Y - left.Y, right.X - left.X);
            k[i] = 2d * (u.X * v.Y - u.Y * v.X) / (u.Length() * v.Length() * (u + v).Length());
            double t = Smooth((s[i] - start) / join);
            beta[i] = (1 - t) * observed.SlideRadians + t * At(s[i]).Beta;
            var road = frame.RoadAt(s[i], offsets[i]);
            var usable = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type, frame.Track.Geometry);
            var track = surface.SampleSurface(road.Segment, Math.Clamp((float)offsets[i] / usable * 4, 0, 4));
            loads[i] = LoadBasedTyreGrip.Budget(mass, track, bike, parameters.LoadBasedTyres);
            var pose = observed with { Position = xy[i], Heading = (float)(course[i] + beta[i]), Road = road };
            (inner[i], outer[i]) = boundary.Clearance(pose);
            double gap = Math.Min(inner[i], outer[i]);
            minPose = Math.Min(minPose, gap);
            if (i > 0)
            {
                double rotation = Math.Abs(BikeAngles.Wrap(course[i] + beta[i] - course[i - 1] - beta[i - 1]));
                minSweep = Math.Min(minSweep, Math.Min(gap, Math.Min(inner[i - 1], outer[i - 1])) -
                    .5 * (ds[i] + SpeedwayBikeDimensions.Reference.BoundingRadiusMeters * rotation));
            }
        }
        for (int i = 0; i < n; i++)
        {
            int a = Math.Max(0, i - 1), b = Math.Min(n - 1, i + 1);
            dk[i] = (k[b] - k[a]) / (ds[b] + (b - a == 2 ? ds[b - 1] : 0));
            var (f, r, _) = Need(i, 1, 0);
            double rear = CapacityUse * loads[i].RearCapacityNewtons;
            // Keep some rear budget for drag and propulsion rather than setting Fx=0.
            limits[i] = Math.Min(CapacityUse * loads[i].FrontCapacityNewtons /
                Math.Max(.001, Math.Abs(f)), .93 * rear / Math.Max(.001, Math.Abs(r)));
        }
        // The exit remains connected to the rest of the lap. Look ahead to the
        // next tight part, using its geometry and distance for a roll-off bound.
        double nextTight = bend.NextEntry + .05 * bend.Length;
        double nextK = 1 / frame.RoadAt(nextTight, PreviousStraightOffset).RadiusMeters;
        double nextLimit = CapacityUse * loads[^1].TotalCapacityNewtons / mass / Math.Max(.001, nextK);
        limits[^1] = Math.Min(limits[^1], nextLimit + 2 * CoastDecel * (nextTight - stop));
        for (int i = n - 2; i >= 0; i--)
            limits[i] = Math.Min(limits[i], limits[i + 1] + 2 * CoastDecel * ds[i + 1]);

        speed2[0] = observed.Speed * observed.Speed;
        double time = 0;
        var points = new Knot177[n];
        for (int i = 0; i < n; i++)
        {
            double v = Math.Sqrt(speed2[i]);
            var (_, r, _) = Need(i, speed2[i], 0);
            double rear = CapacityUse * loads[i].RearCapacityNewtons;
            double fx = Math.Sqrt(Math.Max(0, rear * rear - r * r));
            double blend = 1 - Smooth((s[i] - start) / 16);
            double omega = blend * activation.Omega + (1 - blend) * v / wheel.WheelRadiusMeters;
            var engine = Engine500.Evaluate(omega, spec.Setup, 1);
            double crank = blend * shaft + (1 - blend) * omega * engine.OverallRatio;
            double torque = Engine500.Torque(crank * 30 / Math.PI) * engine.OverallRatio * engine.Efficiency;
            double resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)v);
            double ax = (Math.Min(fx, Math.Min(torque, engine.AvailableWheelTorqueNewtonMeters) / wheel.WheelRadiusMeters) - resistance) / mass;
            if (i + 1 < n)
            {
                speed2[i + 1] = Math.Max(1, Math.Min(speed2[i] + 2 * ax * ds[i + 1],
                    Math.Max(limits[i + 1], speed2[i] - 2 * CoastDecel * ds[i + 1])));
                ax = (speed2[i + 1] - speed2[i]) / (2 * ds[i + 1]);
                time += 2 * ds[i + 1] / (v + Math.Sqrt(speed2[i + 1]));
            }
            var (front, back, moment) = Need(i, speed2[i], ax);
            points[i] = new(s[i], offsets[i], xy[i], course[i], k[i], dk[i], beta[i], v, Math.Sqrt(limits[i]),
                front, back, Math.Max(0, mass * ax + resistance), moment, inner[i], outer[i]);
        }
        return new(line, bend, points, ds.Sum(), time, minPose, minSweep, k.Max(x => Math.Abs(x)),
            minSweep >= .5 && minPose >= .5 && k.All(x => Math.Abs(Math.Atan(x * wheelbase)) < bike.MaximumSteeringRadians),
            speed2[0] <= limits[0]);

        (double Front, double Rear, double Moment) Need(int i, double v2, double ax)
        {
            double lateral = mass * v2 * k[i];
            double moment = inertia * (v2 * dk[i] + ax * k[i]);
            double front = (lateral * bike.RearAxleFromComMeters + moment) / wheelbase;
            return (front, lateral - front, moment);
        }
    }

    private static double Mix(double a, double b, double t) => a + (b - a) * Smooth(t);
    private static double Smooth(double t)
    {
        double u = Math.Clamp(t, 0, 1);
        return u * u * u * (10 + u * (-15 + 6 * u));
    }
}
