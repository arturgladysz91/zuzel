using System.IO.Compression;
using System.Text.Json;
using SharedWorldSpeedway;
using Autonomous146;
using Safety153;

namespace LineThrottle156;

public static partial class Study156
{
    static void Snapshot(string label) { Directory.CreateDirectory(Doc + "/sources"); using var f = new FileStream(Doc + "/sources/" + label + ".zip", FileMode.CreateNew); using var zip = new ZipArchive(f, ZipArchiveMode.Create); foreach (var p in Directory.GetFiles("tools/line-throttle-physics").Where(p => p.EndsWith(".cs") || p.EndsWith(".csproj") || p.EndsWith(".json"))) zip.CreateEntryFromFile(p, Path.GetFileName(p), CompressionLevel.Optimal); }
    public static void Matrix(string fixture, string version, double budget)
    {
        var f = Fixtures().Single(x => x.Name == fixture); if (f.Held) throw new ArgumentException("Do not optimize held states"); if (budget is < 20 or > 200) throw new ArgumentException("20..200CPU-s");
        Begin("matrix-" + fixture + "-" + version, budget + 10); Snapshot("matrix-" + fixture + "-" + version); var began = Cpu;
        try
        {
            var e = new Experiment(f); var rows = new List<object>(); var baseline = new Dictionary<string, Attempt156>();
            foreach (var family in new[] { "A", "B", "C" })
            {
                var a = Rollout(e, f, family, "T0", family + "-T0-" + version, null); baseline[family] = a; Save(Doc + "/attempts/" + fixture + "-" + a.Label + ".json.gz", a);
                rows.Add(Summary(a)); Console.WriteLine($"{fixture} {a.Label} complete={a.Complete} S={a.Legal} family={a.FamilyAchieved} mean={a.MeanOffset:F2} time={a.Arrival - f.Time:F3} CPU={Cpu - began:F2}");
            }
            var remain = Math.Max(0, budget - (Cpu - began));
            foreach (var family in new[] { "A", "B", "C" })
            {
                var start = Cpu; var allocation = remain / 3; var best = baseline[family] with { Strategy = "T1", Label = family + "-T1-" + version + "-incumbent" }; var tried = 0; var improved = 0;
                // All proposals are actual delayed command sequences, at the unchanged0.2s
                // observation grid. Full throttle is one finite candidate. There is no
                // reward for throttle or width: feasible candidates rank by common-goal time.
                var original = best.Actions;
                if (original.Length > 0)
                {
                    var knots = Enumerable.Range(0, Math.Max(1, (original.Length + 3) / 4)).Select(k => k * 4).Reverse().ToArray();
                    for (int pass = 0; pass < 3 && Cpu - start < allocation; pass++)
                    {
                        bool changed = false;
                        foreach (var k in knots)
                        {
                            foreach (var width in new[] { 1, 3, 6 })
                            {
                                foreach (var drive in new[] { 0f, .31820688f, .6f, 1f }) foreach (var steer in pass == 0 ? new[] { 0d, -.2, .2 } : new[] { 0d, -.05, .05 })
                                {
                                    if (Cpu - start >= allocation || Cpu - began >= budget) break;
                                    var tape = best.Actions.ToArray(); for (int j = k; j < Math.Min(tape.Length, k + width); j++) tape[j] = new((float)Math.Clamp(tape[j].SteeringRadians + steer * Math.PI / 180, -35 * Math.PI / 180, 35 * Math.PI / 180), drive);
                                    var a = Rollout(e, f, family, "T1", family + "-T1-" + version + "-" + (tried++).ToString("D5"), null, tape);
                                    rows.Add(new { Summary = Summary(a), Knot = k, Width = width, Drive = drive, SteerDelta = steer, Parent = best.Label });
                                    if (Fitness(a, f) + 1e-7 < Fitness(best, f)) { best = a; changed = true; improved++; Save(Doc + "/attempts/" + fixture + "-" + a.Label + ".json.gz", a); }
                                }
                                if (Cpu - start >= allocation || Cpu - began >= budget) break;
                            }
                            if (Cpu - start >= allocation || Cpu - began >= budget) break;
                        }
                        if (!changed) break;
                    }
                }
                Save(Doc + "/best/" + fixture + "-" + family + "-" + version + ".json.gz", best); Console.WriteLine($"{fixture} {family} T1 tried={tried} improvements={improved} complete={best.Complete} S={best.Legal} family={best.FamilyAchieved} time={best.Arrival - f.Time:F3} CPU={Cpu - began:F2}");
            }
            Save(Doc + "/matrix-" + fixture + "-" + version + ".json", new { Fixture = f, Rows = rows, CpuSeconds = Cpu - began, Limit = budget, Definition = "T0 original K1 menu and score conditioned on a family corridor; infeasible-horizon family deficit is a construction heuristic. T1 bounded offline joint command optimization; only complete S0.5 family members ranked by time." }); End("matrix complete");
        }
        catch (Exception ex) { End("failed: " + ex.Message); throw; }
    }
    static object Summary(Attempt156 a) => new { a.Label, a.Fixture, a.Family, a.Strategy, a.Complete, a.Legal, a.FamilyAchieved, a.MeanOffset, a.Arrival, a.Reason, a.MinimumGap, a.Sweep, a.MaxBeta, a.CpuSeconds };
    public static void Held(string sourcePath, string fixture, string label) { var source = Read<Attempt156>(sourcePath); var f = Fixtures().Single(x => x.Name == fixture); if (!f.Held) throw new ArgumentException("held fixture required"); Begin("held-" + label, 10); Snapshot("held-" + label); try { var skip = (int)Math.Round((f.Time - source.Points[0].Time) / .2); if (skip < 0 || skip >= source.Actions.Length) throw new ArgumentException("Overlapping held window"); var e = new Experiment(f); var a = Rollout(e, f, source.Family, source.Strategy, source.Family + "-" + source.Strategy + "-" + label, null, source.Actions.Skip(skip).ToArray()); Save(Doc + "/held/" + label + ".json.gz", a); Save(Doc + "/held/" + label + "-transfer.json", new { Source = sourcePath, SkippedPastCommands = skip, NoSearch = true, SourceInitial = source.Points[0], Held = f, Summary = Summary(a) }); End(a.Reason); Console.WriteLine($"held {label}: complete={a.Complete} S={a.Legal} family={a.FamilyAchieved} time={a.Arrival - f.Time:F3}"); } catch (Exception ex) { End("failed: " + ex.Message); throw; } }
}
