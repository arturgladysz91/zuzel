using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Efficient147;
using Efficient152;
using Safety153;
using SharedWorldSpeedway;
using WholeCorner171;

namespace Cyclic172;

internal sealed record Continuation177(string Line, double? Seconds, double EndS, double Sweep, double Pose,
    double MaxBeta, double Course, double Yaw, double Speed, double Offset, double Wheel, double Crank,
    int Reviews, int Queries, string Reason);
internal sealed record Screen177(double Time, double Reference, BendVisit Bend, double CpuSeconds,
    string? Chosen, Continuation177[] Paths, Arc177[] Lines);

internal sealed class Controller177(CyclicController172 baseline, TrackFrame frame, TrackStateSnapshot surface,
    WorldParameters parameters, RiderSpec spec, float mass, float inertia, Nominal[][] profiles,
    string[] enabledLines)
{
    private const double Review = .20;
    private const double Reaction = .24;
    private const double Response = Review + Reaction;
    private const double ModelClearance = .60;
    private const double Horizon = 16;
    private const double MinimumModelGain = .025;
    private const double EarlyScreenMeters = 55;
    private const double LateScreenMeters = 4.8;
    private readonly BendMap map = new(frame);
    private readonly LinePlanner177 planner = new(frame, surface, parameters, spec, mass, inertia, profiles);
    private readonly Boundary152 boundary = new(frame);
    private readonly PredictionBoundary147 cache = new(frame);
    private BendVisit? screened;
    private bool screenedLate;
    private Arc177? live, original;
    private int forecasts, queries;
    public List<Screen177> Screens { get; } = [];

    public CyclicDecision172 Choose(FreeRacingInput input, CrankClutch144 shaft, bool active, BendVisit? atDevelopmentBranch = null)
    {
        long clock = Stopwatch.GetTimestamp();
        double cpu = Study172.Cpu;
        var old = baseline.Choose(input, shaft);
        if (!active) return old;
        forecasts = queries = 0;
        double s = input.Rider.TotalReferenceMeters;
        var where = map.Locate(s);
        var bend = atDevelopmentBranch ?? where.Current ?? where.Next;
        if (live is not null && s >= live.Bend.Exit + LinePlanner177.ExitMeters)
        {
            live = null;
            original = null;
        }
        bool late = s >= bend.Entry - LateScreenMeters;
        bool due = atDevelopmentBranch is not null || screened != bend || (late && !screenedLate);
        if (live is null && due && input.Rider.Speed >= 9 &&
            (atDevelopmentBranch is not null || s >= bend.Entry - EarlyScreenMeters) && s <= bend.Entry + 4)
        {
            var screen = Evaluate(input, shaft, baseline, old, bend, false);
            screened = bend;
            screenedLate = late;
            Screens.Add(screen);
            if (screen.Chosen is { } name)
            {
                original = screen.Lines[0];
                live = screen.Lines.Single(x => x.Line.Id == name);
            }
        }
        var action = live is { } route ? Joint(input, shaft, old.Action, original!, route) : old.Action;
        baseline.Applied174(action);
        return old with
        {
            Action = action, Transition = live is null ? old.Transition : live.Line.Id + (action == old.Action ? " · K4 recourse" : " · joint"),
            CpuSeconds = Study172.Cpu - cpu, DecisionMilliseconds = Stopwatch.GetElapsedTime(clock).TotalMilliseconds,
            Queries = old.Queries + queries, Forecasts = old.Forecasts + forecasts
        };
    }

    public Screen177 Evaluate(FreeRacingInput input, CrankClutch144 shaft, CyclicController172 k4,
        CyclicDecision172 old, BendVisit bend, bool allLines)
    {
        double began = Study172.Cpu;
        double atShaft = shaft.Omega;
        var activation = Predict(input, shaft, Reaction, null, (_, rpm) => atShaft = rpm).Points[^1];
        var lines = LinePlanner177.Family.Where(x => allLines || x.Id == "L0" || enabledLines.Contains(x.Id))
            .Select(x => planner.Build(x, bend, activation, atShaft)).ToArray();
        double goal = bend.Exit + LinePlanner177.ExitMeters;
        var reference = Continue(input, shaft, k4, old, goal, lines[0], null);
        var paths = new List<Continuation177> { reference };
        foreach (var line in lines.Skip(1))
        {
            if (line.Geometry && (allLines || reference.Seconds is not null))
                paths.Add(Continue(input, shaft, k4, old, goal, lines[0], line));
        }
        var chosen = paths.Skip(1).Where(p => reference.Seconds is { } baseSeconds &&
            p.Seconds is { } candidate && candidate + MinimumModelGain < baseSeconds &&
            p.Speed >= reference.Speed - .65 && Math.Abs(p.Course) < .18 && Math.Abs(p.Yaw) < .50 &&
            Math.Abs(p.Offset - reference.Offset) < 1.7 && p.Sweep >= ModelClearance).MinBy(x => x.Seconds)?.Line;
        return new(input.Time, input.Rider.TotalReferenceMeters, bend, Study172.Cpu - began, chosen, paths.ToArray(), lines);
    }

    private Continuation177 Continue(FreeRacingInput initial, CrankClutch144 crank, CyclicController172 source,
        CyclicDecision172 first, double goal, Arc177 l0, Arc177? line)
    {
        int startQueries = queries;
        var controller = source.Fork174();
        var input = initial;
        var shaft = crank.Copy();
        var old = first;
        double sweep = double.PositiveInfinity, pose = double.PositiveInfinity, beta = 0;
        int reviews = 0;
        var end = default(FreeRacingPoint);
        double? elapsed = null;
        string reason = "forecast horizon";
        while (input.Time < initial.Time + Horizon)
        {
            var action = line is not null ? Joint(input, shaft, old.Action, l0, line) : old.Action;
            controller.Applied174(action);
            double omega = shaft.Omega;
            var segment = Predict(input, shaft, Review, action, (_, value) => omega = value).Points;
            var clearance = Safety(segment);
            sweep = Math.Min(sweep, clearance.Sweep);
            pose = Math.Min(pose, clearance.Pose);
            beta = Math.Max(beta, clearance.Beta);
            end = segment[^1];
            shaft = new(crank.Parameters, omega);
            reviews++;
            if (!clearance.Legal) { reason = "forecast clearance or beta"; break; }
            int crossed = Array.FindIndex(segment, p => p.Rider.TotalReferenceMeters >= goal);
            if (crossed > 0)
            {
                var a = segment[crossed - 1];
                var b = segment[crossed];
                double u = (goal - a.Rider.TotalReferenceMeters) /
                    (b.Rider.TotalReferenceMeters - a.Rider.TotalReferenceMeters);
                elapsed = a.Time + u * (b.Time - a.Time) - initial.Time;
                reason = "common goal";
                break;
            }
            var commands = input.Pending.Append(new FreeRacingCommand(input.Time, input.Time + Reaction, action));
            var applied = commands.Where(c => c.ActivationAt <= end.Time + 1e-9).Select(c => c.Action)
                .Prepend(input.Active).Last();
            var pending = commands.Where(c => c.ActivationAt > end.Time + 1e-9).ToArray();
            input = new(end.Rider with { Drive = applied.Drive }, end.Time, end.Omega, end.Steering, applied, pending, end.Time);
            old = controller.Choose(input, shaft);
            forecasts += old.Forecasts;
            queries += old.Queries;
        }
        var body = end!.Rider;
        return new(line?.Line.Id ?? "K4", elapsed, body.TotalReferenceMeters, sweep, pose, beta,
            CourseError(body), body.AngularVelocity, body.Speed, body.Road.OffsetMeters, end.Omega, shaft.Omega,
            reviews, queries - startQueries, reason);
    }

    private FreeRacingAction Joint(FreeRacingInput input, CrankClutch144 shaft, FreeRacingAction old, Arc177 l0, Arc177 line)
    {
        var before = Predict(input, shaft, Response, old).Points;
        var expected = before[^1].Rider;
        double s = expected.TotalReferenceMeters;
        if (s >= line.Points[^1].S || s <= line.Points[0].S) return old;
        var target = line.At(s);
        var reference = l0.At(s);
        double join = Math.Clamp((s - line.Points[0].S) / 11, 0, 1) *
            Math.Clamp((line.Points[^1].S - s) / 8, 0, 1);
        double d = Math.Clamp(target.Offset - reference.Offset, -2, 2) * join;
        double course = BikeAngles.Wrap(target.Course - reference.Course) * join;
        double k = (target.K - reference.K) * join;
        double targetOffset = expected.Road.OffsetMeters + .50 * d;
        double targetCourse = Course(expected) + .48 * course;
        double targetYaw = expected.AngularVelocity + .52 * k * expected.Speed;
        double targetSpeed = expected.Speed + Math.Clamp(.75 * (target.Speed - reference.Speed) +
            .24 * (target.Speed - expected.Speed), -.52, .90) * join;
        double targetBeta = expected.SlideRadians + .14 * (target.Beta - expected.SlideRadians);
        var selected = old;
        double best = Safety(before).Legal ? Cost(before[^1], old) : double.PositiveInfinity;
        float centre = (float)(old.SteeringRadians - .010 * d + .21 * course + .34 * k);
        float intended = (float)Math.Clamp(old.Drive + Math.Clamp(.20 * (targetSpeed - expected.Speed), -.10, .11), 0, 1);
        float peak = Math.Min(1, old.Drive + .12f);
        foreach (float drive in new[] { old.Drive, intended, peak, Math.Max(0, old.Drive - .10f) }.Distinct())
        foreach (float variation in new[] { -.017f, -.008f, 0, .008f, .017f })
        {
            float limit = TwoContactBikeParameters.Default.MaximumSteeringRadians;
            var action = new FreeRacingAction(Math.Clamp(centre + variation * (float)join,
                Math.Max(-limit, old.SteeringRadians - .022f), Math.Min(limit, old.SteeringRadians + .022f)), drive);
            if (action == old) continue;
            var shortPath = Predict(input, shaft, Response, action).Points;
            var end = shortPath[^1];
            if (!Safety(shortPath).Legal || Math.Abs(end.Rider.SlideRadians - targetBeta) > .115 ||
                Math.Abs(end.Rider.SlideRadians) > Math.Max(.26, Math.Abs(expected.SlideRadians) + .035) ||
                Math.Abs(end.Rider.AngularVelocity - targetYaw) > .37 ||
                Math.Abs(CourseError(end.Rider)) > Math.Max(.19, Math.Abs(CourseError(expected)) + .038)) continue;
            double score = Cost(end, action);
            if (score >= best) continue;
            best = score;
            selected = action;
        }
        return selected;

        double Cost(FreeRacingPoint p, FreeRacingAction command)
        {
            var r = p.Rider;
            double slip = Math.Max(0, p.Omega * .33 / Math.Max(3, r.Speed) - 1);
            return .24 * Sq(r.Road.OffsetMeters - targetOffset) + 4.3 * Sq(BikeAngles.Wrap(Course(r) - targetCourse)) +
                .23 * Sq(r.AngularVelocity - targetYaw) + 1.75 * Sq(r.SlideRadians - targetBeta) +
                .11 * Sq(r.Speed - targetSpeed) + .11 * Sq(Math.Max(0, slip - .32)) +
                .5 * Sq(command.SteeringRadians - old.SteeringRadians);
        }
    }

    private FreeRacingPrediction Predict(FreeRacingInput input, CrankClutch144 shaft,
        double seconds, FreeRacingAction? action, Action<double, double>? observe = null)
    {
        var result = PredictorA153.Predict(input, spec, frame, surface, parameters, mass, inertia, seconds,
            candidate: action, powertrain: shaft, observeCrank: observe, boundaryCache: cache,
            fastBoundary: boundary, exactInitialWheel: true);
        forecasts++;
        queries += result.ForceEvaluations;
        return result;
    }

    private static (bool Legal, double Sweep, double Pose, double Beta) Safety(FreeRacingPoint[] path)
    {
        double sweep = double.PositiveInfinity, pose = double.PositiveInfinity, beta = 0;
        for (int i = 0; i < path.Length; i++)
        {
            var b = path[i];
            pose = Math.Min(pose, Math.Min(b.Inner, b.Outer));
            beta = Math.Max(beta, Math.Abs(b.Rider.SlideRadians));
            if (i == 0) continue;
            var a = path[i - 1];
            sweep = Math.Min(sweep, Clearance153.SweepLowerBound(a.Rider, b.Rider, b.Time - a.Time,
                (a.Inner, a.Outer), (b.Inner, b.Outer)));
        }
        return (pose >= ModelClearance && sweep >= ModelClearance && beta <= Math.PI / 4, sweep, pose, beta);
    }

    private static double Course(RiderView r) => Math.Atan2(r.Velocity.Y, r.Velocity.X);
    private static double CourseError(RiderView r) => BikeAngles.Wrap(Course(r) - r.Road.Heading);
    private static double Sq(double x) => x * x;
}
