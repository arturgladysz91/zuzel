using System.Text.Json;
using SharedWorldSpeedway;

var scenario = args.FirstOrDefault() ?? "A";
if (scenario == "track-geometry") { TrackGeometryAudit.Run(args[1], args[2]); return; }
if (scenario == "rear-wheel") { RearWheelAudit.Run(args[1], args[2]); return; }
if (scenario == "physics-line") { PhysicsDrivenRacingLineAudit.Run(args[1], args[2]); return; }
if (scenario == "slip-control-validation") { SlipControlValidation.Run(args[1], args[2]); return; }
if (scenario == "slip-dynamics") { SlipDynamicsAudit.Run(args[1], args[2]); return; }
if (scenario == "replay-geometry") { ReplayGeometry.Save(args[1]); return; }
if (scenario == "autonomous-line") { AutonomousLineAudit.Run(args[1], args[2]); return; }
if (scenario == "close-racing") { CloseRacingAudit.Run(args[1], args[2]); return; }
if (scenario == "throttle-diagnose") { ThrottleAudit.Diagnose(args[1]); return; }
if (scenario == "throttle-audit") { ThrottleAudit.Run(args[1], args[2]); return; }
if (scenario == "full-validation") { FullHeatSoloAudit.Run(args[1], args.Length > 2 ? args[2] : "smoke"); return; }
if (scenario == "quality-freeze") { TacticalQualityAudit.Freeze(args[1]); return; }
if (scenario == "quality-evaluate") { TacticalQualityAudit.Evaluate(args[1], args[2]); return; }
if (scenario == "quality-races") { TacticalQualityAudit.Races(args[1]); return; }
if (scenario == "quality-benchmark") { SideBySideAudit.Benchmark(args[1], qualityComparison: true); TacticalQualityAudit.ScoringCost(args[2], args[1]); return; }
if (scenario == "quality-score-cost") { TacticalQualityAudit.ScoringCost(args[1], args[2]); return; }
if (scenario == "quality-reaction") { TacticalQualityAudit.ReactionProbe(args[1], args[2]); return; }
if (scenario == "side-diagnose") { SideBySideAudit.Diagnose(args.Length > 1 ? args[1] : "side-results"); return; }
if (scenario == "side-pairs") { SideBySideAudit.Run(args.Length > 1 ? args[1] : "side-results",
    SideBySideScenarios.PairCases().Where(s => args.Length < 3 || s.Name == args[2]).ToArray()); return; }
if (scenario == "side-fields") { SideBySideAudit.Run(args.Length > 1 ? args[1] : "side-results", SideBySideScenarios.FieldCases()); return; }
if (scenario == "side") { SideBySideAudit.Run(args.Length > 1 ? args[1] : "side-results", SideBySideScenarios.Cases()); return; }
if (scenario == "side-benchmark") { SideBySideAudit.Benchmark(args.Length > 1 ? args[1] : "side-results"); return; }
if (scenario == "audit") { Audit.Run(args.Length > 1 ? args[1] : "shared-world-results"); return; }
if (scenario == "diagnose") { StabilityAudit.Diagnose(args.Length > 1 ? args[1] : "stability-diagnosis"); return; }
if (scenario == "stability") { StabilityAudit.Validate(args.Length > 1 ? args[1] : "stability-results"); return; }
if (scenario == "dynamics") { DynamicsAudit.Run(args.Length > 1 ? args[1] : "dynamics-results"); return; }
if (scenario == "dynamics-benchmark") { DynamicsAudit.Benchmark(args.Length > 1 ? args[1] : "dynamics-results"); return; }
if (scenario == "tactics-a") { TacticalAudit.Run(args.Length > 1 ? args[1] : "tactical-results", new[] {TacticalScenarios.A()}); return; }
if (scenario == "tactics") { TacticalAudit.Run(args.Length > 1 ? args[1] : "tactical-results", TacticalScenarios.Cases()); return; }
if (scenario == "tactics-benchmark") { TacticalAudit.Benchmark(args.Length > 1 ? args[1] : "tactical-results"); return; }
if (scenario == "ccd") { Console.WriteLine(JsonSerializer.Serialize(Audit.CcdProbe(), Audit.Json)); return; }
var step = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : .02;
var selected = scenario switch { "A" => Scenarios.A(), "heat" => Scenarios.A(4), "B" => Scenarios.B(),
    "B0" => Scenarios.B(false), "C" => Scenarios.C(), "D" => Scenarios.D(), "E" => Scenarios.E(),
    "F" => Scenarios.F(), "G" => Scenarios.G(), "stress" => Scenarios.DenseStart(), "support" => Scenarios.Support(),
    "TA" => TacticalScenarios.A(), "SA" => SideBySideScenarios.A(), "SE" => SideBySideScenarios.E(), "SF" => SideBySideScenarios.F(),
    _ => throw new ArgumentException("Use A/B/B0/C/D/E/F/G/heat/stress [0.005|0.01|0.02]") };
using var world = new WorldRunner(selected.Riders, parameters: new() {
    EnableTacticalRacing = args.Contains("--tactics") || args.Contains("--side-by-side") || args.Contains("--quality"),
    EnableSideBySideContests = args.Contains("--side-by-side") || args.Contains("--quality"),
    EnableTacticalQualityScoring = args.Contains("--quality")});
var result = world.Run(selected.Name, step, selected.DurationSeconds, selected.Laps,
    args.Contains("--trace") ? s => {
        if (Math.Abs(s.TimeSeconds * 2 - Math.Round(s.TimeSeconds * 2)) < 1e-8)
            foreach (var v in s.Riders) Console.Error.WriteLine(FormattableString.Invariant(
                $"{s.TimeSeconds:F3},{v.Id},{v.Position.X:F3},{v.Position.Y:F3},{v.Speed:F3},{v.Road.Segment},{v.Road.OffsetMeters:F3},{v.TotalReferenceMeters:F3}"));
    } : null, stopOnCrash: args.Contains("--tactics") || args.Contains("--side-by-side") || args.Contains("--stop-on-crash"));
Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true,
    IncludeFields = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
