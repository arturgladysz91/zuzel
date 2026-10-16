using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using CoreSim;
using TimeStepPhysics;

if (args is ["--baseline-smoke", var assembly])
{
    var old = new Baseline82(Path.GetFullPath(assembly)); var input = Scenarios.Create("straight");
    var race = old.Create(input, false); old.Run(race, .01, .01);
    Console.WriteLine(old.Get<double>(race, "Time")); return;
}
if (args is ["--probe"] or ["--simple"])
{
    var actual = args[0] == "--probe" ? Scenarios.CaptureReal() : null;
    foreach (var name in actual is null ? new[] { "light-straight", "bend-contact", "strong-no-room" } : new[] { "real-R3-R4" })
    foreach (var step in new[] { .005, .01 })
    {
        var race = Scenarios.Create(name, actual); race.Run(2, step);
        Console.WriteLine(JsonSerializer.Serialize(new { name, step, race.Time, race.TerminalCrash, race.StopReason,
            race.LastRejectedWindow, race.Contacts, race.Work }, JsonOptions()));
    }
    return;
}
if (args.Length != 2) throw new ArgumentException("Supply output.json and the preserved #82 Release CoreSim.Tests.dll (or --probe / --simple).");
var origin = Scenarios.CaptureReal(); // Capture/planning is outside every timer.
var baseline = new Baseline82(Path.GetFullPath(args[1]));
var rows = new List<object>(); var convergence = new List<object>();
var reached = new Dictionary<(string, double), double>();
double Median(IEnumerable<double> values) => values.Order().ElementAt(1);
foreach (var name in Scenarios.Names)
foreach (var step in new[] { .005, .01 })
foreach (var version in new[] { "82", "A13" }) Measure(name, step, version, 2);

// Equal requested AND reached short horizon; unlike the full attempts, both
// engines can execute it. Same preserved real poses/effects, no capture.
foreach (var step in new[] { .005, .01 }) foreach (var version in new[] { "82", "A13" })
    Measure("real-R3-R4", step, version, .02);

foreach (var name in Scenarios.Names)
{
    var horizon = Math.Min(reached[(name, .005)], reached[(name, .01)]);
    if (name == "real-R3-R4") horizon = Math.Min(horizon, .8);
    var a = Scenarios.Create(name, origin); var b = Scenarios.Create(name, origin);
    a.Run(horizon, .005); b.Run(horizon, .01);
    convergence.Add(new { Scenario = name, CommonHorizonSeconds = horizon,
        ReachedA = a.Time, ReachedB = b.Time, Comparable = Math.Abs(a.Time - b.Time) < 1e-10,
        MaximumPositionDifferenceMeters = a.Bikes.Zip(b.Bikes).Max(p => (p.First.Position - p.Second.Position).Length),
        MaximumSpeedDifference = a.Bikes.Zip(b.Bikes).Max(p => Math.Abs(p.First.Speed - p.Second.Speed)),
        FirstContactTimeDifferenceSeconds = a.Contacts.Count > 0 && b.Contacts.Count > 0 ? Math.Abs(a.Contacts[0].Time - b.Contacts[0].Time) : (double?)null });
}
var path = Path.GetFullPath(args[0]); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, JsonSerializer.Serialize(new { BaseSha = "67f1f7ab61df401de38e7335398c7eec14628b90",
    BaselineAssemblySha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(args[1]))),
    Warmups = 1, Repetitions = 3, Matrix = rows, Convergence = convergence }, JsonOptions()));
Console.WriteLine(path);

void Measure(string name, double step, string version, double horizon)
{
    var samples = new List<double>(); var allocations = new List<double>(); object? last = null;
    for (var repetition = -1; repetition < 3; repetition++)
    {
        var input = Scenarios.Create(name, origin);
        var old = version == "82" ? baseline.Create(input, name == "real-R3-R4") : null;
        var start = input.Time; var bytes = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        if (old is null) input.Run(horizon, step); else baseline.Run(old, horizon, step);
        watch.Stop(); var allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
        if (repetition >= 0) { samples.Add(watch.Elapsed.TotalMilliseconds); allocations.Add(allocated); }
        var time = old is null ? input.Time : baseline.Get<double>(old, "Time");
        if (version == "A13" && horizon == 2) reached[(name, step)] = time - start;
        last = old is null ? new { input.TerminalCrash, input.StopReason, input.LastRejectedWindow,
            input.Work, input.MaximumPenetration, Contacts = (object)input.Contacts,
            States = (object)input.Bikes.Select(b => new { Id = b.Rider.RiderId, b.Position, b.Velocity, b.Status }).ToArray(), SimulatedSeconds = time - start }
            : (object)new { StopReason = baseline.Get<object?>(old, "StopReason"),
                LastRejectedWindow = baseline.Get<object?>(old, "LastRejectedWindow"),
                Work = baseline.Get<object>(old, "Work"), Contacts = baseline.Get<object>(old, "Contacts"),
                MaximumPenetration = baseline.Get<double>(old, "MaximumPenetration"), SimulatedSeconds = time - start };
    }
    rows.Add(new { Scenario = name, Context = Scenarios.ContextFor(name).ToString(), StepMilliseconds = step * 1000,
        Version = version, RequestedSeconds = horizon, ElapsedMedianMilliseconds = Median(samples), AllocatedMedianBytes = Median(allocations), Result = last });
    Console.WriteLine($"{name} {step * 1000} ms / {version} / requested {horizon}s");
}
static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, IncludeFields = true };

// Read-only adapter to the exact preserved #82 binary. It shares unchanged CoreSim,
// constructs identical physical inputs, and adds no diagnostics inside Run.
sealed class Baseline82
{
    private readonly Assembly _assembly;
    private readonly Type _race, _bike, _control;
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    internal Baseline82(string path)
    {
        var context = new AssemblyLoadContext("baseline82");
        context.Resolving += (_, name) => name.Name == "CoreSim" ? typeof(Track).Assembly : null;
        _assembly = context.LoadFromAssemblyPath(path);
        _race = Type("TimeStepRace"); _bike = Type("DynamicBike"); _control = Type("DriverControl");
    }
    private Type Type(string name) => _assembly.GetType("TimeStepPhysics." + name, true)!;
    internal object Create(TimeStepRace input, bool consumed)
    {
        var array = Array.CreateInstance(_bike, 4);
        for (var i = 0; i < 4; i++)
        {
            var b = input.Bikes[i]; var c = b.Control;
            object Make(Type type, params object?[] values) => Activator.CreateInstance(type, Members, null, values, null)!;
            array.SetValue(Make(_bike, b.Rider, b.Position, b.Velocity, b.Heading, b.AngularVelocity,
                Make(_control, c.TargetOffset, c.Throttle, c.SteeringRate, c.PendingOffset, c.ReactAt),
                b.ControlLoss, b.RecoveryUntil, b.Status, b.Distance), i);
        }
        return Activator.CreateInstance(_race, Members, null, new object?[] { input.Track, input.SurfaceSnapshot,
            array, input.Time, false, null, consumed ? (3, 4) : null }, null)!;
    }
    internal void Run(object race, double horizon, double step) => _race.GetMethod("Run")!.Invoke(race, new object[] { horizon, step });
    internal T Get<T>(object race, string name) => (T)_race.GetProperty(name)!.GetValue(race)!;
}
