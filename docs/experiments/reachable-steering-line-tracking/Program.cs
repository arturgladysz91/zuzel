using System.Numerics;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

// Pure replay: no WorldRunner, Box2D steps, Commit, or replacement contact law.
const string baseHead="8a218fc8cb8be3460c8e42116789ece8ff576498";
const float mass=147,inertia=52.72097f;
var dir=Path.GetFullPath(args[0]);Directory.CreateDirectory(dir);
var source=Path.GetFullPath(Path.Combine(dir,"../feasible-500cc-corner-entry"));
var rows=Read("combined");var controls=Read("combined-control");
var p=CornerEntryAudit.Parameters();var raw=p with{EnablePhysicalSteeringControl=false,EnableTravelDirectionSteering=false};
var frame=RacingLineRootCauseAudit.Frame();var spec=CornerEntryAudit.Rider();var geometry=TwoContactBikeParameters.Default;
var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
double maximumForceError=0,maximumSteeringError=0;var allQueries=0;var changed=0;var opposite=0;var worse=0;
using var writer=InverseFeasibilityAudit.Csv(dir,"saved-state-comparison.csv",
    "t,dt,s,target,offset,error,error_dot,chi,psi,beta,beta_dot,chi_dot,desired_chi_dot,path_chi_dot,demand_ff,demand_error,demand_damping,steer_request,steer_before,steer_after,steer_used,rate_limited,front_fy,rear_fy,rear_fx,budget,required_normal,actual_normal,full_min_normal,full_max_normal,reachable_min_normal,reachable_max_normal,reachable_after,reachable_used,reachable_chi_dot,reachable_beta_dot,old_beta_forecast,reachable_beta_forecast,old_score,reachable_score,front_util,rear_util,required_curvature,actual_curvature,reachable_queries,full_min_beta_forecast,full_max_beta_forecast,reachable_min_beta_forecast,reachable_max_beta_forecast");
