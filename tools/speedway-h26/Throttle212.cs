using System.Numerics;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Steering212(double LookaheadSeconds, double PursuitGain, double StraightOffsetMeters,
        double ApexOffsetMeters, double EntryBlendMeters, double SlipCompensation, double BendDrive,
        double EntryDrive, double ExitDrive, double Posture);
    private sealed record Search212(int Id, string Arm, string Line, string Profile, Steering212 Policy, Race210 Race);
    private sealed record Replay212(Search212 Trial, double[] FlyingLapSeconds, string Trace);

    private const double Wheelbase212 = 1.30;
    private static readonly string[] Arms212 = ["constant-100", "constant-90", "constant-80", "straight-wot",
        "straight-transition", "slide-wot", "joint", "variable"];
    private static readonly (string Name, double Apex, double Straight)[] Lines212 =
        [("cutback", 6.0, 10.8), ("middle", 9.3, 10.8), ("outside", 12.1, 10.8)];
    private static readonly RacingRestriction206[] Gates212 =
        [RacingRestriction206.None, RacingRestriction206.StraightPosition, RacingRestriction206.EntryPosition,
         RacingRestriction206.StraightCourse, RacingRestriction206.StraightYaw, RacingRestriction206.Correction,
         RacingRestriction206.Turn, RacingRestriction206.Slip, RacingRestriction206.All];

    private static void Throttle212(string root, string output)
    {
        string candidatePath = Path.Combine(root, Evidence211, "frozen-candidate.json");
        var baseline = Law211(SourceFor211(root), Read211(candidatePath));
        var standing = RootFor210(root, "C");
        var origin = Prefix210(standing, baseline, Frame.LapMeters);
        var (_, spec) = CourseFixture208(root, "C");
        var models = Gates212.ToDictionary(gate => gate, gate => new Predictor198(Frame, Surface, Physics,
            Settings202("H28-F"), Timing, spec, Mass, Inertia, new RacingProfile206(Map199, gate)));
        var reference = Follow209("C", standing.Model, standing.State, standing.Goal, baseline, true);
        if (!reference.Complete) throw new InvalidOperationException("The frozen #211 strict control failed.");
        using var receipt = File.OpenRead(Path.Combine(root, Evidence211, "official", "admission.json"));
        var admission = System.Text.Json.JsonSerializer.Deserialize<Preflight211>(receipt, Json)
            ?? throw new InvalidDataException("The #211 admission receipt is empty.");
        var check = admission.Registered.Single(r => r.Fixture == "C");
        if (reference.Finish != check.Finish || reference.Sweep != check.Sweep ||
            reference.LastLegalS != check.LastLegalS || reference.Steps != check.Steps)
            throw new InvalidOperationException("The strict #211 control disagrees with its admission receipt.");
        Save(Path.Combine(output, "baseline.json.gz"), reference, true);
        Save(Path.Combine(output, "protocol.json"), new
        {
            Candidate211Sha256 = Hash210(candidatePath), Start = origin.State, Frame.LapMeters, origin.Goal,
            Mass, Inertia, Physics.TwoContactBike, Engine = Engine500.Curve, Lines212,
            Bends = Enumerable.Range(0, 6).Select(i => Map199.At(i / 2, i % 2)).ToArray(),
            Surface = Surface.SampleSurface(0, 2)
        });
        var history = new List<Search212>();
        var winners = new List<Replay212>();
        foreach (string arm in Arms212)
        {
            foreach (var line in Lines212)
            {
                var strict = Search212For(arm, line, RacingRestriction206.None);
                var physical = Search212For(arm, line, RacingRestriction206.All);
                foreach (var candidate in new[] { strict, physical }.DistinctBy(row => row.Policy))
                {
                    foreach (var gate in Gates212)
                    {
                        var trial = Trial212(candidate.Id, arm, line.Name, gate, candidate.Policy);
                        string filename = $"{arm}-{line.Name}-{candidate.Profile}-{gate}.json.gz";
                        var trace = Follow209("C", models[gate], origin.State, origin.Goal,
                            Control212(arm, candidate.Policy), true, posture: Rider212(arm, candidate.Policy));
                        if (trace.Stop != trial.Race.Stop || trace.LastLegalS != trial.Race.LastLegalS)
                            throw new InvalidOperationException($"The matched replay differs for {filename}.");
                        Save(Path.Combine(output, filename), trace, true);
                        var laps = LapTimes212(origin.State.State.Time, trace);
                        winners.Add(new(trial, laps, filename));
                        if (arm == "joint" && gate is RacingRestriction206.None or RacingRestriction206.All)
                        {
                            string pairedFile = $"{arm}-{line.Name}-{candidate.Profile}-{gate}-stock-posture.json.gz";
                            var stock = Follow209("C", models[gate], origin.State, origin.Goal,
                                Control212(arm, candidate.Policy), true);
                            Save(Path.Combine(output, pairedFile), stock, true);
                        }
                    }
                }
                Console.WriteLine($"{arm} {line.Name}: strict {strict.Race.Stop} {strict.Race.LastLegalS:F1}; " +
                    $"physical {physical.Race.Stop} {physical.Race.LastLegalS:F1}");
            }
        }
        Save(Path.Combine(output, "search.json.gz"), history, true);
        Save(Path.Combine(output, "replays.json"), winners);

        Search212 Search212For(string arm, (string Name, double Apex, double Straight) line, RacingRestriction206 gate)
        {
            var policies = new List<Steering212>();
            foreach (double look in new[] { .27, .44, .68, .90 })
            foreach (double gain in new[] { .7, 1.1, 1.5 })
            foreach (double slip in new[] { 0.0, .8 })
                policies.Add(new(look, gain, line.Straight, line.Apex, 8, slip, .26, .28, .72, .65));
            var best = Evaluate(policies.ToArray()).First();
            for (int pass = 0; pass < 3; pass++)
            {
                var policy = best.Policy;
                double d = Math.Pow(.55, pass);
                policies.Clear();
                foreach (int sign in new[] { -1, 1 })
                {
                    policies.Add(policy with { LookaheadSeconds = Math.Clamp(policy.LookaheadSeconds + sign * .15 * d, .15, 1.25) });
                    policies.Add(policy with { PursuitGain = Math.Clamp(policy.PursuitGain + sign * .35 * d, .3, 2.2) });
                    policies.Add(policy with { StraightOffsetMeters = Math.Clamp(policy.StraightOffsetMeters + sign * .50 * d, 9.8, 11.35) });
                    policies.Add(policy with { ApexOffsetMeters = policy.ApexOffsetMeters + sign * .65 * d });
                    policies.Add(policy with { EntryBlendMeters = Math.Clamp(policy.EntryBlendMeters + sign * 7 * d, 1, 30) });
                    if (arm.StartsWith("constant-", StringComparison.Ordinal)) continue;
                    policies.Add(policy with { BendDrive = Math.Clamp(policy.BendDrive + sign * .22 * d, .05, 1) });
                    policies.Add(policy with { EntryDrive = Math.Clamp(policy.EntryDrive + sign * .22 * d, .04, 1) });
                    policies.Add(policy with { ExitDrive = Math.Clamp(policy.ExitDrive + sign * .22 * d, .10, 1) });
                    if (arm == "joint")
                        policies.Add(policy with { Posture = Math.Clamp(policy.Posture + sign * .45 * d, 0, 1) });
                }
                best = Rank212(Evaluate(policies.ToArray()).Append(best)).First();
            }
            return best;

            Search212[] Evaluate(Steering212[] proposals)
            {
                int first = history.Count;
                var results = new Search212[proposals.Length];
                Parallel.For(0, proposals.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                    results[i] = Trial212(first + i, arm, line.Name, gate, proposals[i]));
                history.AddRange(results);
                return Rank212(results).ToArray();
            }
        }

        Search212 Trial212(int id, string arm, string line, RacingRestriction206 gate, Steering212 policy)
        {
            var cursor = models[gate].BeginFeedback207(origin.State, Control212(arm, policy), origin.Goal, .50,
                posture: Rider212(arm, policy));
            cursor.Advance(10000);
            var result = new Race210(origin.Fixture, cursor.Reached, cursor.Reached ? cursor.GoalTime : null,
                cursor.Outcome ?? "step cap", cursor.LegalSweep207, cursor.LastLegalS207, cursor.Steps,
                cursor.Snapshot, cursor.StrictFailure206, cursor.ContactFailure);
            return new(id, arm, line, gate.ToString(), policy, result);
        }
    }

    private static IOrderedEnumerable<Search212> Rank212(IEnumerable<Search212> candidates) => candidates
        .OrderByDescending(row => row.Race.Complete)
        .ThenByDescending(row => row.Race.Complete ? 0 : row.Race.LastLegalS)
        .ThenBy(row => row.Race.Finish ?? double.PositiveInfinity)
        .ThenByDescending(row => row.Race.Sweep).ThenBy(row => row.Id);

    private static Func<Start197, FreeRacingAction> Control212(string arm, Steering212 p) => start =>
    {
        var body = start.State.Rider;
        double s = body.TotalReferenceMeters;
        double speed = Math.Max(3, body.Speed);
        double lookahead = Math.Clamp(speed * p.LookaheadSeconds, 5, 32);
        double targetS = s + lookahead;
        var point = Frame.At(targetS, Offset212(targetS, p)).Position - body.Position;
        var course = body.Speed > 1 ? body.Velocity : body.Road.Tangent;
        double error = CoreSim.PhysicalSpace.BikeAngles.Wrap(Math.Atan2(point.Y, point.X) - Math.Atan2(course.Y, course.X));
        double yaw = 2 * p.PursuitGain * speed * Math.Sin(error) / Math.Max(4, point.Length());
        double beta = body.SlideRadians;
        yaw += p.SlipCompensation * 2.8 * Math.CopySign(Math.Max(0, Math.Abs(beta) - .24), beta);
        float steering = (float)Math.Clamp(Math.Atan(Wheelbase212 * yaw / speed), -.60, .60);
        double previewS = s + Math.Clamp(speed * .22, 3, 8);
        var (_, _, _, next, bend) = Map199.Locate(previewS);
        double entry = bend is null ? next.Entry - previewS : bend.Value.Entry - previewS;
        double progress = bend is null ? 0 : (previewS - bend.Value.Entry) / bend.Value.Length;
        double drive = arm switch
        {
            "constant-100" => 1,
            "constant-90" => .9,
            "constant-80" => .8,
            "straight-wot" => Map199.Locate(s + speed * .05).Current is null ? 1 :
                progress > .72 ? p.ExitDrive : p.BendDrive,
            "straight-transition" => bend is null && entry > 16 ? 1 :
                bend is not null && progress > .72 ? p.ExitDrive : p.BendDrive,
            "slide-wot" => bend is not null && progress < .70 || entry is >= 0 and <= 12 ? 1 :
                bend is not null ? p.ExitDrive : p.EntryDrive,
            _ => bend is null ? (entry > 18 ? p.ExitDrive : p.EntryDrive) :
                (progress > .68 ? p.ExitDrive : p.BendDrive)
        };
        return new(steering, (float)drive);
    };

    private static Func<Start197, PostureCommand212>? Rider212(string arm, Steering212 p)
    {
        if (arm != "joint") return null;
        return start =>
        {
            double s = start.State.Rider.TotalReferenceMeters;
            var (_, _, _, next, bend) = Map199.Locate(s);
            double phase = bend is {} active ? (s - active.Entry) / active.Length : 0;
            double entry = bend is null ? Math.Clamp(1 - (next.Entry - s) / 18, 0, 1) : 1;
            double forward = entry * Math.Clamp(1 - 2 * phase, -1, 1);
            return new(Math.Clamp(.8 - .5 * p.Posture * forward, 0, 1),
                p.Posture * HandlingContact.MaximumRiderForeAftMeters * forward);
        };
    }

    private static double Offset212(double s, Steering212 p)
    {
        var (_, _, _, next, bend) = Map199.Locate(s);
        if (bend is null)
        {
            if (next.Entry - s >= p.EntryBlendMeters) return p.StraightOffsetMeters;
            double preview = (s - next.Entry + p.EntryBlendMeters) / p.EntryBlendMeters;
            return p.StraightOffsetMeters + .10 * preview * preview;
        }
        double progress = (s - bend.Value.Entry) / bend.Value.Length;
        double apex = Math.Sin(Math.PI * progress);
        return p.StraightOffsetMeters + .10 * (1 - progress) +
            (p.ApexOffsetMeters - p.StraightOffsetMeters) * apex * apex;
    }

    private static double[] LapTimes212(double startTime, Run209 trace)
    {
        var laps = new List<double>();
        double last = startTime;
        for (int lap = 2; lap <= 3; lap++)
        {
            double s = lap * Frame.LapMeters;
            var cross = trace.Trace!.FirstOrDefault(tick => tick.After.S >= s);
            if (cross is null) break;
            double prior = cross.Before.State.Rider.TotalReferenceMeters;
            double time = cross.Before.State.Time + (cross.After.Time - cross.Before.State.Time) *
                (s - prior) / (cross.After.S - prior);
            laps.Add(time - last);
            last = time;
        }
        return laps.ToArray();
    }

    private sealed record Pulse212(double Begin, double End, double Steer, double Pace);
    private sealed record LapsTrial212(string Arm, string Profile, Pulse212 Pulse, Race210 Race);

    private static void Laps212(string root, string output)
    {
        var baseline = Law211(SourceFor211(root), Read211(Path.Combine(root, Evidence211, "frozen-candidate.json")));
        var straight = RootFor210(root, "C");
        var origin = Prefix210(straight, baseline, Frame.LapMeters);
        var (_, spec) = CourseFixture208(root, "C");
        var gates = Gates212.ToDictionary(gate => gate, gate => new Predictor198(Frame, Surface, Physics,
            Settings202("H28-F"), Timing, spec, Mass, Inertia, new RacingProfile206(Map199, gate)));
        var all = new List<LapsTrial212>();
        foreach (string arm in new[] { "straight-pulse", "bend-pulse" })
        {
            var begin = arm == "straight-pulse" ? new[] { -48d, -38, -27, -18 } : new[] { -4d, 4, 12, 18, 32, 51, 70 };
            var menu = (from s in begin
                        from length in new[] { 3d, 7, 12, 20 }
                        from steer in new[] { -.009, 0, .009 }
                        from pace in new[] { 0d, -.10, -.21 }
                        select new Pulse212(s, s + length, steer, pace)).ToArray();
            var scans = new LapsTrial212[menu.Length];
            Parallel.For(0, menu.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                scans[i] = Run(arm, menu[i], RacingRestriction206.None));
            var trials = scans.ToList();
            for (int round = 0; round < 2; round++)
            {
                double step = round == 0 ? 1 : .5;
                var refined = Rank(trials).Take(5).SelectMany(x => Near(x.Pulse, step))
                    .Except(trials.Select(x => x.Pulse)).ToArray();
                var local = new LapsTrial212[refined.Length];
                Parallel.For(0, refined.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
                    local[i] = Run(arm, refined[i], RacingRestriction206.None));
                trials.AddRange(local);
            }
            all.AddRange(trials);
            var selection = Rank(trials).First();
            int strict = trials.Count(x => x.Race.Complete);
            Console.WriteLine($"{arm}: {strict}/{trials.Count}; {selection.Race.Stop}, {selection.Race.LastLegalS:F2}");
            foreach (var gate in Gates212)
            {
                var trace = Follow209("C", gates[gate], origin.State, origin.Goal, Policy(selection.Pulse), true);
                Save(Path.Combine(output, $"{arm}-{gate}.json.gz"), trace, true);
                if (gate != RacingRestriction206.None) all.Add(Run(arm, selection.Pulse, gate));
            }
        }
        Save(Path.Combine(output, "pulse-search.json.gz"), all, true);

        static IOrderedEnumerable<LapsTrial212> Rank(IEnumerable<LapsTrial212> rows) =>
            rows.OrderByDescending(x => x.Race.Complete)
                .ThenBy(x => x.Race.Finish ?? double.PositiveInfinity)
                .ThenByDescending(x => x.Race.LastLegalS).ThenByDescending(x => x.Race.Sweep);

        static IEnumerable<Pulse212> Near(Pulse212 pulse, double scale)
        {
            foreach (int direction in new[] { -1, 1 })
            {
                double delta = direction * scale;
                yield return pulse with { Begin = pulse.Begin + 2 * delta, End = pulse.End + 2 * delta };
                yield return pulse with { End = pulse.End + delta };
                yield return pulse with { Steer = pulse.Steer + .003 * delta };
                yield return pulse with { Pace = Math.Clamp(pulse.Pace + .05 * delta, -.3, 0) };
            }
        }

        LapsTrial212 Run(string arm, Pulse212 pulse, RacingRestriction206 gate) =>
            new(arm, gate.ToString(), pulse, RaceFrom210(origin with { Model = gates[gate] }, Policy(pulse)));

        Func<Start197, FreeRacingAction> Policy(Pulse212 pulse) => start =>
        {
            double s = start.State.Rider.TotalReferenceMeters;
            var (_, _, _, next, bend) = Map199.Locate(s);
            var chosen = bend is {} current && s - current.Entry <= pulse.End + 14 ? current : next;
            int visit = chosen.Lap * Map199.Count + chosen.Index;
            var action = baseline(start);
            if (visit is < 2 or > 5) return action;
            double local = s - chosen.Entry;
            if (local < pulse.Begin - 18 || local > pulse.End + 14) return action;
            double window = ExitPolicy208.Gate((local - pulse.Begin + 7) / 7) *
                ExitPolicy208.Gate((pulse.End + 10 - local) / 10);
            double power = local >= pulse.Begin && local <= pulse.End ? 1 :
                action.Drive + pulse.Pace * (1 - window);
            return ExitPolicy208.Bound(action.SteeringRadians + pulse.Steer * window, power);
        };
    }

    private static void Slides212(string root, string output)
    {
        var baseline = Law211(SourceFor211(root), Read211(Path.Combine(root, Evidence211, "frozen-candidate.json")));
        var standing = RootFor210(root, "C");
        var (_, spec) = CourseFixture208(root, "C");
        foreach (int visit in new[] { 2, 3, 4, 5 })
        {
            var bend = Map199.At(visit / 2, visit % 2);
            foreach (int offset in new[] { -8, 5, 16, 30 })
            {
                var origin = Prefix210(standing, baseline, bend.Entry + offset);
                foreach (var gate in new[] { RacingRestriction206.None, RacingRestriction206.All })
                {
                    var model = new Predictor198(Frame, Surface, Physics, Settings202("H28-F"), Timing,
                        spec, Mass, Inertia, new RacingProfile206(Map199, gate));
                    foreach (float steer in new[] { -.035f, 0f, .035f })
                    {
                        FreeRacingAction Control(Start197 start)
                        {
                            var action = baseline(start);
                            return new(Math.Clamp(action.SteeringRadians + steer, -.60f, .60f), 1);
                        }
                        var trace = Follow209("C", model, origin.State, origin.Goal, Control, true, 1.45);
                        string tag = steer < 0 ? "less" : steer > 0 ? "more" : "stock";
                        Save(Path.Combine(output, $"slide-{visit}-{offset}-{gate}-{tag}.json.gz"), trace, true);
                    }
                    var original = Follow209("C", model, origin.State, origin.Goal, baseline, true, 1.45);
                    Save(Path.Combine(output, $"slide-{visit}-{offset}-{gate}-reference.json.gz"), original, true);
                    if (offset != 16 || gate != RacingRestriction206.None) continue;
                    foreach (int direction in new[] { -1, 1 })
                    {
                        var shifted = Follow209("C", model, origin.State, origin.Goal,
                            start => baseline(start) with { Drive = 1 }, true, 1.45,
                            posture: _ => new(1, direction * HandlingContact.MaximumRiderForeAftMeters));
                        string tag = direction > 0 ? "forward" : "rearward";
                        Save(Path.Combine(output, $"slide-{visit}-{offset}-{gate}-{tag}-posture.json.gz"), shifted, true);
                    }
                }
            }
            Console.WriteLine($"Recorded slide probes for bend {visit + 1}.");
        }
    }
}
