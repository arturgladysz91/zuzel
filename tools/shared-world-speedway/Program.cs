using System.Text.Json;
using SharedWorldSpeedway;

var scenario = args.FirstOrDefault() ?? "A";
if (scenario == "audit") { Audit.Run(args.Length > 1 ? args[1] : "shared-world-results"); return; }
if (scenario == "diagnose") { StabilityAudit.Diagnose(args.Length > 1 ? args[1] : "stability-diagnosis"); return; }
if (scenario == "stability") { StabilityAudit.Validate(args.Length > 1 ? args[1] : "stability-results"); return; }
if (scenario == "ccd") { Console.WriteLine(JsonSerializer.Serialize(Audit.CcdProbe(), Audit.Json)); return; }
var step = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : .02;
var selected = scenario switch { "A" => Scenarios.A(), "heat" => Scenarios.A(4), "B" => Scenarios.B(),
    "B0" => Scenarios.B(false), "C" => Scenarios.C(), "D" => Scenarios.D(), "E" => Scenarios.E(),
    "F" => Scenarios.F(), "G" => Scenarios.G(), "stress" => Scenarios.DenseStart(), "support" => Scenarios.Support(),
    _ => throw new ArgumentException("Use A/B/B0/C/D/E/F/G/heat/stress [0.005|0.01|0.02]") };
using var world = new WorldRunner(selected.Riders);
var result = world.Run(selected.Name, step, selected.DurationSeconds, selected.Laps,
    args.Contains("--trace") ? s => {
        if (Math.Abs(s.TimeSeconds * 2 - Math.Round(s.TimeSeconds * 2)) < 1e-8)
            foreach (var v in s.Riders) Console.Error.WriteLine(FormattableString.Invariant(
                $"{s.TimeSeconds:F3},{v.Id},{v.Position.X:F3},{v.Position.Y:F3},{v.Speed:F3},{v.Road.Segment},{v.Road.OffsetMeters:F3},{v.TotalReferenceMeters:F3}"));
    } : null);
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true,
    IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
