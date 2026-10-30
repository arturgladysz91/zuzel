namespace LineThrottle156;

public static partial class Study156
{
    public static void Control(string fixture, string label) { Begin("control-" + fixture + "-" + label, 15); Snapshot("control-" + fixture + "-" + label); try { var f = Fixtures().Single(x => x.Name == fixture); var a = Rollout(new Experiment(f), f, "U", "UNCONDITIONED", fixture + "-unconditioned-" + label, null); Save(Doc + "/control/" + fixture + "-" + label + ".json.gz", a); End(a.Reason); Console.WriteLine($"unconditioned {fixture}: {a.Reason} complete={a.Complete} S={a.Legal} mean={a.MeanOffset:F2}"); } catch (Exception ex) { End(ex.Message); throw; } }
}
