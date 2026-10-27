using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

if(args.Length!=2||args[0] is not ("--check-prefixes" or "--shared-world"))throw new ArgumentException("--check-prefixes|--shared-world <main-native-directory>");
var directory=Path.GetFullPath(args[1]);var json=FreeLearningExperiment.Json;var study=JsonNode.Parse(File.ReadAllText(Path.Combine(directory,"study.json")))!;
foreach(var item in study["Protocol"]!["Sources"]!.AsObject())if(Offline142.Hash(File.ReadAllText(item.Key))!=item.Value!.GetValue<string>())throw new IOException("Frozen main source changed.");
Run144 Read(string name){using var z=new GZipStream(File.OpenRead(Path.Combine(directory,name+".json.gz")),CompressionMode.Decompress);return JsonSerializer.Deserialize<Run144>(z,json)!;}
var sourceSha=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/standing144-validation/Program.cs")));
if(args[0]=="--shared-world"){
    if(Read("A").Outcome!="completed")throw new IOException("Complete solo baseline first.");
    var file=Path.Combine(directory,"shared-A.json.gz");var lockFile=Path.Combine(directory,"shared-lock.json");
    if(File.Exists(file)||File.Exists(lockFile))throw new IOException("No silent shared-world repeat.");
    File.WriteAllText(lockFile,JsonSerializer.Serialize(new{BeforeNativeWorld=true,SourceSha256=sourceSha,WorldsReserved=1,MainACompletedFirst=true,
        Gates=new[]{"A","B","C","D"},Variant="A",Scope="One common Box2D world; one clock, separate crank/wheel/steering/queue per rider, same accepted snapshot. Original solo H09 forecasts do not anticipate rivals. Stop whole capture on first failed rider; others are interrupted, not falsely classified as finished or DNF.",NoControllerOrPhysicsChange=true},json));
    Run144 run;var cpu=Process.GetCurrentProcess().TotalProcessorTime;
    using(var world=new StandingWorld144(new(Enabled:true),new[]{StartingGate.A,StartingGate.B,StartingGate.C,StartingGate.D}))run=world.Run("shared-A","A");
    using(var z=new GZipStream(File.Create(file),CompressionLevel.SmallestSize))JsonSerializer.Serialize(z,run,json);
    double minGap=double.PositiveInfinity;int penetrating=0;double? firstPenetration=null;
    PhysicalBikePose Pose(RiderView v)=>new(v.Id,"shared-world",new(v.Position.X,v.Position.Y),new(0,v.Speed>.01f?Math.Atan2(v.Velocity.Y,v.Velocity.X):v.Heading,v.Heading),SpeedwayBikeDimensions.Reference);
    for(int n=0;n<run.Riders[0].Samples.Length;n++)for(int i=0;i<run.Riders.Length;i++)for(int j=i+1;j<run.Riders.Length;j++){
        var a=run.Riders[i].Samples[n];var b=run.Riders[j].Samples[n];if(a.Input.Time!=b.Input.Time)throw new IOException("Shared clock differs.");
        var gap=MechanicalSeparation.Between(Pose(a.Input.Rider),Pose(b.Input.Rider)).SignedMeters;minGap=Math.Min(minGap,gap);
        if(gap<0){penetrating++;firstPenetration??=a.Input.Time;}
    }
    var result=new{run.Outcome,run.EndSeconds,Worlds=1,run.TotalCpuSeconds,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,
        ReservationCpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,run.Fingerprint,ContactBegins=run.Contacts.Count(c=>c.Kind=="begin"),
        TotalImpulseNewtonSeconds=run.Contacts.Where(c=>c.Kind=="begin").Sum(c=>c.ImpulseNewtonSeconds),MinimumNativeCapsuleGapMeters=minGap,NegativeGapSamples=penetrating,FirstNegativeGapTime=firstPenetration,
        Riders=run.Riders.Select(r=>new{Id=r.Spec.Profile.Id,Gate=r.Gate.ToString(),r.Outcome,r.CompletedLaps,r.FinishSeconds}),
        RawSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),RawBytes=new FileInfo(file).Length,
        Interpretation="Actual same-world rigid-body impulses, not four stitched solo traces. Negative capsule gap, if any, remains explicit solver overlap; it is not concealed by a zero tolerance claim. No traffic-aware prediction or post-crash management is established."};
    File.WriteAllText(Path.Combine(directory,"shared-summary.json"),JsonSerializer.Serialize(result,json));Console.WriteLine(JsonSerializer.Serialize(result,json));return;
}
{
    var cpu=Process.GetCurrentProcess().TotalProcessorTime;var checks=new List<object>();bool all=true;int forecasts=0;
    foreach(var name in new[]{"A","B","F","K1"}){
        var run=Read(name);var rider=run.Riders[0];var frame=StandingWorld144.Frame();var p=FreeLearningExperiment.Parameters();
        var eligible=rider.Decisions.Where(d=>d.Choice.Time<=run.EndSeconds-.4-1e-8).ToArray();
        var targets=new[]{0d,frame.SegmentStartMeters(1),frame.LapMeters+frame.SegmentStartMeters(1),2*frame.LapMeters+frame.SegmentStartMeters(1),2*frame.LapMeters+frame.SegmentStartMeters(4)+31};
        var probes=targets.Select(reference=>eligible.MinBy(d=>Math.Abs(rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-d.Choice.Time))!.Input.Rider.TotalReferenceMeters-reference))!).DistinctBy(d=>d.Choice.Time);
        foreach(var d in probes){
            var sample=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-d.Choice.Time))!;
            var input=sample.Input with{Time=d.Choice.Time,NextReview=d.Choice.Time,Pending=sample.Input.Pending.Where(q=>Math.Abs(q.ObservedAt-d.Choice.Time)>1e-8).ToArray()};
            var crankPoints=new Dictionary<double,double>();
            var prediction=Predictor144.Predict(input,rider.Spec,frame,MultiActionExperiment135.Surface(frame),p,147,52.72097f,.4,d.Choice.Action,
                powertrain:new CrankClutch144(omega:sample.CrankOmega),observeCrank:(t,w)=>crankPoints[Math.Round(t,8)]=w);forecasts++;
            double xy=0,speed=0,yaw=0,omega=0,crank=0,steering=0;int count=0;
            foreach(var point in prediction.Points){var native=rider.Samples.MinBy(s=>Math.Abs(s.Input.Time-point.Time))!;if(Math.Abs(native.Input.Time-point.Time)>1e-7)continue;
                xy=Math.Max(xy,Vector2.Distance(point.Rider.Position,native.Input.Rider.Position));speed=Math.Max(speed,Math.Abs(point.Rider.Speed-native.Input.Rider.Speed));
                yaw=Math.Max(yaw,Math.Abs(point.Rider.AngularVelocity-native.Input.Rider.AngularVelocity));omega=Math.Max(omega,Math.Abs(point.Omega-native.Input.Omega));
                crank=Math.Max(crank,Math.Abs(crankPoints[Math.Round(point.Time,8)]-native.CrankOmega));steering=Math.Max(steering,Math.Abs(point.Steering-native.Input.Steering));count++;
            }
            var passed=count>=39&&xy<.02&&speed<.02&&yaw<.005&&omega<.02&&crank<.2&&steering<.0002;all&=passed;
            checks.Add(new{Name=name,Time=d.Choice.Time,Reference=sample.Input.Rider.TotalReferenceMeters,Passed=passed,Points=count,XYMeters=xy,SpeedMetersPerSecond=speed,YawRadiansPerSecond=yaw,WheelOmega=omega,CrankOmega=crank,SteeringRadians=steering});
        }
    }
    var result=new{Passed=all,NativeWorlds=0,Forecasts=forecasts,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,SourceSha256=sourceSha,Checks=checks,
        Scope="First0.4s before a subsequent review could activate at+.44. Body, wheel, crank and steering compared at shared native timestamps, including start and laps2/3. This does not certify long prediction or empirical hardware accuracy."};
    File.WriteAllText(Path.Combine(directory,"predictor-check.json"),JsonSerializer.Serialize(result,json));Console.WriteLine(JsonSerializer.Serialize(new{all,forecasts}));
    if(!all)throw new IOException("Predictor/native mismatch; report before promotion.");
}
