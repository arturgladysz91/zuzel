using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;
public sealed record Variant143(string Id,string[] Changes,bool Recovery=false,bool NominalHierarchy=false);
public sealed record Choice143(FreeLearningDecision Decision,FreeLearningCandidate Original,double Score,Terms142 Terms,
    double PreparationCost,int ChangedCandidates,int HardFeasible,int Forecasts,long Contacts,string Reason,
    FreeRacingAction? ForecastRecovery=null,double? RecoveryObservedAt=null,int ViableRoots=0);
public sealed record Leaf143(Leaf142 Original,FreeRacingPrediction Forecast,double Score,FreeActionSafety Safety,double PreparationCost);
public sealed class Controller143
{
    public static FreeLearningPolicy Policy=>PhysicsExperiment139.Policy;
    readonly TrackFrame frame;readonly TrackStateSnapshot surface;readonly RiderSpec spec;readonly WorldParameters parameters;
    readonly (double Reference,double Curvature,double Heading)[] profile;
    public Controller143(NativeCase139 c){
        frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);surface=MultiActionExperiment135.Surface(frame);spec=FreeLearningExperiment.Rider(c.Case.Scenario);parameters=FreeLearningExperiment.Parameters();
        profile=Enumerable.Range(0,(int)Math.Ceiling(frame.LapMeters/4)).Select(i=>Math.Min(frame.LapMeters,i*4d))
            .Concat(frame.Track.Segments.Select((_,i)=>frame.SegmentStartMeters(i))).Distinct().Order().Select(s=>{
                var q=frame.RoadAt(s,0);return(s,q.IsTurn&&double.IsFinite(q.RadiusMeters)?1/q.RadiusMeters:0,(double)q.Heading);}).ToArray();
    }
    public Leaf143[] Evaluate(FreeRacingInput input,Variant143 variant){
        var forecasts=FreeLearningController.Actions(input,Policy,true).Select(action=>{
            var forecast=FreeRacingModel.Predict(input,spec,frame,surface,parameters,147,52.72097f,.9,action);
            return (Action:action,Forecast:forecast,Leaf:Objective142.FromPrediction(input,Policy,frame,surface,forecast,action));}).ToArray();
        return forecasts.Select(x=>{
            var coast=variant.Changes.Contains("H")?forecasts.First(q=>q.Action.SteeringRadians==x.Action.SteeringRadians&&q.Action.Drive==0).Forecast:null;
            var preparation=coast is null?0:Preparation(x.Forecast,coast);
            var (score,safety)=Score(x.Leaf,variant,preparation);
            return new Leaf143(x.Leaf,x.Forecast,score,safety,preparation);
        }).ToArray();
    }
    public static (double Score,FreeActionSafety Safety) Score(Leaf142 leaf,Variant143 variant,double preparation=0){
        var score=leaf.Candidate.Score;var t=leaf.Terms;var changes=variant.Changes;
        if(changes.Contains("B"))score-=t.Throttle;
        if(changes.Contains("C"))score-=t.Direction;
        if(changes.Contains("D"))score-=t.Yaw;
        if(changes.Contains("E"))score-=t.Steering;
        if(changes.Contains("F")||changes.Contains("H"))score-=t.Geometry;
        if(changes.Contains("H"))score+=preparation;
        var safety=leaf.Candidate.Safety;
        if(changes.Contains("G")){
            // Only caution margins and their soft charge change; no footprint/beta detector is removed.
            safety=leaf.Candidate.Clearance<0||leaf.MaximumBetaDegrees>45?FreeActionSafety.Unsafe:
                leaf.Candidate.Clearance<.05||leaf.Candidate.FutureClearance<.025?FreeActionSafety.Uncertain:FreeActionSafety.SafeInHorizon;
            score=score-t.Clearance-t.Unsafe+10*Math.Pow(Math.Max(0,.05-leaf.Candidate.FutureClearance),2)
                +(safety==FreeActionSafety.Unsafe?10000*Math.Pow(Math.Max(.001,-leaf.Candidate.Clearance),2):0);
        }
        return(score,safety);
    }
    public Leaf143[] Cached(FreeRacingInput input,Variant143 variant,YawLeaf137[] held)=>held.Select(x=>{
        var old=Objective142.FromPrediction(input,Policy,frame,surface,x.Prediction,x.Actions[0]);
        var coast=held.First(q=>q.Actions[0].SteeringRadians==x.Actions[0].SteeringRadians&&q.Actions[0].Drive==0);
        var preparation=variant.Changes.Contains("H")?Preparation(x.Prediction,coast.Prediction):0;
        var (score,safety)=Score(old,variant,preparation);return new Leaf143(old,x.Prediction,score,safety,preparation);
    }).ToArray();
    double Preparation(FreeRacingPrediction forecast,FreeRacingPrediction coast){
        var end=forecast.Points[^1];var rider=end.Rider;var a=coast.Points.First(q=>q.Time>=coast.Points[0].Time+.24-1e-8);var b=coast.Points[^1];
        var deceleration=Math.Max(0,(a.Rider.Speed-b.Rider.Speed)/Math.Max(.01,b.Time-a.Time));
        var lane=Math.Clamp((float)rider.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[rider.Road.Segment].Type,frame.Track.Geometry)*4,0,4);
        var capacity=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(rider.Road.Segment,lane),TwoContactBikeParameters.Default).TotalCapacityNewtons/147;
        var steeringTime=Math.Abs(end.Steering)/TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond;
        var referenceSpeed=Math.Max(.1,frame.ReferenceSpeed(rider.Road,rider.Speed));double residual=0;
        foreach(var point in profile.Where(q=>q.Curvature>1e-8)){
            var distance=(point.Reference-rider.Road.ReferenceMeters+frame.LapMeters)%frame.LapMeters;if(distance>90)continue;
            // Static known geometry, only the physically attained offset; no prospective outer teleport.
            var radius=1/point.Curvature+Math.Max(0,rider.Road.OffsetMeters);
            var hint=Math.Sqrt(capacity*radius); // Full force budget, no arbitrary .8 reserve.
            var available=Math.Max(0,distance/referenceSpeed-.24-steeringTime-parameters.LateralResponseSeconds-parameters.SlipYawResponseSeconds);
            residual=Math.Max(residual,Math.Max(0,rider.Speed-deceleration*available-hint));
        }
        return Policy.GeometrySpeedWeight*residual*residual;
    }
    static IOrderedEnumerable<Leaf143> Rank(IEnumerable<Leaf143> leaves,FreeRacingInput input,bool nominal)=>leaves
        .Where(l=>!nominal||l.Original.HardViolation is null).OrderBy(l=>nominal?0:(int)l.Safety).ThenBy(l=>l.Score)
        .ThenBy(l=>Math.Abs(l.Original.Candidate.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(l=>l.Original.Candidate.Action.SteeringRadians);
    public Choice143 Choose(FreeRacingInput input,Variant143 variant){
        var leaves=Evaluate(input,variant);var original=leaves.OrderBy(l=>l.Original.Candidate.Safety).ThenBy(l=>l.Original.Candidate.Score)
            .ThenBy(l=>Math.Abs(l.Original.Candidate.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(l=>l.Original.Candidate.Action.SteeringRadians).First();
        var selected=Rank(leaves,input,variant.NominalHierarchy).FirstOrDefault();
        var reason="global objective, fresh native observation";int forecasts=leaves.Length;long contacts=leaves.Sum(l=>(long)l.Forecast.ForceEvaluations);int viable=0;
        FreeRacingAction? recovery=null;double? recoveryAt=null;
        if(selected is null){selected=original;reason="no nominally admissible held candidate; original A fallback, no global impossibility claim";}
        if(variant.Recovery){
            var roots=leaves.GroupBy(l=>l.Original.Candidate.Action.Drive).Select(g=>Rank(g,input,false).First()).Append(original).DistinctBy(l=>l.Original.Candidate.Action).ToArray();
            var eligible=new List<(Leaf143 Leaf,FreeRacingAction Recovery)>();
            foreach(var root in roots){
                if(root.Original.HardViolation is not null)continue;
                var end=root.Forecast.Points.First(q=>Math.Abs(q.Time-input.Time-.8)<1e-7);
                var commands=input.Pending.Append(new FreeRacingCommand(input.Time,input.Time+.24,root.Original.Candidate.Action)).OrderBy(q=>q.ActivationAt).ToArray();
                var active=commands.Where(q=>q.ActivationAt<=end.Time+1e-10).Select(q=>q.Action).LastOrDefault(input.Active);
                var future=new FreeRacingInput(end.Rider with{Drive=active.Drive},end.Time,end.Omega,end.Steering,active,commands.Where(q=>q.ActivationAt>end.Time+1e-10).ToArray(),end.Time);
                var chi=Math.Atan2(end.Rider.Velocity.Y,end.Rider.Velocity.X);var ahead=Math.Max(.1,frame.ReferenceSpeed(end.Rider.Road,end.Rider.Speed)*Policy.PreviewSeconds);
                var preview=frame.RoadAt(end.Rider.TotalReferenceMeters+ahead,end.Rider.Road.OffsetMeters);
                var desired=end.Rider.Speed*(preview.IsTurn&&double.IsFinite(preview.RadiusMeters)?1/preview.RadiusMeters:0)+BikeAngles.Wrap(preview.Heading-chi)/Math.Max(.1,Policy.PreviewSeconds);
                TwoContactForces Query(float angle){contacts++;return TwoContactBikeDynamics.Forces(end.Rider with{Drive=0},spec,frame,surface,parameters,end.Time,147,
                    new RearWheelState(end.Omega*.33),new(end.Steering),0,steeringOverride:angle,inertia:52.72097f);}
                var inverse=PhysicalSteeringControl.Decide(end.Rider,(float)(end.Rider.Speed*desired),147,52.72097f,parameters.SlipYawResponseSeconds,
                    TwoContactBikeParameters.Default,end.Steering,.01f,Query);
                var angle=FreeLearningController.Actions(future,Policy,true).Select(a=>a.SteeringRadians).Distinct().OrderBy(a=>Math.Abs(a-inverse.AngleRequestRadians)).ThenBy(a=>a).First();
                var action=new FreeRacingAction(angle,0);var tail=FreeRacingModel.Predict(future,spec,frame,surface,parameters,147,52.72097f,.9,action);forecasts++;contacts+=tail.ForceEvaluations;
                if(Objective142.HardViolation(tail) is null)eligible.Add((root,action));
            }
            viable=eligible.Count;
            if(viable>0){selected=Rank(eligible.Select(q=>q.Leaf),input,variant.NominalHierarchy).FirstOrDefault()??original;
                var q=eligible.FirstOrDefault(q=>q.Leaf==selected);if(q.Leaf is not null){recovery=q.Recovery;recoveryAt=input.Time+.8;}
                reason="original held H09 plus finite causal recovery request at+.8, activation+1.04; checked to1.7s, not a promise or global viability certificate";
            }else{selected=original;reason="no checked viable gas root; original A fallback; bounded search is inconclusive";}
        }
        var c=selected.Original.Candidate with{Score=selected.Score,Safety=selected.Safety};
        var decision=new FreeLearningDecision(input.Time,c.Action,c.Safety,leaves.All(l=>l.Safety!=FreeActionSafety.SafeInHorizon),
            leaves.Select(l=>l.Original.Candidate with{Score=l.Score,Safety=l.Safety}).ToArray(),checked((int)contacts),forecasts);
        return new(decision,original.Original.Candidate,selected.Score,selected.Original.Terms,selected.PreparationCost,
            leaves.Count(l=>l.Score!=l.Original.Candidate.Score||l.Safety!=l.Original.Candidate.Safety),leaves.Count(l=>l.Original.HardViolation is null),forecasts,contacts,reason,recovery,recoveryAt,viable);
    }
}
