using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Freeze211(string root, string output)
    {
        string prior = Path.Combine(output, "209");
        Directory.CreateDirectory(prior);
        FreezeReference210(root, prior);
        var source = SourceFor211(root);
        var law = Law211(source, Reference211);
        var strict = new RacingProfile206(Map199);
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, Evidence210, "official/admission.json")));
        var models = expected.RootElement.GetProperty("Races").Deserialize<Run209[]>(Json)!;
        var rows = new List<object>();
        var aggregate = new List<double>();
        foreach (string fixture in Official208)
        {
            string path = Path.Combine(root, Evidence210, "native", "H29-P-" + fixture + ".json.gz");
            using var compressed = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
            using var archive = JsonDocument.Parse(compressed);
            var run = archive.RootElement.GetProperty("Run").Deserialize<Run144>(Json)!;
            var result = archive.RootElement.GetProperty("Result").Deserialize<NativeResult208>(Json)!;
            var observer = strict.Begin();
            var rider = run.Riders.Single();
            observer.ObserveInitial(rider.Samples[0].Input.Rider, rider.Samples[0].Input.Time);
            int index = 1, actions = 0, histories = 0, count = 0;
            double goal = 3 * run.LapMeters;
            var times = new List<double>();
            foreach (var row in archive.RootElement.GetProperty("Decisions").EnumerateArray())
            {
                var state = row.GetProperty("State").Deserialize<Start197>(Json)!;
                while (index < rider.Samples.Length && rider.Samples[index].Input.Time <= state.State.Time + 1e-9)
                    Inspect(rider.Samples[index++]);
                if (state.TechniqueHistory == observer.Capture()) histories++;
                if (law(state) == row.GetProperty("Action").Deserialize<FreeRacingAction>(Json)) actions++;
                times.Add(row.GetProperty("Milliseconds").GetDouble());
                count++;
            }
            while (index < rider.Samples.Length) Inspect(rider.Samples[index++]);
            var model = RaceFrom210(RootFor210(root, fixture), law);
            var known = models.Single(r => r.Fixture == fixture);
            double sweep = rider.Samples.Where(s => s.Certificate.HasValue).Min(s => s.Certificate!.Value);
            var timing = DecisionTiming206.Measure(times);
            if (histories != count || actions != count || !model.Complete || model.Finish != known.Finish ||
                Math.Abs(model.Sweep - known.Sweep) > 1e-9 || result.Sweep != sweep || result.Decisions != timing ||
                result.Finish != rider.FinishSeconds || run.Crossings.Last().Time != rider.FinishSeconds ||
                !result.Finished || run.Contacts.Length != 0)
                throw new InvalidDataException("Frozen #210 replay disagrees.");
            aggregate.AddRange(times);
            rows.Add(new { Fixture = fixture, Archive = Hash210(path), ActionsExact = actions, HistoriesExact = histories,
                RecheckedSubsteps = index - 1, NativeFinish = result.Finish, NativeSweep = sweep, Contacts = run.Contacts.Length,
                result.Decisions, Technique = observer.Capture(), Model = model });
            Console.WriteLine($"{fixture}: {actions} exact #210 requests and {histories} strict checkpoints; {model.Finish:F6} MODEL.");

            void Inspect(StartSample144 sample)
            {
                var failure = observer.Check(sample.Input.Rider, sample.Input.Time, sample.Outer, sample.Certificate!.Value,
                    .5, RacingProfile206.Physical(sample.Input.Rider, sample.Input.Time, (float)sample.Powertrain!.Seconds,
                        sample.Forces!.Value, sample.Powertrain), sample.Input.Rider.TotalReferenceMeters >= goal);
                if (failure is not null) throw new InvalidDataException($"Archived #210 violation: {failure.Condition}.");
            }
        }
        var failed = JsonSerializer.Deserialize<NativeResult208>(File.ReadAllText(Path.Combine(root, Evidence210,
            "first-freeze/native/H29-P-C.result.json")), Json);
        Save(Path.Combine(output, "frozen-210.json"), new { Parent = Parent211, BaselineSha256 = Frozen210Sha, Fixtures = rows,
            Pooled = DecisionTiming206.Measure(aggregate), HistoricalDNF = failed, NativeWorlds = 0 });
        var nominal = RootFor210(root, "C");
        Save(Path.Combine(output, "nominal-210.json.gz"), Follow209("C", nominal.Model, nominal.State, nominal.Goal, law, true), true);
    }

    private static void HighEntries211(string root, string output, string candidatePath)
    {
        var source = SourceFor211(root);
        var selected = Read211(candidatePath);
        var nominal = RootFor210(root, "C");
        var law = Law211(source, selected);
        using var process = Process.GetCurrentProcess();
        var cpu = process.TotalProcessorTime;
        var watch = Stopwatch.StartNew();
        var surveys = new List<object>();
        var previews = new List<object>();
        const int proposalCapPerTransition = 54;
        for (int visit = 0; visit < 5; visit++)
        {
            var previous = source.Parent.Source.Policy.Bend(visit);
            var next = source.Parent.Source.Policy.Bend(visit + 1);
            var start = Prefix210(nominal, law, LinkFeedback211.Begin(previous) - 3);
            var b = selected.Links.FirstOrDefault(p => p.Visit == visit) ?? Neutral211(visit);
            var menu = new List<Policy211>();
            foreach (double drive in new[] { .50, .78, 1 })
            foreach (double release in new[] { -2d, 7, 15, 23, 32, 41 })
            foreach (double correction in new[] { -.007, 0, .007 })
            {
                var link = b with { StraightDrive = drive, ReleaseMeters = release,
                    ReleaseDrive = Math.Min(b.ReleaseDrive, -.22), PrepareSteer = b.PrepareSteer + correction,
                    TurnSteer = b.TurnSteer - .5 * correction };
                menu.Add(Replace211(selected, link));
            }
            var results = new Sample211[menu.Count];
            Parallel.For(0, menu.Count, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
            {
                var plan = menu[i];
                results[i] = new(i + 1, 0, "entry exploration", plan, [Measure211(start, Law211(source, plan), source, [visit])]);
            });
            surveys.Add(new { Visit = visit + 1, EntryS = next.Entry, StabilizationS = next.Exit + 12, Root = start.State,
                ParentSweep = start.Sweep, Trials = results });

            foreach (int offset in new[] { -12, 5, 20, 35 })
            {
                var at = Prefix210(nominal, law, previous.Exit + offset).State;
                // Prefix210 crosses at a physical substep. Advance to the next review,
                // preserving the same active command and all other checkpoint history.
                var review = nominal.Model.BeginFeedback207(at, law, nominal.Goal, .50);
                review.Advance(20, at.Review);
                at = review.Checkpoint();
                foreach (float gas in new[] { .7f, 1f })
                {
                    FreeRacingAction Request(Start197 s) => s.State.Time < at.State.Time + .025
                        ? law(s) with { Drive = gas } : law(s);
                    var shortGuard = ProbeRun210(nominal.Model, at, nominal.Goal, Request, nominal.Goal, .25);
                    var supervisor = ProbeRun210(nominal.Model, at, nominal.Goal, Request, nominal.Goal, 1.2);
                    var nextBend = ProbeRun210(nominal.Model, at, nominal.Goal, Request, next.Exit + 12, 16);
                    var finish = RaceFrom210(nominal with { State = at }, Request);
                    previews.Add(new { Visit = visit + 1, Offset = offset, Gas = gas, Start = at, Short = shortGuard,
                        Supervisor = supervisor, Next = nextBend, Finish = finish,
                        ThroughNext = LinkFeedback211.Eligible(nextBend.LastLegalS, nextBend.Stop, nextBend.Sweep, next.Exit + 12) });
                }
            }
            Console.WriteLine($"Transition {visit + 1}: {results.Count(Legal211)}/{results.Length} full nominal continuations.");
        }
        Save(Path.Combine(output, "entry-search.json.gz"), new { Surveys = surveys, ProposalsPerTransition = proposalCapPerTransition,
            TotalTransitions = 5, MaxRolloutSteps = 10000, Workers = 4, HorizonComparisons = previews,
            Cpu = (process.TotalProcessorTime - cpu).TotalSeconds, Wall = watch.Elapsed.TotalSeconds, NativeWorlds = 0 }, true);
    }
}
