using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

// Read-only saved-state queries. No WorldRunner/ContactAdapter/native initialization,
// integration, commits, prediction rollout, or changes to the original contact functions.
var output=Path.GetFullPath(args.Single());Directory.CreateDirectory(output);
var initial=PreparedCornerAudit.InitialPlan();var frame=initial.Path.Frame;
var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
var p=ContinuousCornerAudit.Parameters();var spec=CornerEntryAudit.Rider();
var results=new List<object>();int queries=0;double maxError=0;
foreach(var item in new[]{("123","../continuous-corner-planning/continuous","../continuous-corner-planning/reviews.json",true),
    ("122A","../feasible-path-speed-planning/A","../feasible-path-speed-planning/A-reviews.json",false),
    ("122B","../feasible-path-speed-planning/B","../feasible-path-speed-planning/B-reviews.json",true)}){
    var rows=DynamicAxleLoadAudit.ReadSaved(item.Item2);
    var reviews=JsonSerializer.Deserialize<PreparedCornerReview[]>(File.ReadAllText(PhysicalSteeringAudit.PreviousPath(item.Item3)))!;
    float memory=1;
    foreach(var review in reviews){
        var r=rows.MinBy(row=>Math.Abs(row["t"]-review.Time))!;
        if(Math.Abs(r["t"]-review.Time)>1e-8)throw new InvalidOperationException("Missing exact accepted review state.");
        var v=PhysicalSteeringAudit.View(r,frame);
        var wheel=new RearWheelState(r["omega_before"]*.33);var steering=new TwoContactBikeState((float)r["steer_before"]);
        var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,review.Time,147,wheel,steering,0,steeringOverride:steering.SteeringRadians);queries++;
        maxError=Math.Max(maxError,Math.Max(Math.Abs(f.RearLongitudinal-review.ActualFx),Math.Abs(f.RearLateral-review.ActualFy)));
        var front=Math.Max(0,-Vector2.Dot(f.FrontForce,f.FrontVelocity));
        var rear=Math.Max(0,-f.RearLateral*Vector2.Dot(f.RearVelocity,f.BodyLeft));
        var drag=Math.Max(0,-Vector2.Dot(f.Resistance,v.Velocity));
        var available=f.Engine!.AvailableWheelTorqueNewtonMeters*wheel.AngularVelocityRadPerSecond;
        var sustain=(front+rear+drag)/Math.Max(1,available);
        var feedback=PreparedCornerPlan.SpeedFeedbackPerMeterPerSecond*(review.SpeedTarget-v.Speed);
        var unclamped=review.SpeedTarget>100?1:sustain+feedback;
        var request=Math.Clamp(unclamped,0,1);
        var spinGate=Math.Abs(review.Wheelspin)>.25;var betaGate=review.Beta*review.BetaDot>.05;
        var gated=spinGate||betaGate?0:request;
        var ramp=memory+PreparedCornerPlan.ThrottleRisePerSecond*PreparedCornerPlan.ReviewSeconds;
        var planned=(float)Math.Min(gated,ramp);
        if(item.Item4&&Math.Abs(planned-review.Drive)>1e-7)throw new InvalidOperationException("Throttle reconstruction disagrees with original saved decision.");
        results.Add(new{Run=item.Item1,review.Time,Speed=v.Speed,ActualDrive=v.Drive,PreviousMemory=memory,
            Sustain=sustain,Feedback=feedback,Unclamped=unclamped,Request=request,SpinGate=spinGate,BetaGate=betaGate,
            Gated=gated,RampLimited=gated>ramp,ReconstructedDrive=planned,RecordedDrive=review.Drive,
            AvailableWheelPower=available,FrontLossPower=front,RearLossPower=rear,ResistancePower=drag,
            LocalRearAlpha=Math.Atan2(Vector2.Dot(f.RearVelocity,f.BodyLeft),Vector2.Dot(f.RearVelocity,f.BodyForward)),
            Omega=wheel.AngularVelocityRadPerSecond,review.Wheelspin,review.Beta,review.BetaDot,review.ActualFx,review.ActualFy});
        memory=planned;
    }
}
if(maxError>1e-7)throw new InvalidOperationException("Original force replay differs.");
var saved=ContinuousCornerAudit.SavedEnd();
var next=new PreparedCornerPlan(saved.Rider,spec,frame,p,saved.Time,147,saved.Wheel,saved.Steering,null,surface,
    new(saved.Old.Path,saved.Old.ControlMemory,saved.Pending));
var knots=new List<object>();
foreach(var plan in new[]{initial,next})foreach(var station in new[]{plan.TransitionStartMeters,plan.TransitionEndMeters}){
    foreach(var h in new[]{.1,.01,.001}){
        var q=plan.Path.At(station);
        double Derivative(double a,double b)=>(plan.Path.At(b).Curvature-plan.Path.At(a).Curvature)/((b-a)*q.Metric);
        knots.Add(new{Station=station,H=h,Curvature=q.Curvature,
            LeftKPrime=Derivative(station-h,station-h/2),RightKPrime=Derivative(station+h/2,station+h)});
    }
}
var jsonOptions=new JsonSerializerOptions{WriteIndented=true};
File.WriteAllText(Path.Combine(output,"probe.json"),JsonSerializer.Serialize(new{NewWorlds=0,BodySteps=0,WheelCommits=0,ContactQueries=queries,
    MaximumSavedForceErrorNewtons=maxError,Reviews=results,Knots=knots},jsonOptions)+"\n");
Console.WriteLine($"Read-only PASS: {queries} original h0 queries; force error {maxError:R} N; zero worlds/steps/commits.");
