using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

// Eight interpretable preferences, no coordinates, offset target or command tape.
public sealed record FreeLearningPolicy(double PreviewSeconds=.5, double DirectionWeight=.25, double YawWeight=.05,
    double Drive=.15, double ThrottleWeight=.2, double ProgressWeight=1, double SteeringChangeWeight=.01, double GeometrySpeedWeight=.1)
{
    public double[] Values => new[]{PreviewSeconds,DirectionWeight,YawWeight,Drive,ThrottleWeight,ProgressWeight,SteeringChangeWeight,GeometrySpeedWeight};
    public static readonly string[] Names={"PreviewSeconds","DirectionWeight","YawWeight","Drive","ThrottleWeight","ProgressWeight","SteeringChangeWeight","GeometrySpeedWeight"};
    public static readonly double[] Lower={0,0,0,.01,0,.2,0,0}, Upper={1.5,4,2,.8,4,2,.2,1};
    public static FreeLearningPolicy From(double[] x){
        if(x.Length!=8||x.Where((v,i)=>!double.IsFinite(v)||v<Lower[i]||v>Upper[i]).Any())throw new ArgumentException("Eight bounded driving preferences required.");
        return new(x[0],x[1],x[2],x[3],x[4],x[5],x[6],x[7]);
    }
}
public enum FreeActionSafety { SafeInHorizon, Uncertain, Unsafe }
public sealed record FreeLearningCandidate(FreeRacingAction Action,FreeActionSafety Safety,double Score,double Progress,
    double Clearance,double FutureClearance,double ExitSpeed,double ExitBeta,double DirectionError);
public sealed record FreeLearningDecision(double Time,FreeRacingAction Action,FreeActionSafety Safety,bool NoSafeContinuation,
    FreeLearningCandidate[] Candidates,int ForceEvaluations,int Forecasts);

