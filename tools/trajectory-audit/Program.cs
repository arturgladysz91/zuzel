using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreSim.Analysis;

Directory.CreateDirectory(args[0]);
var bytes = CaptureProbe.Capture();
File.WriteAllBytes(Path.Combine(args[0], "trace.json"), bytes);
var options = new JsonSerializerOptions { WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }, NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals };
var document = new { CaptureProbe.Decisions, CaptureProbe.FinalHeat };
File.WriteAllText(Path.Combine(args[0], "audit.json"), JsonSerializer.Serialize(document, options) + "\n");
var bits = new SortedDictionary<string, object>(StringComparer.Ordinal);
Walk(document, "$", bits);
File.WriteAllText(Path.Combine(args[0], "bits.json"), JsonSerializer.Serialize(bits, options) + "\n");
Console.WriteLine($"CAPTURE SHA256={Convert.ToHexString(SHA256.HashData(bytes))} bytes={bytes.Length}");

static void Walk(object? value, string path, IDictionary<string, object> bits)
{
    if (value is float single) { bits[path] = new { Type = "float", Bits = $"0x{BitConverter.SingleToInt32Bits(single):X8}" }; return; }
    if (value is double number) { bits[path] = new { Type = "double", Bits = $"0x{BitConverter.DoubleToInt64Bits(number):X16}" }; return; }
    if (value is null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) return;
    if (value is IEnumerable sequence)
    {
        var i = 0; foreach (var item in sequence) Walk(item, $"{path}[{i++}]", bits);
        return;
    }
    foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetIndexParameters().Length == 0))
        Walk(property.GetValue(value), path + "." + property.Name, bits);
}
