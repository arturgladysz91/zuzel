using SharedWorldSpeedway;
namespace LineThrottle156;

public static partial class Study156
{
    public static void Refine(string fixture, string label, double limit)
    {
        if (limit is < 15 or > 100) throw new ArgumentException("15..100CPU-s"); var f = Fixtures().Single(x => x.Name == fixture); var version = fixture == "historical" ? "v4" : "v3";
        Begin("refine-" + fixture + "-" + label, limit + 5); Snapshot("refine-" + fixture + "-" + label); var began = Cpu;
        try
        {
            var e = new Experiment(f); var seeds = new[] { "A", "B", "C" }.Select(family => Read<Attempt156>(Doc + "/best/" + fixture + "-" + family + "-" + version + ".json.gz")).ToArray(); var rows = new List<object>();
            foreach (var family in new[] { "A", "B", "C" })
            {
                var start = Cpu; int n = 0; var best = seeds.Single(x => x.Family == family) with { Label = family + "-T1-" + label + "-seed" };
                foreach (var seed in seeds.Where(x => x.Complete && x.Legal)) { var a = Rollout(e, f, family, "T1", family + "-T1-" + label + "-cross-" + seed.Family, null, seed.Actions); rows.Add(new { Summary = Summary(a), CrossFamilySeed = seed.Family }); if (Fitness(a, f) < Fitness(best, f)) { best = a; Save(Doc + "/attempts/" + fixture + "-" + a.Label + ".json.gz", a); } }
                int At(double reference) { var p = best.Points.FirstOrDefault(p => p.Rider.TotalReferenceMeters >= reference) ?? best.Points[^1]; return Math.Clamp((int)Math.Round((p.Time - f.Time) / .2), 0, Math.Max(0, best.Actions.Length - 1)); }
                var entry = At(f.Entry); var apex = At((f.Entry + f.Exit) / 2); var exit = At(f.Exit); var tail = best.Actions.Length - 1;
                var knots = new[] { entry, apex, Math.Max(0, entry - 5), exit, 0, (entry + apex) / 2, (apex + exit) / 2, (exit + tail) / 2, Math.Max(0, entry - 2), Math.Min(tail, apex + 1), Math.Max(0, apex - 1) }.Distinct().ToArray();
                foreach (var width in new[] { 3, 1, 6 })
                {
                    foreach (var knot in knots)
                    {
                        foreach (var drive in new[] { 1f, .6f, .31820688f, 0f }) foreach (var angle in new[] { 0d, -.25, .25, -.75, .75 })
                        {
                            if (Cpu - start >= limit / 3 || Cpu - began >= limit) break; var tape = best.Actions.ToArray(); for (int j = knot; j < Math.Min(tape.Length, knot + width); j++) tape[j] = new((float)Math.Clamp(tape[j].SteeringRadians + angle * Math.PI / 180, -35 * Math.PI / 180, 35 * Math.PI / 180), drive);
                            var a = Rollout(e, f, family, "T1", family + "-T1-" + label + "-" + (n++).ToString("D5"), null, tape); rows.Add(new { Summary = Summary(a), Parent = best.Label, Knot = knot, Width = width, Drive = drive, SteeringDeltaDegrees = angle });
                            if (Fitness(a, f) + 1e-7 < Fitness(best, f)) { best = a; Save(Doc + "/attempts/" + fixture + "-" + a.Label + ".json.gz", a); }
                        }
                        if (Cpu - start >= limit / 3 || Cpu - began >= limit) break;
                    }
                    if (Cpu - start >= limit / 3 || Cpu - began >= limit) break;
                }
                Save(Doc + "/best/" + fixture + "-" + family + "-" + label + ".json.gz", best); Console.WriteLine($"refine {fixture}/{family} n={n} complete={best.Complete} S={best.Legal} family={best.FamilyAchieved} time={best.Arrival - f.Time:F3} CPU={Cpu - began:F2}");
            }
            Save(Doc + "/refine-" + fixture + "-" + label + ".json", new { Fixture = f, Rows = rows, ElapsedCpu = Cpu - began, Scope = "Balanced preparation/entry/apex/exit/straight knots. Cross-family tapes are seeds only; actual family and physical constraints still own feasibility. No native state injection." }); End("balanced joint command search");
        }
        catch (Exception ex) { End("failed: " + ex.Message); throw; }
    }
}
