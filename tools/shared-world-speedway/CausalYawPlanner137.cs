using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record YawLeaf137(FreeRacingAction[] Actions,FreeLearningCandidate Evaluation,FreeRacingPrediction Prediction,
    double TerminalYawCost,double MeanYawCost,double ThrottleCost,double SteeringCost,FreeRacingInput? SecondObservation=null);
public sealed record YawSearch137(Dictionary<string,YawLeaf137> Best,YawLeaf137[] Held,YawLeaf137[] Pairs,
    int Forecasts,long Contacts,double CpuSeconds,int Roots,int MaximumCorrections);

/// <summary>Explicit experiment only. A finite two-request lookahead, never future native data.
/// The original predictor owns all integration and queue semantics; no new physical model.</summary>
public static class CausalYawPlanner137
{
    public const double Horizon=.9,Review=.2;
    public const int RootLimit=5,CorrectionLimit=7;
    public static double DesiredYaw(RiderView v,FreeLearningPolicy policy,TrackFrame f){
        var ahead=Math.Max(.1,f.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);
        var preview=f.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
        return v.Speed*(preview.IsTurn&&double.IsFinite(preview.RadiusMeters)?1/preview.RadiusMeters:0);
    }
    public static double YawCost(FreeRacingPrediction prediction,FreeLearningPolicy policy,TrackFrame frame,bool integrated){
        double Error(FreeRacingPoint q)=>q.Rider.AngularVelocity-DesiredYaw(q.Rider,policy,frame);
        if(!integrated)return policy.YawWeight*Math.Pow(Error(prediction.Points[^1]),2)*10;
        double sum=0;foreach(var (a,b) in prediction.Points.Zip(prediction.Points.Skip(1)))
            sum+=(b.Time-a.Time)*(Math.Pow(Error(a),2)+Math.Pow(Error(b),2))/2;
        return policy.YawWeight*10*sum/(prediction.Points[^1].Time-prediction.Points[0].Time);
    }
    public static (double Throttle,double Steering) ActionCosts(FreeRacingInput input,FreeLearningPolicy policy,FreeRacingAction[] actions,double reaction=.24){
        if(actions.Length is <1 or >2||reaction<=0||reaction+Review>=Horizon)throw new ArgumentException("One/two commands within the frozen horizon.");
        var first=actions[0];double gas=policy.ThrottleWeight*Math.Pow(first.Drive-policy.Drive,2);
        double change=policy.SteeringChangeWeight*Math.Pow((first.SteeringRadians-input.Active.SteeringRadians)*180/Math.PI,2);
        if(actions.Length==1)return(gas,change);
        // Normalize over controllable exposure [.24,.9]. Identical channel requests coalesce,
        // so hold->hold reproduces the one-action utility exactly rather than getting a discount.
        var second=actions[1];var w=Review/(Horizon-reaction);
        if(second.Drive!=first.Drive)gas=w*gas+(1-w)*policy.ThrottleWeight*Math.Pow(second.Drive-policy.Drive,2);
        if(second.SteeringRadians!=first.SteeringRadians)change=w*change+(1-w)*policy.SteeringChangeWeight*Math.Pow((second.SteeringRadians-first.SteeringRadians)*180/Math.PI,2);
        return(gas,change);
    }
    public static YawLeaf137 Evaluate(FreeRacingInput input,FreeLearningPolicy policy,TrackFrame frame,TrackStateSnapshot surface,
        FreeRacingPrediction prediction,FreeRacingAction[] actions,bool integrated,FreeRacingInput? second=null){
        var old=MultiActionPlanner135.Evaluate(input,policy,frame,surface,prediction,actions[0]);
        var terminal=YawCost(prediction,policy,frame,false);var mean=YawCost(prediction,policy,frame,true);
        var original=ActionCosts(input,policy,new[]{actions[0]});var cost=ActionCosts(input,policy,actions);
        var score=actions.Length==1&&!integrated?old.Score:old.Score-terminal-original.Throttle-original.Steering+(integrated?mean:terminal)+cost.Throttle+cost.Steering;
        return new(actions,old with{Score=score},prediction,terminal,mean,cost.Throttle,cost.Steering,second);
    }
    public static IOrderedEnumerable<YawLeaf137> Rank(IEnumerable<YawLeaf137> leaves,FreeRacingInput input)=>leaves
        .OrderBy(x=>x.Evaluation.Safety).ThenBy(x=>x.Evaluation.Score)
        .ThenBy(x=>Math.Abs(x.Actions[0].SteeringRadians-input.Active.SteeringRadians)).ThenBy(x=>x.Actions[0].SteeringRadians)
        .ThenBy(x=>string.Join(";",x.Actions.Select(a=>$"{a.SteeringRadians:R}/{a.Drive:R}")),StringComparer.Ordinal);

