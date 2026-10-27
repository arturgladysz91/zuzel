using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using SharedWorldSpeedway;
using CoreSim.PhysicalSpace;

if(args.Length!=3||args[0]!="--compare-native-prefixes")throw new ArgumentException("Read-only, zero native worlds: --compare-native-prefixes <results.json> <raw-directory>");
var path=Path.GetFullPath(args[1]);var raw=Path.GetFullPath(args[2]);var r=JsonNode.Parse(File.ReadAllText(path))!;var json=FreeLearningExperiment.Json;
if(r["ConfirmationComplete"]?.GetValue<bool>()!=true)throw new IOException("Complete held native trials first.");
var winner=r["ConfirmationLock"]!["Winners"]![0]!["Id"]!.GetValue<string>();
var names=(from family in new[]{"D0","H0","H1"} from variant in new[]{"A",winner} select family+"-"+variant).Distinct();
var cpu=Process.GetCurrentProcess().TotalProcessorTime;var checks=new List<object>();int forecasts=0;long contacts=0;bool passed=true;
foreach(var name in names){
    var row=((JsonArray)r["Native"]!).Single(q=>q!["Name"]!.GetValue<string>()==name)!;
    var c=row["Case"]!.Deserialize<NativeCase139>(json)!;var run=RobustLearningExperiment.ReadEpisode(raw,name);var samples=run.Replay.Samples;
    var f=TrackAwarenessExperiment134.Frame(c.Case.Geometry);var p=FreeLearningExperiment.Parameters();var spec=FreeLearningExperiment.Rider(c.Case.Scenario);
    var eligible=run.Replay.Decisions.Where(d=>d.Time<=samples[^1].Input.Time-.4-1e-7).ToArray();var exit=SpeedThrottleAudit136.Exit(f,c.Corner);
    double Position(FreeLearningDecision d)=>samples.MinBy(s=>Math.Abs(s.Input.Time-d.Time))!.Input.Rider.TotalReferenceMeters;
    var probes=new[]{eligible[0],eligible.MinBy(d=>Math.Abs(Position(d)-(exit-10)))!,eligible.MinBy(d=>Math.Abs(Position(d)-(exit+20)))!}.DistinctBy(d=>d.Time);
    foreach(var decision in probes){
        var sample=samples.MinBy(s=>Math.Abs(s.Input.Time-decision.Time))!;var input=RiderDecisionAudit133.BeforeDecision(sample) with{Time=decision.Time,NextReview=decision.Time};
        var prediction=FreeRacingModel.Predict(input,spec,f,MultiActionExperiment135.Surface(f),p,147,52.72097f,.4,decision.Action);forecasts++;contacts+=prediction.ForceEvaluations;
        double xy=0,speed=0,yaw=0,omega=0,heading=0,steering=0;int count=0;
        foreach(var q in prediction.Points){var native=samples.MinBy(s=>Math.Abs(s.Input.Time-q.Time))!;if(Math.Abs(native.Input.Time-q.Time)>1e-7)continue;
            xy=Math.Max(xy,Vector2.Distance(q.Rider.Position,native.Input.Rider.Position));speed=Math.Max(speed,Math.Abs(q.Rider.Speed-native.Input.Rider.Speed));
            yaw=Math.Max(yaw,Math.Abs(q.Rider.AngularVelocity-native.Input.Rider.AngularVelocity));omega=Math.Max(omega,Math.Abs(q.Omega-native.Input.Omega));
            heading=Math.Max(heading,Math.Abs(BikeAngles.Wrap(q.Rider.Heading-native.Input.Rider.Heading))*180/Math.PI);
            steering=Math.Max(steering,Math.Abs(q.Steering-native.Input.Steering)*180/Math.PI);count++;
        }
        var ok=count>=39&&xy<=.02&&speed<=.02&&yaw<=.005&&omega<=.02&&heading<=.1&&steering<=.01;passed&=ok;
        checks.Add(new{Name=name,Time=decision.Time,Reference=input.Rider.TotalReferenceMeters,Passed=ok,Count=count,XYMeters=xy,SpeedMetersPerSecond=speed,Yaw=yaw,Omega=omega,HeadingDegrees=heading,SteeringDegrees=steering,
            Meaning="Compare the common physical first0.4s before a later review's command could activate at+.44. H09 terminals after real replanning are not falsely compared as identical controls."});
    }
}
var output=new{Passed=passed,NewNativeWorlds=0,Forecasts=forecasts,Contacts=contacts,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,Checks=checks,
    SourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/constraints-analysis143/Program.cs"))),
    Limits="Verification is an analysis cost, separate and added to main totals. Accurate common-prefix predictions do not certify long continuation or empirical tyre realism."};
File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!,"predictor-check.json"),JsonSerializer.Serialize(output,json));
Console.WriteLine(JsonSerializer.Serialize(new{passed,forecasts,contacts,checks=checks.Count}));if(!passed)throw new IOException("Predictor/native common prefix differs; report before any promotion.");