foreach(var (r,index) in rows.Select((r,i)=>(r,i))){
    var v=PhysicalSteeringAudit.View(r,frame) with{TargetOffsetMeters=(float)controls[index]["target"]};
    var dt=(float)r["dt"];var before=(float)r["steer_before"];var wheel=new RearWheelState(r["omega_before"]*.33);var state=new TwoContactBikeState(before);
    var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,r["t"],mass,wheel,state,dt,inertia:inertia);
    maximumForceError=Math.Max(maximumForceError,new[]{Math.Abs(f.FrontForce.X-r["front_fx"]),Math.Abs(f.FrontForce.Y-r["front_fy"]),Math.Abs(f.RearForce.X-r["rear_fx"]),Math.Abs(f.RearForce.Y-r["rear_fy"])}.Max());
    maximumSteeringError=Math.Max(maximumSteeringError,Math.Abs(f.SteeringUsed-r["steer_used"]));
    if(state.SteeringRadians!=before||Math.Abs(wheel.AngularVelocityRadPerSecond-r["omega_before"])>1e-12)throw new Exception("Replay changed accepted state.");
    var d=f.TravelSteering!;var motion=TravelDirectionState.Observe(v,f.NetForce,mass);var queries=0;
    TwoContactForces Held(float used){queries++;return TwoContactBikeDynamics.Forces(v,spec,frame,surface,raw,r["t"],mass,new(r["omega_before"]*.33),new(used),dt,steeringOverride:used);}
    double Score(TwoContactForces value,out double chi,out double forecast){
        chi=TravelDirectionState.Observe(v,value.NetForce,mass).ChiDot;
        forecast=v.AngularVelocity+p.SlipYawResponseSeconds*value.NetTorque/inertia-chi;
        var lower=(-TravelDirectionSteering.ControlledSlideLimitRadians-motion.Beta)/p.SlipYawResponseSeconds;
        var upper=(TravelDirectionSteering.ControlledSlideLimitRadians-motion.Beta)/p.SlipYawResponseSeconds;
        var excess=forecast-Math.Clamp(forecast,lower,upper);
        return Math.Pow(chi-d.DesiredChiDot,2)+forecast*forecast+excess*excess;
    }
    var decision=ReachableTravelDirectionSteering.Decide(v,f.Demand,mass,inertia,p.SlipYawResponseSeconds,geometry,before,dt,Held);
    var best=decision.AngleRequestRadians;var minimum=decision.MinimumChiDot;var maximum=decision.MaximumChiDot;
    var minBeta=decision.MinimumProjectedBetaDot;var maxBeta=decision.MaximumProjectedBetaDot;
    var newChi=decision.PredictedChiDot;var newBeta=decision.PredictedBetaDot;
    // One extra diagnostic query evaluates the score; excluded from the controller's 51 queries.
    var reachable=Held((before+best)/2);var newScore=Score(reachable,out _,out _);var oldScore=Score(f,out _,out var oldBeta);
    if(newScore<oldScore-1e-9)changed++;if(newScore>oldScore+1e-9)worse++;
    if((f.SteeringAfter-before)*(best-before)<-1e-10)opposite++;
    var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*2*p.YawResponseSeconds);
    var preview=frame.RoadAt(v.TotalReferenceMeters+ahead,v.TargetOffsetMeters);var angle=BikeAngles.Wrap(preview.Heading-v.Road.Heading);
    var previewK=angle/(ahead+Math.Abs(angle)*Math.Max(0,v.Road.OffsetMeters));var along=Vector2.Dot(v.Velocity,v.Road.Tangent);
    var response=p.LateralResponseSeconds+SlipCoupledDynamics.OuterToYawResponseSeparation*p.SlipYawResponseSeconds;
    var error=v.Road.OffsetMeters-v.TargetOffsetMeters;var errorDot=Vector2.Dot(v.Velocity,v.Road.Outward);
    var laneK=frame.Shape!.At(v.TotalReferenceMeters).Curvature;laneK/=1+laneK*v.TargetOffsetMeters;
    InverseFeasibilityAudit.Row(writer,r["t"],dt,v.TotalReferenceMeters,v.TargetOffsetMeters,v.Road.OffsetMeters,error,errorDot,
        motion.Chi,motion.Psi,motion.Beta,motion.BetaDot,motion.ChiDot,d.DesiredChiDot,v.Speed*laneK,
        along*along*previewK,error/(response*response),2*errorDot/response,f.SteeringRequest,before,f.SteeringAfter,f.SteeringUsed,d.RateLimited?1:0,
        f.FrontLateral,f.RearLateral,f.RearLongitudinal,f.Budget,mass*v.Speed*v.Speed*laneK,mass*v.Speed*motion.ChiDot,
        mass*v.Speed*d.MinimumChiDot,mass*v.Speed*d.MaximumChiDot,mass*v.Speed*minimum,mass*v.Speed*maximum,
        best,(before+best)/2,newChi,v.AngularVelocity-newChi,oldBeta,newBeta,oldScore,newScore,
        Math.Abs(f.FrontLateral)/f.TyreLoads!.FrontCapacityNewtons,Math.Sqrt(f.RearLateral*f.RearLateral+f.RearLongitudinal*f.RearLongitudinal)/f.TyreLoads.RearCapacityNewtons,
        laneK,motion.ChiDot/v.Speed,decision.ContactQueries,d.MinimumProjectedBetaDot,d.MaximumProjectedBetaDot,minBeta,maxBeta);
    allQueries+=queries;
}
if(maximumForceError!=0||maximumSteeringError!=0)throw new Exception("Historical force replay differs.");
InverseFeasibilityAudit.Save(dir,"replay.json",new{BaseHead=baseHead,States=rows.Length,Worlds=0,WheelCommits=0,MaximumForceReplayDifferenceNewtons=maximumForceError,
    MaximumSteeringReplayDifferenceRadians=maximumSteeringError,ReachableControllerContactQueries=allQueries-rows.Length,ScoreDiagnosticContactQueries=rows.Length,
    OriginalReplayContactQueries=rows.Length*52,TotalContactQueries=allQueries+rows.Length*52,
    ImprovedLocalScoreStates=changed,WorseLocalScoreStates=worse,OppositeSteeringDirectionStates=opposite,
    Rule="Compare original #117 global held-angle search then rate clamp with the identical score searched over reachable ENDPOINTS and evaluated at their physical midpoint. Same 33+16 candidates, initial/final query; no changes to tyres, drive, prediction horizon or score weights. Later saved states remain the historical trajectory; local score is not closed-loop stability evidence."});
Console.WriteLine($"Replayed {rows.Length} states; force error {maximumForceError}; improved {changed}, worse {worse}, opposite {opposite}");
Dictionary<string,double>[] Read(string name){using var stream=File.OpenRead(Path.Combine(source,name+".csv.gz"));using var zip=new GZipStream(stream,CompressionMode.Decompress);using var reader=new StreamReader(zip);
    var keys=reader.ReadLine()!.Split(',');var result=new List<Dictionary<string,double>>();while(reader.ReadLine() is {} line)result.Add(line.Split(',').Select((s,i)=>(keys[i],double.Parse(s,CultureInfo.InvariantCulture))).ToDictionary(x=>x.Item1,x=>x.Item2));return result.ToArray();}
