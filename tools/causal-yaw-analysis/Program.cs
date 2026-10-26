using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using SharedWorldSpeedway;

if(args.Length!=3||args[0]!="--read-native")throw new ArgumentException("Read-only metrics, no simulation: --read-native <raw> <evidence>");
var raw=Path.GetFullPath(args[1]);var evidence=Path.GetFullPath(args[2]);var destination=Path.Combine(evidence,"native-metrics");Directory.CreateDirectory(destination);
var started=Process.GetCurrentProcess().TotalProcessorTime;var files=new List<object>();long diagnosticQueries=0;
foreach(var file in Directory.GetFiles(raw,"*.json.gz").Order()){
    var name=Path.GetFileName(file)[..^8];var output=Path.Combine(destination,name+".json.gz");
    var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    if(File.Exists(output))continue;
    var run=RobustLearningExperiment.ReadEpisode(raw,name);var frame=TrackAwarenessExperiment134.Frame(run.GeometryName!);var policy=run.Replay.Policy;
    var telemetry=run.Telemetry.ToDictionary(t=>Math.Round(t.Time,8));
    var rows=run.Replay.Samples.Select(s=>{
        var v=s.Input.Rider;telemetry.TryGetValue(Math.Round(s.Input.Time,8),out var interval);
        var desired=CausalYawPlanner137.DesiredYaw(v,policy,frame);
        var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);var preview=frame.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
        return new{Time=s.Input.Time,DesiredYaw=desired,YawRate=v.AngularVelocity,YawError=v.AngularVelocity-desired,ChiDegrees=Math.Atan2(v.Velocity.Y,v.Velocity.X)*180/Math.PI,PsiDegrees=v.Heading*180/Math.PI,
            PreviewReference=v.TotalReferenceMeters+ahead,PreviewSegment=preview.Segment,PreviewRadius=double.IsFinite(preview.RadiusMeters)?preview.RadiusMeters:(double?)null,
            TravelCurvature=interval is null?(double?)null:TwoContactForces.Cross(v.Velocity,interval.Forces.NetForce)/(147*Math.Pow(Math.Max(.1,v.Speed),3)),
            TangentAcceleration=interval is null?(double?)null:Vector2.Dot(v.Velocity,interval.Forces.NetForce)/(147*Math.Max(.1,v.Speed)),
            FrontFx=interval is null?(double?)null:Vector2.Dot(interval.Forces.FrontForce,interval.Forces.BodyForward),
            ForceIntervalStart=interval is null?(double?)null:interval.Time-interval.Seconds,ForceIntervalSeconds=interval?.Seconds};
    }).ToArray();
    using(var zip=new GZipStream(File.Create(output),CompressionLevel.SmallestSize))JsonSerializer.Serialize(zip,new{NativeRawSha256=hash,WorldsAdded=0,
        Basis="DesiredYaw uses the original read-only TrackFrame/geometry-preview formula at each accepted native state. Forces/curvature/acceleration use the last completed native interval; no force queries or new dynamics.",Rows=rows},FreeLearningExperiment.Json);
    files.Add(new{Name=name,NativeRawSha256=hash,Samples=rows.Length});
}
foreach(var file in Directory.GetFiles(evidence,"offline-*.json.gz").Order()){
    using var input=new GZipStream(File.OpenRead(file),CompressionMode.Decompress);using var document=JsonDocument.Parse(input);
    var state=document.RootElement.GetProperty("State").Deserialize<YawState137>(FreeLearningExperiment.Json)!;
    var search=document.RootElement.GetProperty("Search").Deserialize<YawSearch137>(FreeLearningExperiment.Json)!;
    var rows=new List<object>();
    foreach(var group in search.Pairs.Where(x=>x.Actions[0].Drive==1).GroupBy(x=>x.Actions[0])){
        var corrected=CausalYawPlanner137.Rank(group,state.Input).First();var held=search.Held.Single(x=>x.Actions[0]==group.Key);
        var heldTrace=CausalYawExperiment137.Trace(state,held);var correctedTrace=CausalYawExperiment137.Trace(state,corrected);diagnosticQueries+=heldTrace.Length+correctedTrace.Length;
        rows.Add(new{First=group.Key,Held=held,Corrected=corrected,HeldTrace=heldTrace,CorrectedTrace=correctedTrace,
            Rule="Same first full-throttle action/steering; choose second from the frozen bounded suffix set by original terminal137B score, before any native continuation. Analysis only, not an autonomous outcome."});
    }
    using var zip=new GZipStream(File.Create(Path.Combine(evidence,"full-root-traces-"+state.Name+".json.gz")),CompressionLevel.SmallestSize);
    JsonSerializer.Serialize(zip,new{State=state,Rows=rows},FreeLearningExperiment.Json);
}
RobustLearningExperiment.Save(evidence,"native-metrics-cost.json",new{CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-started).TotalSeconds,NativeWorlds=0,NativeMetricForceQueries=0,DiagnosticForecastStateQueries=diagnosticQueries,Files=files,
    AnalysisSourceSha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes("tools/causal-yaw-analysis/Program.cs")))});