public sealed class FreeLearningController
{
    public const double ReviewSeconds=.2, HorizonSeconds=.6;
    public FreeLearningPolicy Policy {get;}
    public FreeRacingAction Active {get;private set;}=new(0,1);
    public double NextReview {get;private set;}
    private readonly List<FreeRacingCommand> pending=new();
    public List<FreeLearningDecision> Decisions {get;}=new();
    public List<FreeRacingCommand> Activations {get;}=new();
    public long Evaluations {get;private set;}
    public long Forecasts {get;private set;}
    // Experiment-only intervention at an observation, still through the same reaction queue and actuator.
    internal Func<FreeRacingInput,FreeLearningDecision>? DiagnosticChooser {get;set;}
    public FreeLearningController(FreeLearningPolicy? policy=null){Policy=policy??new();FreeLearningPolicy.From(Policy.Values);}
    public FreeRacingInput Input(RiderView v,double time,double omega,float steering)=>new(v,time,omega,steering,Active,pending.ToArray(),NextReview);
    public double Deadline=>pending.Count>0?Math.Min(NextReview,pending[0].ActivationAt):NextReview;
    public void Advance(RiderView v,double time,double omega,float steering,RiderSpec spec,TrackFrame frame,
        TrackStateSnapshot surface,WorldParameters p,float mass,float inertia){
        while(pending.Count>0&&pending[0].ActivationAt<=time+1e-10){Active=pending[0].Action;Activations.Add(pending[0]);pending.RemoveAt(0);}
        if(time<NextReview-1e-10)return;
        var input=Input(v with{Drive=Active.Drive},time,omega,steering);
        var decision=DiagnosticChooser?.Invoke(input)??Choose(input,Policy,spec,frame,surface,p,mass,inertia);
        decision.Action.Validate();
        Decisions.Add(decision);Evaluations+=decision.ForceEvaluations;Forecasts+=decision.Forecasts;
        pending.Add(new(time,time+RiderController.ReactionSeconds(spec),decision.Action));NextReview+=ReviewSeconds;
    }
    public static FreeRacingAction[] Actions(FreeRacingInput input,FreeLearningPolicy policy){
        var angles=new[]{-15f,-5f,-1.5f,-.5f,0f,.5f,1.5f,5f,15f}.Select(d=>d*MathF.PI/180)
            .Append(input.Steering).Append(input.Active.SteeringRadians).Distinct();
        return angles.SelectMany(a=>new[]{new FreeRacingAction(a,0),new FreeRacingAction(a,(float)policy.Drive)})
            .Append(input.Active).Distinct().ToArray();
    }
    public static FreeLearningDecision Choose(FreeRacingInput input,FreeLearningPolicy policy,RiderSpec spec,TrackFrame frame,
        TrackStateSnapshot surface,WorldParameters p,float mass,float inertia,double horizonSeconds=HorizonSeconds){
        if(!double.IsFinite(horizonSeconds)||horizonSeconds<.3||horizonSeconds>FreeRacingModel.MaximumHorizonSeconds)throw new ArgumentException("Bounded audit horizon required.");
        var candidates=new List<FreeLearningCandidate>();var evaluations=0;var forecasts=0;
        foreach(var action in Actions(input,policy)){
            var prediction=FreeRacingModel.Predict(input,spec,frame,surface,p,mass,inertia,horizonSeconds,action);
            evaluations+=prediction.ForceEvaluations;forecasts++;
            var end=prediction.Points[^1];var v=end.Rider;var clearance=prediction.Points.Min(q=>Math.Min(q.Inner,q.Outer));
            var closing=Vector2.Dot(v.Velocity,v.Road.Outward);
            var futureGap=Math.Min(end.Inner-Math.Max(0,-closing)*.24,end.Outer-Math.Max(0,closing)*.24);
            var safety=clearance<0?FreeActionSafety.Unsafe:clearance<.2||futureGap<.1?FreeActionSafety.Uncertain:FreeActionSafety.SafeInHorizon;
            var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);
            var road=frame.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
            var chi=Math.Atan2(v.Velocity.Y,v.Velocity.X);var error=BikeAngles.Wrap(road.Heading-chi);
            var desiredYaw=v.Speed*(road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0);
            var progress=v.TotalReferenceMeters-input.Rider.TotalReferenceMeters;
            // Geometry supplies orientation and a hazard estimate, never lateral destination.
            var lane=Math.Clamp((float)v.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[v.Road.Segment].Type,frame.Track.Geometry)*4,0,4);
            var capacity=LoadBasedTyreGrip.Budget(mass,surface.SampleSurface(v.Road.Segment,lane),TwoContactBikeParameters.Default,null).TotalCapacityNewtons/mass;
            var speedEnvelope=desiredYaw>1e-8?Math.Sqrt(.8*capacity/(desiredYaw/Math.Max(v.Speed,.5))):100;
            var score=-policy.ProgressWeight*progress+policy.DirectionWeight*error*error*10
                +policy.YawWeight*Math.Pow(v.AngularVelocity-desiredYaw,2)*10
                +policy.ThrottleWeight*Math.Pow(action.Drive-policy.Drive,2)
                +policy.SteeringChangeWeight*Math.Pow((action.SteeringRadians-input.Active.SteeringRadians)*180/Math.PI,2)
                +policy.GeometrySpeedWeight*Math.Pow(Math.Max(0,v.Speed-speedEnvelope),2)
                +10*Math.Pow(Math.Max(0,.2-futureGap),2);
            // Beta is not a zero-slip target. Only departure from the inherited control envelope is unsafe.
            if(prediction.Points.Any(q=>Math.Abs(q.Rider.SlideRadians)>Math.PI/4))safety=FreeActionSafety.Unsafe;
            if(safety==FreeActionSafety.Unsafe)score+=10000*Math.Pow(Math.Max(.001,-clearance),2);
            candidates.Add(new(action,safety,score,progress,clearance,futureGap,v.Speed,v.SlideRadians,error));
        }
        var chosen=candidates.OrderBy(c=>c.Safety).ThenBy(c=>c.Score).ThenBy(c=>Math.Abs(c.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(c=>c.Action.SteeringRadians).First();
        return new(input.Time,chosen.Action,chosen.Safety,candidates.All(c=>c.Safety!=FreeActionSafety.SafeInHorizon),candidates.ToArray(),evaluations,forecasts);
    }
}
