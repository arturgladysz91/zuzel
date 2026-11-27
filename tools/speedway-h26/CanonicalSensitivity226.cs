namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Perturbation46(string Name, Vec215 Position, Vec215 Velocity, Vec215 Euler,
        Vec215 AngularMomentum, double DelaySeconds);

    private static void Perturb46(Lab38 lab, Geometry215 g, string output, string[] args)
    {
        var old = Read199<Evidence39>(Source46(lab, 225, "transition-615-full.json.gz"));
        var guide = Guides46(lab).Single(x => x.Name == "H44-B615");
        var supported = guide with { Name = "H45 local feedback", Damping = .40 };
        double marker = args.Length == 0 ? Frame.LapMeters - 12 : double.Parse(args[0],
            System.Globalization.CultureInfo.InvariantCulture);
        var checkpoint = old.Rows.First(s => s.ReferenceMeters >= marker);
        var at = new Checkpoint38(checkpoint.State, checkpoint.ReferenceMeters);
        const double duration = .96;
        var cases = new Perturbation46[]
        {
            new("nominal", default, default, default, default, 0),
            new("position rounding scale", new(4e-14, 0, 0), default, default, default, 0),
            new("momentum rounding scale", default, new(0, 2e-12 / g.Base.Mass, 0), default, default, 0),
            new("position 1 mm", new(0, .001, 0), default, default, default, 0),
            new("velocity 0.02 m/s", default, new(.02, 0, 0), default, default, 0),
            new("attitude 0.4 mrad", default, default, new(.0004, 0, 0), default, 0),
            new("angular momentum 0.015 kg m2/s", default, default, default, new(.015, 0, 0), 0),
            new("next queued activation delayed 0.2 ms", default, default, default, default, .0002)
        };
        var clock = new ClockPlan46(Clock46.Tick(at.State.Time), Clock46.StockTicks,
            Clock46.Tick(at.State.Time) + Clock46.Tick(duration), null, true);
        var nominal = new ReferenceTape46("legacy B615 reached state", at, old.Commands,
            Clock46.Seconds(clock.EndTick), null, "transition-615-full");

        var modes = new[] { "fixed commands", "H44 feedback", "H45 feedback" };
        var grouped = modes.Select(mode =>
        {
            CanonicalRun46 Run(ReferenceTape46 input) => mode == modes[0] ?
                ReplayCanonical46(lab, g, input, clock) :
                Live46(lab, g, input, clock, (mode == modes[1] ? guide : supported) with { StartStation = at.Distance });
            var baseline = Run(nominal);
            var trials = cases.AsParallel().AsOrdered().WithDegreeOfParallelism(4).Select(p =>
            {
                var q = at.State;
                var pose = p.Euler == default ? q.Pose : q.Pose.Advance(p.Euler, 1);
                var perturbed = q with { Center = q.Center + p.Position, Momentum = q.Momentum + p.Velocity * g.Base.Mass,
                    Pose = pose, Angular = q.Angular + p.AngularMomentum };
                var tape = old.Commands.ToArray();
                if (p.DelaySeconds > 0 && q.Next < tape.Length)
                    tape[q.Next] = tape[q.Next] with { Time = tape[q.Next].Time + p.DelaySeconds };
                var local = Run(nominal with { Seed = at with { State = perturbed }, Tape = tape });
                var f = local.Motion.Flight;
                string Branch(Step215 r) => $"{r.Subdivisions}/{r.Contacts.Length}/{r.Forces.Front.Normal > 0}/{r.Forces.Rear.Normal > 0}/{r.Forces.LeftFoot?.Contact.NormalForce > 0}";
                var comparisons = baseline.Motion.Flight.Steps.Zip(f.Steps).Select((pair, index) =>
                {
                    var (a, b) = pair;
                    return new { index, Time = a.After.Time, OtherTime = b.After.Time,
                        DtA = a.Plan.Duration, DtB = b.Plan.Duration, Original = Branch(a), Other = Branch(b),
                        Position = (a.After.Center - b.After.Center).Norm, Momentum = (a.After.Momentum - b.After.Momentum).Norm,
                        Angular = (a.After.Angular - b.After.Angular).Norm, Roll = b.After.Pose.Roll - a.After.Pose.Roll,
                        SolveBranchChanged = Branch(a) != Branch(b), Requested = local.Schedule[index].Requested,
                        BaseRequested = baseline.Schedule[index].Requested };
                }).ToArray();
                var matched = new List<object>();
                for (int i = 1; i <= 16; i++)
                {
                    long tick = clock.Origin + i * Clock46.ReviewTicks;
                    var a = baseline.Motion.Flight.Steps.LastOrDefault(r => Clock46.Tick(r.After.Time) == tick);
                    var b = f.Steps.LastOrDefault(r => Clock46.Tick(r.After.Time) == tick);
                    if (a is null || b is null) break;
                    matched.Add(new { Time = Clock46.Seconds(tick), Position = (a.After.Center - b.After.Center).Norm,
                        Momentum = (a.After.Momentum - b.After.Momentum).Norm, Roll = b.After.Pose.Roll - a.After.Pose.Roll,
                        Angular = (a.After.Angular - b.After.Angular).Norm, b.Forces.RearSlip, b.Forces.Front.Normal });
                }
                return new { p, f.Final, f.Stop, f.Boundary, f.Ground, local.SweepRetries,
                    FirstDifferentBranch = comparisons.FirstOrDefault(x => x.SolveBranchChanged),
                    FirstDifferentStep = comparisons.FirstOrDefault(x => !SameBits46(x.DtA, x.DtB)),
                    SameClockSamples = matched, Commands = local.Motion.Tape,
                    Energy = Balance46(f, g) };
            }).ToArray();
            return new { Mode = mode, Base = Digest46(lab, g, clock, baseline), Trials = trials };
        }).ToArray();

        var convergence = new[] { 248d, at.Distance, 399d }.Select(station =>
        {
            var o = old.Rows.First(x => x.ReferenceMeters >= station);
            var local = nominal with { Seed = new(o.State, o.ReferenceMeters), Until = o.State.Time + duration };
            var meshes = Mesh46.Select(step =>
            {
                var plan = PlanClock46(local, step, refine: true);
                var run = ReplayCanonical46(lab, g, local, plan);
                var f = run.Motion.Flight;
                return new { StepMs = 1000 * Clock46.Seconds(step), f.Final, f.Stop, f.Boundary, f.Ground,
                    f.Steps.Length, run.SweepRetries, Work = Balance46(f, g),
                    ContactEvents = f.Steps.Sum(x => x.Contacts.Length) };
            }).ToArray();
            return new { local.Seed, Meshes = meshes };
        }).ToArray();

        Save(Path.Combine(output, "perturbations.json.gz"), new { from = at, Seconds = duration, Modes = grouped,
            Convergence = convergence }, true);
    }
}
