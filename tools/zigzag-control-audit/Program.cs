using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

// Offline analysis only: no WorldRunner, native step, body write or new controller.
var output=Path.GetFullPath(args.Single());Directory.CreateDirectory(output);
var root=Path.GetFullPath(Path.Combine(output,"../../.."));var prior=Path.Combine(root,"docs/experiments/predictive-rider-control");
var json=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
T Read<T>(string path){using var file=File.OpenRead(path);if(path.EndsWith(".gz")){using var zip=new GZipStream(file,CompressionMode.Decompress);return JsonSerializer.Deserialize<T>(zip,json)!;}return JsonSerializer.Deserialize<T>(file,json)!;}
void Save(string name,object value){var path=Path.Combine(output,name);if(File.Exists(path))throw new IOException("Audit evidence already exists: "+path);File.WriteAllText(path,JsonSerializer.Serialize(value,json)+"\n");}
void Zip(string name,object value){var path=Path.Combine(output,name);if(File.Exists(path))throw new IOException("Audit evidence already exists: "+path);using var file=File.Create(path);using var zip=new GZipStream(file,CompressionLevel.SmallestSize);JsonSerializer.Serialize(zip,value,json);}
var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;var saved=ContinuousCornerAudit.SavedEnd();var p=ContinuousCornerAudit.Parameters();
var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var spec=CornerEntryAudit.Rider();
var next=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
PreparedCornerPlan Plan(double at)=>at<10.15?initial:next;
var candidateRows=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/controlled/candidate");
var candidatePlans=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/controlled/candidate-plan");
var candidateReviews=Read<PreparedCornerReview[]>(Path.Combine(prior,"controlled/candidate-reviews.json.gz"));
using var candidateRun=JsonDocument.Parse(File.ReadAllText(Path.Combine(prior,"controlled/candidate.json")));
var candidateCommands=candidateRun.RootElement.GetProperty("Result").GetProperty("Decisions").Deserialize<DecisionEvent[]>(json)!;
var decisions=Read<PredictiveSteeringDecision[]>(Path.Combine(prior,"controlled/candidate-predictions.json.gz"));
var activations=Read<PredictiveSteeringActivation[]>(Path.Combine(prior,"controlled/candidate-steering-activations.json"));
PredictiveRiderInput Input(Dictionary<string,double>[] rows,Dictionary<string,double>[] plans,PreparedCornerReview[] reviews,DecisionEvent[] commands,double at){
    var i=Array.FindIndex(rows,r=>Math.Abs(r["t"]-at)<1e-8);if(i<0)throw new InvalidOperationException("No accepted native state.");
    var r=rows[i];var review=reviews.Last(q=>q.Time<=at+1e-8);
    var pending=commands.FirstOrDefault(c=>c.ObservedAt<=at+1e-8&&c.RequestedActivation>at+1e-8&&c.CancelledAt is null);
    return new(PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)plans[i]["target"]},r["t"],r["omega_before"],(float)r["steer_before"],
        new((float)review.Drive,review.Time+.1),pending is null?null:pending with{ActualActivation=null});
}
double Error(RiderView v,PreparedCornerPlan plan){var q=plan.Path.At(v.TotalReferenceMeters);return MeterPoint.Dot(new(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y),q.Outward);}
double Chi(RiderView v)=>Math.Atan2(v.Velocity.Y,v.Velocity.X);
object Summary(PredictiveRiderResult forecast,PreparedCornerPlan plan,float correction,float previous){
    double tracking=0,heading=0;var points=forecast.Points;var first=points[0];var last=points[^1];var horizon=last.Time-first.Time;
    foreach(var (before,point) in points.Zip(points.Skip(1))){var dt=point.Time-before.Time;var q=plan.Path.At(point.Rider.TotalReferenceMeters);
        tracking+=dt*Math.Pow(Error(point.Rider,plan),2);heading+=dt*Math.Pow(2.1*BikeAngles.Wrap(Chi(point.Rider)-q.Heading),2);}
    tracking/=horizon;heading/=horizon;
    var growth=Math.Pow(2.1*Math.Max(0,Math.Abs(last.Rider.SlideRadians)-Math.Abs(first.Rider.SlideRadians)),2);
    var progress=last.Rider.TotalReferenceMeters-first.Rider.TotalReferenceMeters;var progressCost=-PredictiveRiderControl.ProgressWeightMeters*progress;
    var reversal=previous*correction<0?PredictiveRiderControl.ReversalCostSquareMeters:0;
    return new{Tracking=tracking,Heading=heading,EndBetaGrowth=growth,Progress=progressCost,Reversal=reversal,Total=tracking+heading+growth+progressCost+reversal,
        ReferenceProgress=progress,DistanceXY=forecast.DistanceMeters,SpeedChange=last.Rider.Speed-first.Rider.Speed,MinimumSpeed=points.Min(q=>q.Rider.Speed),
        PeakAbsBeta=points.Max(q=>Math.Abs(q.Rider.SlideRadians)),FinalAbsBeta=Math.Abs(last.Rider.SlideRadians),InitialNormalError=Error(first.Rider,plan),FinalNormalError=Error(last.Rider,plan),
        PeakAbsTangentError=points.Max(q=>Math.Abs(BikeAngles.Wrap(Chi(q.Rider)-plan.Path.At(q.Rider.TotalReferenceMeters).Heading))),
        IntegratedBetaSquared=points.Zip(points.Skip(1)).Sum(q=>(q.Second.Time-q.First.Time)*q.Second.Rider.SlideRadians*q.Second.Rider.SlideRadians),
        PeakAbsYawRate=points.Max(q=>Math.Abs(q.Rider.AngularVelocity)),forecast.MinimumInner,forecast.MinimumOuter,forecast.MaximumFrontUtilization,forecast.MaximumRearUtilization,forecast.MaximumSteeringRate,
        forecast.ContactQueries,forecast.ForceEvaluations};
}
var matrix=new List<object>();var forecasts=new List<object>();var nativeValidation=new List<object>();var defaultEquality=new List<object>();
var originalModelHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"tools/shared-world-speedway/PredictiveRiderModel.cs"))));
Save("attempt.json",new{BaseHead="1b3c43c4dcb54b5a925912ecf75c48edf1e58f4d",NativeWorlds=0,NoRetuning=true,ModelSourceSha256=originalModelHash,ProtocolSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(output,"protocol.json"))))});
float previous=0;var watchAll=Stopwatch.StartNew();
foreach(var decision in decisions.Where(d=>d.Queued)){
    var input=Input(candidateRows,candidatePlans,candidateReviews,candidateCommands,decision.ObservedAt);var plan=Plan(input.Time);
    var record=new List<object>();
    foreach(var horizon in new[]{.6,.9})foreach(var (label,correction,bias) in new[]{("A",0f,false),("B+",PredictiveRiderControl.CorrectionRadians,false),("B-",-PredictiveRiderControl.CorrectionRadians,false),("C-",-PredictiveRiderControl.CorrectionRadians,true)}){
        var timer=Stopwatch.StartNew();
        var prediction=PredictiveRiderModel.Predict(input,spec,plan.ForkForPrediction(),surface,p,147,52.72097f,horizon,new(true),correction==0?null:correction,diagnosticRequestBias:bias);timer.Stop();
        var summary=Summary(prediction,plan,correction,previous);record.Add(new{Horizon=horizon,Variant=label,Summary=summary,Milliseconds=timer.Elapsed.TotalMilliseconds});
        forecasts.Add(new{ObservedAt=input.Time,Horizon=horizon,Variant=label,Prediction=prediction});
        if(horizon==.6&&!bias){var stored=decision.Candidates.Single(c=>c.CorrectionRadians==correction).Prediction;
            double position=0,steering=0,drive=0;foreach(var point in prediction.Points){var q=stored.Points.First(x=>Math.Abs(x.Time-point.Time)<1e-8);
                position=Math.Max(position,Vector2.Distance(q.Rider.Position,point.Rider.Position));steering=Math.Max(steering,Math.Abs(q.Steering-point.Steering));drive=Math.Max(drive,Math.Abs(q.Rider.Drive-point.Rider.Drive));}
            defaultEquality.Add(new{ObservedAt=input.Time,Variant=label,Position=position,Steering=steering,Drive=drive});
        }
        if(label=="B-"){
            var until=activations.Where(a=>a.Time>decision.Activation+1e-8).Select(a=>a.Time).DefaultIfEmpty(double.PositiveInfinity).Min();
            double ep=0,ev=0,eb=0,es=0;int compared=0;
            foreach(var point in prediction.Points.Where(q=>q.Time<until-1e-8)){
                var i=Array.FindIndex(candidateRows,r=>Math.Abs(r["t"]-point.Time)<1e-7);if(i<0)continue;var r=candidateRows[i];
                ep=Math.Max(ep,Vector2.Distance(point.Rider.Position,new((float)r["x"],(float)r["y"])));ev=Math.Max(ev,Math.Abs(point.Rider.Speed-r["speed"]));
                eb=Math.Max(eb,Math.Abs(BikeAngles.Wrap(point.Rider.SlideRadians-r["beta"])));es=Math.Max(es,Math.Abs(point.Steering-r["steer_before"]));compared++;
            }
            nativeValidation.Add(new{ObservedAt=input.Time,Horizon=horizon,ValidBeforeNextUnmodelledActivation=double.IsFinite(until)?(double?)until:null,Position=ep,Speed=ev,BetaRadians=eb,SteeringRadians=es,Compared=compared});
        }
    }
    matrix.Add(new{decision.ObservedAt,decision.Activation,decision.CorrectionRadians,Input=input,StoredCosts=decision.Candidates.Select(c=>new{c.CorrectionRadians,c.Score,c.Safe}),Comparisons=record});
    previous=decision.CorrectionRadians;
}
watchAll.Stop();
Save("execution-and-cost.json",new{NewNativeWorlds=0,ModelMaximumHorizon=.9,MatrixMilliseconds=watchAll.Elapsed.TotalMilliseconds,Records=matrix,DefaultEquality=defaultEquality,SelectedNativePrefixValidation=nativeValidation,
    CIsOfflineOnly=true,FutureAdditionalSteeringDecisions="Not supplied; after this single pulse each comparison resumes #117. Later recorded activations are only used to bound retrospective validation, never forecast inputs."});
