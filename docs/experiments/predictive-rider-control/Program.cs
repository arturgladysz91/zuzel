using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;
using CoreSim.PhysicalSpace;

if(args.Length==2&&args[1]=="responses"){CandidateResponseAudit.Run(args[0]);return;}
if(args.Length==2&&args[1]=="prediction"){PredictionAudit.Run(args[0]);return;}
if(args.Length==2&&args[1]=="decisions"){DecisionAudit.Run(args[0]);return;}
if(args.Length==2&&args[1] is "baseline" or "candidate"){PredictiveRiderAudit.Run(args[0],args[1]=="candidate");return;}
var output=Path.GetFullPath(args.Single());Directory.CreateDirectory(output);
var json=new JsonSerializerOptions{WriteIndented=true};
var protocol=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,"protocol.json")));
var times=protocol.RootElement.GetProperty("ObservationTimes").Deserialize<double[]>()!;
var rows=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous");
var plans=DynamicAxleLoadAudit.ReadSaved("../continuous-corner-planning/continuous-plan");
var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;
var saved=ContinuousCornerAudit.SavedEnd();var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
var next=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,
    new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
var records=new List<object>();double maximumForceError=0;
foreach(var at in times){
    var index=Array.FindIndex(rows,r=>Math.Abs(r["t"]-at)<1e-8);var r=rows[index];
    var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)plans[index]["target"]};
    var path=at<10.15?initial:next;
    TwoContactForces Query(WorldParameters parameters)=>TwoContactBikeDynamics.Forces(v,spec,frame,surface,parameters,r["t"],147,
        new(r["omega_before"]*.33),new((float)r["steer_before"]),(float)r["dt"],path.Guidance(v,p,r["t"]),inertia:52.72097f);
    var a=Query(p);var b=Query(p with{EnableReachableTravelDirectionSteering=true});
    foreach(var pair in new[]{(a.FrontForce.X,r["front_fx"]),(a.FrontForce.Y,r["front_fy"]),(a.RearForce.X,r["rear_fx"]),(a.RearForce.Y,r["rear_fy"]),
        (a.SteeringRequest,r["steer_request"]),(a.SteeringAfter,r["steer_after"])})maximumForceError=Math.Max(maximumForceError,Math.Abs(pair.Item1-pair.Item2));
    var after=PhysicalSteeringAudit.View(rows[index+1],frame);
    double Score(TwoContactForces f){var m=TravelDirectionState.Observe(v,f.NetForce,147);var desired=f.Demand/Math.Max(v.Speed,.5f);
        var q=v.AngularVelocity+p.SlipYawResponseSeconds*f.NetTorque/52.72097f-m.ChiDot;
        return Math.Pow(m.ChiDot-desired,2)+q*q;}
    object Contact(TwoContactForces f)=>new{f.SteeringRequest,f.SteeringBefore,f.SteeringUsed,f.SteeringAfter,f.FrontForce,f.RearForce,
        f.FrontLateral,f.RearLateral,f.RearLongitudinal,f.NetTorque,f.Demand,Motion=TravelDirectionState.Observe(v,f.NetForce,147),
        ProjectedBetaRate=v.AngularVelocity+p.SlipYawResponseSeconds*f.NetTorque/52.72097f-TravelDirectionState.Observe(v,f.NetForce,147).ChiDot,
        LocalScore=Score(f),f.TravelSteering!.RateLimited,FrontUtilization=Math.Abs(f.FrontLateral)/f.TyreLoads!.FrontCapacityNewtons,
        RearUtilization=Math.Sqrt((double)f.RearLateral*f.RearLateral+(double)f.RearLongitudinal*f.RearLongitudinal)/f.TyreLoads.RearCapacityNewtons};
    var chi=Math.Atan2(v.Velocity.Y,v.Velocity.X);var afterChi=Math.Atan2(after.Velocity.Y,after.Velocity.X);
    records.Add(new{Time=r["t"],Dt=r["dt"],Drive=v.Drive,Before=v,Original=Contact(a),Reachable=Contact(b),
        NativeChiRate=BikeAngles.Wrap(afterChi-chi)/r["dt"],NativeYawRate=BikeAngles.Wrap(after.Heading-v.Heading)/r["dt"],
        NativeBetaRate=BikeAngles.Wrap(after.SlideRadians-v.SlideRadians)/r["dt"],NormalError=plans[index]["normal_error"],
        LocalScoreReduction=Score(a)-Score(b)});
}
if(maximumForceError>1e-7)throw new InvalidOperationException("Native #123 reconstruction differs.");
File.WriteAllText(Path.Combine(output,"steering-audit.json"),JsonSerializer.Serialize(new{BaseHead="d8d7e959eab00d0b41236aa6292ab04f3c15f3fa",
    NewWorlds=0,NativeSteps=0,MaximumNativeReplayError=maximumForceError,Records=records},new JsonSerializerOptions(json){IncludeFields=true})+"\n");
Console.WriteLine($"A audit: {records.Count} original states; exact force/steering error {maximumForceError:R}; no native world.");
