
using System.Diagnostics;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static Trial39 VerifyTrial39(Lab38 lab, Geometry215 g, Trial39 attempt, SpatialBike36 envelope)
    {
        var replay = ReplayTape38(lab, g, attempt.Seed, attempt.Commands, attempt.Flight.Final.State.Time,
            envelope, exact: true, endDistance: lab.Bend.NextEntry - lab.Bend.Entry + 4);
        return attempt with { Flight = replay with
            { Stop = replay.Stop == "verified interval" ? attempt.Flight.Stop : replay.Stop } };
    }

    private static Score39 Measure39(Lab38 lab, Geometry215 g, Trial39 trace)
    {
        var f = trace.Flight;
        double exit = lab.Bend.Length, next = lab.Bend.NextEntry - lab.Bend.Entry;
        Step215? entry = At(0), e = At(exit), eight = At(exit + 8), twenty = At(exit + 20), n = At(next);
        double? lateSpeed = null;
        bool late = false;
        if (eight is not null)
        {
            double at = eight.After.Time + 2.10;
            var latest = f.Steps.FirstOrDefault(x => x.After.Time >= at - 1e-10);
            if (latest is not null)
            {
                var tail = f.Steps.Where(r => r.After.Time > at - .25 && r.Before.Time < at).ToArray();
                lateSpeed = Speed38(latest.After, g);
                late = tail.Length > 0 && tail.All(r => Balanced(r.After)) &&
                    lateSpeed >= Speed38(eight.After, g) + 1 && lateSpeed >= Speed38(tail[0].Before, g);
            }
        }
        double loaded = 0, powered25 = 0, powered50 = 0, powered85 = 0, full = 0, currentFull = 0, longest = 0,
            straight = 0, engine = 0, loss = 0, residual = 0, peakResidual = 0, peakForce = 0, peakPower = 0;
        double min = double.PositiveInfinity;
        foreach (var (s, d) in f.Steps.Zip(f.Distances))
        {
            var q = s.Forces;
            double dt = Duration215(s);
            float thr = s.Before.Command.Bike.Drive;
            if (d >= 0 && d <= exit)
            {
                min = Math.Min(min, Speed38(s.After, g));
                bool slide = Slide38(s);
                if (slide) loaded += dt;
                if (slide && q.RearSlip >= PoweredSlip38)
                {
                    if (thr >= .25f) powered25 += dt;
                    if (thr >= .50f) powered50 += dt;
                    if (thr >= .85f) powered85 += dt;
                    if (thr >= .999f)
                    {
                        full += dt;
                        currentFull += dt;
                        longest = Math.Max(longest, currentFull);
                    }
                    else currentFull = 0;
                }
                else currentFull = 0;
            }
            else if (d > exit && d < next && thr >= .999f) straight += dt;
            engine += q.EnginePower * dt;
            loss += q.Loss * dt;
            residual += s.EnergyResidual;
            peakResidual = Math.Max(peakResidual, Math.Abs(residual));
            peakForce = Math.Max(peakForce, q.Rider.Norm + Math.Abs(q.LeftFoot?.MuscleForce ?? 0));
            peakPower = Math.Max(peakPower, q.LeftFoot?.AbsoluteMusclePower ?? Math.Abs(s.ActuatorPower));
        }
        bool plus20 = entry is not null && e is not null && eight is not null && twenty is not null &&
            Balanced(eight.After) && Balanced(twenty.After) && Speed38(twenty.After, g) >= Speed38(e.After, g) + .5;
        var pose20 = twenty?.After;
        bool nextReach = n is not null && f.Final.Distance >= next + 3.95 &&
            n.Forces.Front.Normal > 80 && Math.Abs(SlipAngle38(n.After)) < .34 && Math.Abs(n.After.Pose.Roll) < .72;
        return new(trace.Plan.Id, f.Stop, f.Steps.Length, trace.ForecastSteps, (double)trace.DecisionTicks / Stopwatch.Frequency,
            trace.Decisions, f.Final.Distance, entry is not null && e is not null, plus20, late, nextReach, SpeedAt(entry),
            entry is null || e is null ? null : e.After.Time - entry.After.Time, double.IsFinite(min) ? min : null, SpeedAt(e),
            SpeedAt(twenty), SpeedAt(At(exit + 40)), SpeedAt(At(exit + 60)), SpeedAt(n), lateSpeed,
            pose20?.Pose.Roll, pose20 is null ? null : lab.Course.Mark(pose20, g).AngularVelocity,
            f.Boundary, f.Ground, loaded, powered25, powered50, powered85, full, longest, straight, engine, loss, peakResidual,
            peakForce, peakPower);

        Step215? At(double d)
        {
            int index = Array.FindIndex(f.Distances, x => x >= d);
            return index < 0 ? null : f.Steps[index];
        }
        bool Balanced(State215 s) => Math.Abs(s.Pose.Roll) <= .22 && Math.Abs(lab.Course.Mark(s, g).AngularVelocity) <= .28;
        double? SpeedAt(Step215? p) => p is null ? null : Speed38(p.After, g);
    }

    private static int Rank39(Score39 x, Score39 y)
    {
        static int Stage(Score39 r) => r.Plus20Gate && r.LaterGate && r.NextReachable && r.LongestFullSlide >= 1.0724 ? 6 :
            r.Plus20Gate && r.LaterGate && r.NextReachable && r.LongestFullSlide >= .15 ? 5 :
            r.Plus20Gate && r.LaterGate && r.LongestFullSlide >= .15 ? 4 :
            r.LaterGate && r.LongestFullSlide >= .15 ? 3 :
            r.Plus20Gate && r.LongestFullSlide >= .15 ? 2 : r.Bend ? 1 : 0;
        int order = Stage(y).CompareTo(Stage(x));
        if (order == 0 && Stage(x) > 2) order = Nullable.Compare(x.BendSeconds, y.BendSeconds);
        if (order == 0) order = y.LastMeters.CompareTo(x.LastMeters);
        if (order == 0) order = y.LongestFullSlide.CompareTo(x.LongestFullSlide);
        return order == 0 ? x.Id.CompareTo(y.Id) : order;
    }

    private static object[] ProbeFrontier39(Lab38 lab, Witnesses39 cases)
    {
        var result = new List<object>();
        var source = cases.Attempts.Single(x => x.Plan.Id == 57);
        var g = cases.Reference.Geometry;
        foreach (double station in new[] { 74d, 83, 91.5, 96, 101, source.Trail.Final.Distance })
        {
            int i = Array.FindIndex(source.Trail.Distances, x => x >= station);
            var seed = new Checkpoint38(source.Trail.Steps[i].After, source.Trail.Distances[i]);
            var prefix = source.Commands.Where(c => c.Requested < seed.State.Time - 1e-10).ToArray();
            foreach (var drive in new[] { 0f, .50f, 1f })
            {
                var s = seed;
                var q = prefix;
                var model = lab.Engine(g);
                var trace = new List<Step215>();
                double start = s.State.Time, margin = 100;
                string status = "verified interval";
                var fixedLane = Lane39(lab, g, s, Recovery39.Room);
                while (s.State.Time < start + .42 - 1e-9 && status == "verified interval")
                {
                    var chosen = ChooseRecovery39(lab, g, model, s, q, drive, fixedLane, Recovery39.Room, 2);
                    if (chosen.Command is not { } c) { status = "recovery candidate set exhausted"; break; }
                    q = Submit38(s, q, c);
                    var f = Window38(lab, g, model, s, q, Math.Min(Cadence38, start + .42 - s.State.Time));
                    s = f.Final;
                    trace.AddRange(f.Steps);
                    status = f.Stop;
                    margin = Math.Min(margin, f.Boundary);
                }
                result.Add(new { Station = station, Drive = drive, Seed = seed, InitialFIFO = prefix,
                    Outcome = status, AdvanceMeters = s.Distance - seed.Distance, CommandHistory = q,
                    Receipt = Measure38(trace.ToArray(), g, status), MinimumBoundary = margin, StartRoll = seed.State.Pose.Roll,
                    StartRollRate = RollRate36(seed.State, g.Motion(seed.State).BodyRate),
                    FinalRoll = s.State.Pose.Roll, FinalRollRate = RollRate36(s.State, g.Motion(s.State).BodyRate),
                    Yaw = lab.Course.Mark(s.State, g).AngularVelocity });
            }
        }
        return result.ToArray();
    }
}