Zip("offline-forecasts.json.gz",forecasts);
// Re-evaluate the original 63-forecast native gate with the extended model's DEFAULT mode.
var baselineRows=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/baseline");var baselinePlans=DynamicAxleLoadAudit.ReadSaved("../predictive-rider-control/baseline-plan");
var baselineReviews=Read<PreparedCornerReview[]>(Path.Combine(prior,"baseline-reviews.json.gz"));
using var baselineRun=JsonDocument.Parse(File.ReadAllText(Path.Combine(prior,"baseline.json")));
var baselineCommands=baselineRun.RootElement.GetProperty("Result").GetProperty("Decisions").Deserialize<DecisionEvent[]>(json)!;
using var originalGate=JsonDocument.Parse(File.ReadAllText(Path.Combine(prior,"prediction-validation.json")));
var validation=new List<object>();
foreach(var row in originalGate.RootElement.GetProperty("Records").EnumerateArray()){
    var at=row.GetProperty("ObservedAt").GetDouble();var horizon=row.GetProperty("Horizon").GetDouble();
    var input=Input(baselineRows,baselinePlans,baselineReviews,baselineCommands,at);
    var prediction=PredictiveRiderModel.Predict(input,spec,Plan(at).ForkForPrediction(),surface,p,147,52.72097f,horizon,new(true));
    double ep=0,ev=0,ec=0,eb=0,es=0,ew=0;int n=0;
    foreach(var point in prediction.Points){var i=Array.FindIndex(baselineRows,r=>Math.Abs(r["t"]-point.Time)<1e-7);if(i<0)continue;var r=baselineRows[i];
        ep=Math.Max(ep,Vector2.Distance(point.Rider.Position,new((float)r["x"],(float)r["y"])));ev=Math.Max(ev,Math.Abs(point.Rider.Speed-r["speed"]));
        ec=Math.Max(ec,Math.Abs(BikeAngles.Wrap(Chi(point.Rider)-r["velocity_heading"]))*180/Math.PI);eb=Math.Max(eb,Math.Abs(BikeAngles.Wrap(point.Rider.SlideRadians-r["beta"]))*180/Math.PI);
        es=Math.Max(es,Math.Abs(point.Steering-r["steer_before"])*180/Math.PI);ew=Math.Max(ew,Math.Abs(point.Omega-r["omega_before"]));n++;
    }
    validation.Add(new{ObservedAt=at,Horizon=horizon,Position=ep,Speed=ev,ChiDegrees=ec,BetaDegrees=eb,SteeringDegrees=es,WheelOmega=ew,Compared=n,Passed=ep<=.1&&ev<=.2&&ec<=1&&eb<=1&&es<=1.5&&ew<=2});
}
Save("default-model-validation.json",new{NativeWorlds=0,Forecasts=63,Records=validation});
// Continuous native sequence: same accepted body/wheel/Drive, query A and C even where B bypasses #117.
var contacts=new List<object>();double replayError=0;int bypass=0;
for(var i=0;i<candidateRows.Length;i++){
    var r=candidateRows[i];if(r["t"]>11.3)break;
    var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)candidatePlans[i]["target"]};var plan=Plan(r["t"]);
    TwoContactForces Query(float? target)=>TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,r["t"],147,new(r["omega_before"]*.33),new((float)r["steer_before"]),(float)r["dt"],plan.Guidance(v,p,r["t"]),target,inertia:52.72097f);
    var original=Query(null);var active=activations.LastOrDefault(a=>a.Time<=r["t"]+1e-10&&r["t"]<a.Time+.1-1e-10);
    var actual=active is null?original:Query(active.TargetRadians);var bias=active is null?original:Query(Math.Clamp(original.SteeringRequest+active.CorrectionRadians,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians));
    if(active is not null)bypass++;
    foreach(var (a,b) in new[]{(actual.FrontForce.X,r["front_fx"]),(actual.FrontForce.Y,r["front_fy"]),(actual.RearForce.X,r["rear_fx"]),(actual.RearForce.Y,r["rear_fy"]),(actual.SteeringAfter,r["steer_after"])})replayError=Math.Max(replayError,Math.Abs(a-b));
    object Response(TwoContactForces f)=>new{f.SteeringRequest,f.SteeringAfter,f.SteeringUsed,f.FrontLateral,f.RearLateral,f.RearLongitudinal,f.NetTorque,Motion=TravelDirectionState.Observe(v,f.NetForce,147),Original117Called=f.TravelSteering is not null};
    contacts.Add(new{Time=r["t"],Dt=r["dt"],HardOverride=active is not null,Original=Response(original),Actual=Response(actual),Bias=Response(bias)});
}
if(replayError>1e-7)throw new InvalidOperationException("Native force reconstruction changed.");
Zip("same-state-contacts.json.gz",new{NativeWorlds=0,CounterfactualBodies="All three queries use the same accepted #126 body. Only Actual was executed; A/C queries here are not independent trajectories.",MaximumNativeReplayError=replayError,Suppressed117Intervals=bypass,Records=contacts});
Console.WriteLine($"Offline audit:10 events,80 rollouts +63 original validations; max native force replay error {replayError:R}; zero new native worlds.");