    public static FreeRacingAction[] Corrections(FreeRacingInput predicted,FreeRacingAction first,FreeLearningPolicy policy,TrackFrame frame){
        var angles=FreeLearningController.Actions(predicted,policy,true).Select(a=>a.SteeringRadians).Append(first.SteeringRadians).Distinct().Order().ToArray();
        var desired=DesiredYaw(predicted.Rider,policy,frame);
        var sign=first.SteeringRadians!=0?Math.Sign(first.SteeringRadians):Math.Sign(desired-predicted.Rider.AngularVelocity);
        if(sign==0)sign=1;
        float Nearest(double target)=>angles.OrderBy(x=>Math.Abs(x-target)).ThenBy(x=>x).First();
        var magnitude=Math.Abs(first.SteeringRadians);
        var smaller=Nearest(first.SteeringRadians*.5);
        var greater=Nearest(sign*Math.Max(1.5*Math.PI/180,magnitude*2));
        var opposite=Nearest(-sign*Math.Max(.5*Math.PI/180,Math.Min(magnitude,1.5*Math.PI/180)));
        // These are candidates derived from the predicted observation/pending first request.
        // A pure suffix forecast, not this ordering, chooses the actual correction.
        return new[]{first,new FreeRacingAction(smaller,1),new(greater,first.Drive),new(opposite,1),
            new(opposite,(float)policy.Drive),new(opposite,0),new(Nearest(0),first.Drive)}.Distinct().Take(CorrectionLimit).ToArray();
    }
    public static FreeRacingInput AfterPrefix(FreeRacingInput input,FreeRacingPrediction prefix,FreeRacingAction first,RiderSpec spec){
        var end=prefix.Points[^1];var reaction=RiderController.ReactionSeconds(spec);
        if(Math.Abs(end.Time-input.Time-Review)>1e-8||reaction<=Review)throw new ArgumentException("Common prefix ends before the first new activation.");
        var active=input.Active;
        foreach(var c in input.Pending.OrderBy(x=>x.ActivationAt).Where(x=>x.ActivationAt<=end.Time+1e-10))active=c.Action;
        var pending=input.Pending.Where(x=>x.ActivationAt>end.Time+1e-10).Append(new FreeRacingCommand(input.Time,input.Time+reaction,first)).OrderBy(x=>x.ActivationAt).ToArray();
        return new(end.Rider with{Drive=active.Drive},end.Time,end.Omega,end.Steering,active,pending,end.Time);
    }
    public static FreeRacingPrediction Join(FreeRacingPrediction prefix,FreeRacingPrediction suffix)=>new(
        prefix.Points.SkipLast(1).Concat(suffix.Points).ToArray(),prefix.ForceEvaluations+suffix.ForceEvaluations,prefix.DistanceMeters+suffix.DistanceMeters);

    public static YawSearch137 Search(FreeRacingInput input,FreeLearningPolicy policy,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,bool pairs=true){
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var held=new List<YawLeaf137>();var pairLeaves=new List<YawLeaf137>();int forecasts=0;long contacts=0;
        foreach(var action in FreeLearningController.Actions(input,policy,true)){
            var prediction=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,Horizon,action);forecasts++;contacts+=prediction.ForceEvaluations;
            held.Add(Evaluate(input,policy,frame,surface,prediction,new[]{action},false));
        }
        var a=Rank(held,input).First();var c=Rank(held.Select(x=>Evaluate(input,policy,frame,surface,x.Prediction,x.Actions,true)),input).First();
        var best=new Dictionary<string,YawLeaf137>{{"A",a},{"C",c}};int rootCount=0,maxCorrections=0;
        if(pairs){
            var roots=new[]{a.Actions[0],c.Actions[0],Rank(held.Where(x=>x.Actions[0].Drive==0),input).First().Actions[0],
                Rank(held.Where(x=>x.Actions[0].Drive==1),input).First().Actions[0],input.Active}.Distinct().Take(RootLimit).ToArray();rootCount=roots.Length;
            // Before .24 no candidate first action can change the physical state. Compute .2 once.
            var prefix=FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,Review,input.Active);forecasts++;contacts+=prefix.ForceEvaluations;
            foreach(var first in roots){
                var q=AfterPrefix(input,prefix,first,spec);var options=Corrections(q,first,policy,frame);maxCorrections=Math.Max(maxCorrections,options.Length);
                foreach(var second in options){
                    var suffix=FreeRacingModel.Predict(q,spec,frame,surface,p,147,52.72097f,Horizon-Review,second);forecasts++;contacts+=suffix.ForceEvaluations;
                    pairLeaves.Add(Evaluate(input,policy,frame,surface,Join(prefix,suffix),new[]{first,second},false,q));
                }
            }
            best["B"]=Rank(pairLeaves,input).First();best["D"]=Rank(pairLeaves.Select(x=>Evaluate(input,policy,frame,surface,x.Prediction,x.Actions,true,x.SecondObservation)),input).First();
        }
        return new(best,held.ToArray(),pairLeaves.ToArray(),forecasts,contacts,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,rootCount,maxCorrections);
    }
    public static FreeLearningDecision Decision(FreeRacingInput input,YawSearch137 search,string arm){
        var best=search.Best[arm];
        // The decision's executed request is only the first action. At the next real review
        // the causal prototype replans from that then-current native observation, never a future one.
        var candidates=new[]{best.Evaluation};
        return new(input.Time,best.Actions[0],best.Evaluation.Safety,best.Evaluation.Safety!=FreeActionSafety.SafeInHorizon,candidates,
            checked((int)search.Contacts),search.Forecasts);
    }
    public static FreeRacingPrediction ReferencePair(FreeRacingInput input,FreeRacingAction[] actions,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,
        Action<FreeRacingInput>? secondObserved=null)=>FreeRacingModel.Predict(input,spec,frame,surface,p,147,52.72097f,Horizon,
            causalPolicy:q=>{var first=q.Time<input.Time+Review-1e-8;if(!first&&Math.Abs(q.Time-input.Time-Review)<1e-8)secondObserved?.Invoke(q);
                return(actions[first?0:1],0);},reviewSeconds:Review);
}
