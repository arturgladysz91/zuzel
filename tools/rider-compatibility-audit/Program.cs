using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim;
using CoreSim.Tests;

Directory.CreateDirectory(args[0]);
var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
foreach (var adaptive in new[] { false, true })
{
    var bytes = RiderCompatibilityFixture.Capture(adaptive);
    var hash = Convert.ToHexString(SHA256.HashData(bytes));
    var expected = adaptive ? RiderCompatibilityFixture.AdaptiveSha256 : RiderCompatibilityFixture.ConvergenceSha256;
    if (hash != expected) throw new InvalidOperationException("Legacy heat changed from frozen main.");
    var modern = RiderCompatibilityFixture.Capture(adaptive, p => RiderCompatibilityFixture.Modern(p), s => s.Condition = .72f);
    if (!bytes.SequenceEqual(modern)) throw new InvalidOperationException("Canonical data changed race behavior.");
    var name = adaptive ? "adaptive" : "convergence";
    hashes[name] = hash;
    File.WriteAllBytes(Path.Combine(args[0], name + ".json"), bytes);
}
var evidence = new
{
    RiderCompatibilityFixture.BaseMainSha,
    Scope = "Canonical domain and compatibility bridge only; no production consumers migrated.",
    LegacyFallback = RiderAbilityCompatibility.FromLegacy(new(80, 95, 70, 60, 40, 20), RiderStyle.Balanced),
    ExplicitGameplay = RiderCompatibilityFixture.ExampleGameplay(),
    Condition = .72f,
    CompleteHeatSha256 = hashes,
};
File.WriteAllText(Path.Combine(args[0], "rider-abilities-compatibility-evidence.json"),
    JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true,
        Converters = { new JsonStringEnumConverter() } })
        .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
Console.WriteLine("Canonical/legacy compatibility captures match frozen main.");
