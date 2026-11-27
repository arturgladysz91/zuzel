namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Tape45(string Name, string Stop, State215 Seed, double SeedStation,
        Activation215[] Commands, State215 Terminal, int AcceptedSteps, double FinalStation);

    private static void Standing45(Lab38 lab, Geometry215 g, string output)
    {
        var score = new[] { "support.json.gz", "sustain.json.gz" }.Select(path => Path.Combine(output, path))
            .Where(File.Exists).SelectMany(path => Read199<Stage45>(path).Exact);
        string supplement = Path.Combine(output, "support-exact.json.gz");
        if (File.Exists(supplement)) score = score.Concat(Read199<SupportCertificate45>(supplement).Proofs.Select(x => x.Exact));
        var plan = score.FirstOrDefault(x => x.Finished)?.Plan
            ?? throw new InvalidOperationException("Four flying laps plus recovery have not qualified.");
        double start = -((lab.Bend.Entry % Frame.LapMeters + Frame.LapMeters) % Frame.LapMeters);
        double finish = start + 4 * Frame.LapMeters;
        var idle = lab.Fixture.Rest(g, NominalSupport216) with
        {
            Crank = 900 * Math.PI / 30 * g.Base.CrankInertia
        };
        idle = Place36(NominalSupport216.PassiveEquilibrium(g, idle, Gravity216), lab.Bend.Entry + start,
            Lane45(lab, plan, lab.Bend.Entry + start));
        var seed = new Checkpoint38(idle, start);
        var root = new Fork40("legal model rest", seed, new(seed, [], [], "stationary on the line", 0, 0), []);
        var run = Certify45(lab, g, Ride45(lab, g, root, start, plan, finish, exact: true));
        var summary = Summarize45(lab, g, run, finish, lab.Bend.Length);
        SaveRun45(lab, g, output, "standing-selected", run, summary);
        Save(Path.Combine(output, "standing.json"), new { StandingStart = start, Finish = finish, Elapsed = run.Motion.Flight.Final.State.Time,
            GateRelease = lab.Fixture.Release, summary, FromRest = Speed38(idle, g) < 1e-8,
            RaceLaps = run.Motion.Flight.Final.Distance >= finish - .001 ? 4 :
                (int)Math.Floor((run.Motion.Flight.Final.Distance - start) / Frame.LapMeters), Energy = Energy45(run.Motion.Flight) });
    }

    private sealed record SupportCertificate45(SupportProof45[] Proofs);

    private static void Verify45(Lab38 lab, Geometry215 g, string output)
    {
        var files = Directory.GetFiles(output, "*-tape.json.gz").Where(p => !Path.GetFileName(p).StartsWith("discrepancy-")).ToArray();
        var proofs = files.AsParallel().AsOrdered().WithDegreeOfParallelism(Math.Min(4, Environment.ProcessorCount)).Select(file =>
        {
            var e = Read199<Tape45>(file);
            var motion = ReplayTape38(lab, g, new(e.Seed, e.SeedStation), e.Commands, e.Terminal.Time, exact: true);
            var last = motion.Final.State;
            var result = new
            {
                e.Name, ExactTerminal = last == e.Terminal, ExactStepCount = motion.Steps.Length == e.AcceptedSteps,
                PositionErrorM = (last.Center - e.Terminal.Center).Norm, LinearMomentumError = (last.Momentum - e.Terminal.Momentum).Norm,
                AngularMomentumError = (last.Angular - e.Terminal.Angular).Norm, RiderError = (last.RiderMomentum - e.Terminal.RiderMomentum).Norm,
                StationErrorM = motion.Final.Distance - e.FinalStation, motion.Boundary, motion.Ground, Energy = Energy45(motion),
                motion.Stop, Commands = e.Commands.Length, e.AcceptedSteps
            };
            Console.WriteLine($"Replay {e.Name}: exact terminal {result.ExactTerminal}; exact length {result.ExactStepCount}");
            return result;
        }).ToArray();
        Save(Path.Combine(output, "independent-proof.json"), new { Base = Base45, Proofs = proofs, NativeWorlds = 0 });
        if (proofs.Any(p => p.PositionErrorM > .002 || Math.Abs(p.StationErrorM) > .003 ||
            p.LinearMomentumError > .12 || p.AngularMomentumError > .12 || p.RiderError > .06 ||
            p.Boundary < .50 || p.Ground <= 0))
            throw new InvalidOperationException("A retained motion failed the independent spatial replay.");
    }
}
