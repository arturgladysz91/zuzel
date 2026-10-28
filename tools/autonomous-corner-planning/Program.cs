using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Autonomous146;
using SharedWorldSpeedway;

if(args.Length==3&&args[0]=="--enable-native146"){Experiment146.Run(args[1],args[2]);return;}
if(args.Length!=2||args[0]!="--diagnose146")throw new ArgumentException("Default OFF: --diagnose146 <new-stage-A-directory>");
var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);var json=FreeLearningExperiment.Json;
var old="docs/experiments/corner-speed-loss/native/";
Run144 Read(string name){using var z=new GZipStream(File.OpenRead(old+name+".json.gz"),CompressionMode.Decompress);return JsonSerializer.Deserialize<Run144>(z,json)!;}
void Save(string name,object value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
string Hash(object v)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(v,json)));
var run=Read("autonomous-two-command-K1");var rider=run.Riders[0];var plans=JsonNode.Parse(File.ReadAllText("docs/experiments/autonomous-corner-planning/diagnosis-saved.json"))!["Rows"]!.AsArray();
var frame=StandingWorld144.Frame();var surface=MultiActionExperiment135.Surface(frame);var parameters=FreeLearningExperiment.Parameters();
var checks=new List<object>();var ledger=new JsonArray();
if(File.Exists(Path.Combine(output,"budget.json")))throw new IOException("Preserve prior Stage A attempts; use a new directory.");
using var lease=new FileStream(Path.Combine(output,"budget.lock"),FileMode.Create,FileAccess.ReadWrite,FileShare.None);
foreach(var mode in new[]{"replay145","pair-50","pair-52"}){
    var reservation=new JsonObject{["Name"]=mode,["Complete"]=false};ledger.Add(reservation);Save("budget.json",ledger);
    var cpu=Process.GetCurrentProcess().TotalProcessorTime;var at=mode=="pair-50"?5:5.2;var row=plans.First(p=>Math.Abs(p!["Time"]!.GetValue<double>()-at)<1e-8)!;
    var first=row["Chosen"]!.Deserialize<FreeRacingAction>(json);var second=row["ForecastSecond"]!.Deserialize<FreeRacingAction>(json);
    Decision144 Choose(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,CoreSim.TrackStateSnapshot t,WorldParameters p,float mass,float inertia,string variant){
        var recorded=rider.Decisions.MinBy(d=>Math.Abs(d.Choice.Time-i.Time))!;
        if(mode=="replay145"||i.Time<at-1e-8||i.Time>=at+.9-1e-8)return recorded;
        var action=i.Time<at+.2-1e-8?first:second;
        return new(new(i.Time,action,FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0),0);
    }
    Run144 native;using(var world=new DiagnosticWorld145(new(Enabled:true,MaximumSeconds:8)))native=world.Run(mode,"K1",Choose);
    using(var z=new GZipStream(File.Create(Path.Combine(output,mode+".json.gz")),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,native,json);
    var prefixEnd=mode=="replay145"?double.PositiveInfinity:at;
    var nativePrefix=native.Riders[0].Samples.Where(s=>s.Input.Time<=prefixEnd+1e-8).ToArray();var oldPrefix=rider.Samples.Where(s=>s.Input.Time<=prefixEnd+1e-8).ToArray();
    var prefixExact=nativePrefix.Length==oldPrefix.Length&&nativePrefix.Zip(oldPrefix).All(pair=>Hash(pair.First)==Hash(pair.Second));
    object? agreement=null;
    if(mode!="replay145"){
        var saved=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-at))!;var observed=new State146(saved.Input with{Time=at,NextReview=at},saved.CrankOmega);
        var model=new Model146(rider.Spec,frame,surface,parameters);var points=new List<FreeRacingPoint>();
        for(int k=0;k<5;k++){
            var transition=model.Step(observed,k==0?first:second,k==4?.1:.2);observed=transition.End;points.AddRange(transition.Prediction.Points.Skip(k==0?0:1));
        }
        double xy=0,speed=0,yaw=0,wheel=0;int count=0;double predictedGap=points.Min(p=>Math.Min(p.Inner,p.Outer));
        foreach(var point in points){var sample=native.Riders[0].Samples.MinBy(s=>Math.Abs(s.Input.Time-point.Time))!;if(Math.Abs(sample.Input.Time-point.Time)>1e-7)continue;
            xy=Math.Max(xy,Vector2.Distance(point.Rider.Position,sample.Input.Rider.Position));speed=Math.Max(speed,Math.Abs(point.Rider.Speed-sample.Input.Rider.Speed));
            yaw=Math.Max(yaw,Math.Abs(point.Rider.AngularVelocity-sample.Input.Rider.AngularVelocity));wheel=Math.Max(wheel,Math.Abs(point.Omega-sample.Input.Omega));count++;
        }
        agreement=new{Points=count,MaximumXYMeters=xy,MaximumSpeedMps=speed,MaximumYawRadiansPerSecond=yaw,MaximumWheelOmega=wheel,PredictedMinimumGap=predictedGap,
            Passed=count>=80&&xy<.03&&speed<.03&&yaw<.01&&wheel<.1,model.Forecasts,model.Contacts};
    }
    var result=new{Mode=mode,native.Outcome,native.EndSeconds,native.Fingerprint,HistoricalFingerprint=run.Fingerprint,PrefixExact=prefixExact,
        NativePrefixFingerprint=Hash(nativePrefix),HistoricalPrefixFingerprint=Hash(oldPrefix),Agreement=agreement,
        MinimumInner=native.Riders[0].Samples.Min(s=>s.Inner),MinimumOuter=native.Riders[0].Samples.Min(s=>s.Outer),
        CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,Scope="Diagnostic forced pair / exact tape replay; not a new autonomous controller."};
    checks.Add(result);reservation["Complete"]=true;reservation["CpuSeconds"]=result.CpuSeconds;Save("budget.json",ledger);Save(mode+"-result.json",result);
    Console.WriteLine(JsonSerializer.Serialize(result,json));if(!prefixExact)throw new IOException("Native prefix mismatch; no new planner may be implemented.");
}
Save("sequence-checks.json",new{NativeWorlds=3,Checks=checks});
