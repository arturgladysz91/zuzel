using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record Sequence135(FreeRacingAction[] Actions,FreeLearningCandidate Evaluation,FreeRacingPrediction Prediction);
public sealed record Search135(FreeLearningDecision Baseline,Sequence135[] Two,Sequence135[] Three,int Forecasts,long Contacts,double CpuSeconds)
{
    public Sequence135 BestTwo=>MultiActionPlanner135.Rank(Two,Baseline.Action).First();
    public Sequence135 BestThree=>MultiActionPlanner135.Rank(Three,Baseline.Action).First();
}

/// <summary>PROVISIONAL, invoked only by the explicit diagnostic CLI. No native world or future
/// native observations are accepted here. All leaves have the original 0.9s horizon.</summary>
public static class MultiActionPlanner135
{
    public const double HorizonSeconds=.9;
    public static FreeLearningDecision Baseline(FreeRacingInput i,FreeLearningPolicy policy,RiderSpec spec,TrackFrame f,TrackStateSnapshot surface,WorldParameters p)
        =>FreeLearningController.Choose(i,policy,spec,f,surface,p,147,52.72097f,.9,true,true,false,false);

    public static FreeRacingPrediction Predict(FreeRacingInput i,FreeRacingAction[] actions,RiderSpec spec,TrackFrame f,TrackStateSnapshot surface,WorldParameters p)
    {
        if(actions.Length is <1 or >3)throw new ArgumentException("One to three causal decisions required.");
        foreach(var a in actions)a.Validate();
        // The existing predicted-observation callback owns queue ordering and full reaction.
        // Reissuing the last action at later reviews is physically equivalent to holding it.
        return FreeRacingModel.Predict(i,spec,f,surface,p,147,52.72097f,HorizonSeconds,causalPolicy:q=>
            (actions[Math.Min(actions.Length-1,(int)Math.Round((q.Time-i.Time)/FreeLearningController.ReviewSeconds))],0),reviewSeconds:FreeLearningController.ReviewSeconds);
    }

    // Literal #134 A utility and classes, evaluated at the sequence's endpoint. The action
    // preference remains attached to its first request. No new weight or sequence penalty.
    // This makes delayed expensive actions cheap in the utility: a diagnosed limitation,
    // never a claim that H09 certifies their autonomous native continuation.
    public static FreeLearningCandidate Evaluate(FreeRacingInput input,FreeLearningPolicy policy,TrackFrame frame,TrackStateSnapshot surface,
        FreeRacingPrediction prediction,FreeRacingAction action)
    {
        var end=prediction.Points[^1];var v=end.Rider;var clearance=prediction.Points.Min(q=>Math.Min(q.Inner,q.Outer));
        var closing=Vector2.Dot(v.Velocity,v.Road.Outward);
        var futureGap=Math.Min(end.Inner-Math.Max(0,-closing)*.24,end.Outer-Math.Max(0,closing)*.24);
        var safety=clearance<0?FreeActionSafety.Unsafe:clearance<.2||futureGap<.1?FreeActionSafety.Uncertain:FreeActionSafety.SafeInHorizon;
        var ahead=Math.Max(.1,frame.ReferenceSpeed(v.Road,v.Speed)*policy.PreviewSeconds);
        var road=frame.RoadAt(v.TotalReferenceMeters+ahead,v.Road.OffsetMeters);
        var chi=Math.Atan2(v.Velocity.Y,v.Velocity.X);var error=BikeAngles.Wrap(road.Heading-chi);
        var desiredYaw=v.Speed*(road.IsTurn&&double.IsFinite(road.RadiusMeters)?1/road.RadiusMeters:0);
        var progress=v.TotalReferenceMeters-input.Rider.TotalReferenceMeters;
        var lane=Math.Clamp((float)v.Road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[v.Road.Segment].Type,frame.Track.Geometry)*4,0,4);
        var capacity=LoadBasedTyreGrip.Budget(147,surface.SampleSurface(v.Road.Segment,lane),TwoContactBikeParameters.Default,null).TotalCapacityNewtons/147;
        var speedEnvelope=desiredYaw>1e-8?Math.Sqrt(.8*capacity/(desiredYaw/Math.Max(v.Speed,.5))):100;
        var scoredProgress=progress*FreeLearningController.HorizonSeconds/HorizonSeconds;
        var score=-policy.ProgressWeight*scoredProgress+policy.DirectionWeight*error*error*10
            +policy.YawWeight*Math.Pow(v.AngularVelocity-desiredYaw,2)*10
            +policy.ThrottleWeight*Math.Pow(action.Drive-policy.Drive,2)
            +policy.SteeringChangeWeight*Math.Pow((action.SteeringRadians-input.Active.SteeringRadians)*180/Math.PI,2)
            +policy.GeometrySpeedWeight*Math.Pow(Math.Max(0,v.Speed-speedEnvelope),2)
            +10*Math.Pow(Math.Max(0,.2-futureGap),2);
        if(prediction.Points.Any(q=>Math.Abs(q.Rider.SlideRadians)>Math.PI/4))safety=FreeActionSafety.Unsafe;
        if(safety==FreeActionSafety.Unsafe)score+=10000*Math.Pow(Math.Max(.001,-clearance),2);
        return new(action,safety,score,progress,clearance,futureGap,v.Speed,v.SlideRadians,error);
    }

    public static IOrderedEnumerable<Sequence135> Rank(IEnumerable<Sequence135> rows,FreeRacingAction active)=>rows
        .OrderBy(x=>x.Evaluation.Safety).ThenBy(x=>x.Evaluation.Score)
        .ThenBy(x=>Math.Abs(x.Actions[0].SteeringRadians-active.SteeringRadians)).ThenBy(x=>x.Actions[0].SteeringRadians)
        .ThenBy(x=>string.Join(";",x.Actions.Select(a=>$"{a.SteeringRadians:R}/{a.Drive:R}")),StringComparer.Ordinal);

    public static FreeRacingAction[] Roots(FreeRacingInput input,FreeLearningDecision baseline)
    {
        var ranked=baseline.Candidates.OrderBy(c=>c.Safety).ThenBy(c=>c.Score).ThenBy(c=>Math.Abs(c.Action.SteeringRadians-input.Active.SteeringRadians)).ThenBy(c=>c.Action.SteeringRadians).ToArray();
        var list=new[]{baseline.Action,ranked.First(c=>c.Action.Drive==0).Action,ranked.First(c=>c.Action.Drive==1).Action,input.Active}.Distinct().ToList();
        foreach(var c in ranked.OrderByDescending(c=>Math.Abs(c.Action.SteeringRadians-baseline.Action.SteeringRadians)))if(list.Count<4&&!list.Contains(c.Action))list.Add(c.Action);
        return list.Take(4).ToArray();
    }
    public static FreeRacingAction[] Continuations(FreeRacingInput input,FreeLearningPolicy policy,FreeRacingAction prior,int count)
    {
        var angles=FreeLearningController.Actions(input,policy,true).Select(a=>a.SteeringRadians).Distinct().Order().ToArray();
        var lower=angles.Where(a=>a<prior.SteeringRadians-1e-7).DefaultIfEmpty(angles[0]).Max();
        var upper=angles.Where(a=>a>prior.SteeringRadians+1e-7).DefaultIfEmpty(angles[^1]).Min();
        // Diverse candidates, not a prescribed driving sequence: every leaf is scored.
        return new[]{prior,new FreeRacingAction(lower,prior.Drive==0?1:0),new FreeRacingAction(upper,prior.Drive==1?0:1)}.Distinct().Take(count).ToArray();
    }
    public static Search135 Search(FreeRacingInput input,FreeLearningPolicy policy,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p)
    {
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var a=Baseline(input,policy,spec,frame,surface,p);
        int forecasts=a.Forecasts;long contacts=a.ForceEvaluations;var two=new List<Sequence135>();var three=new List<Sequence135>();
        Sequence135 Leaf(FreeRacingAction[] actions){var q=Predict(input,actions,spec,frame,surface,p);forecasts++;contacts+=q.ForceEvaluations;return new(actions,Evaluate(input,policy,frame,surface,q,actions[0]),q);}
        foreach(var first in Roots(input,a))foreach(var second in Continuations(input,policy,first,3)){
            var pair=Leaf(new[]{first,second});two.Add(pair);
            foreach(var third in Continuations(input,policy,second,2))three.Add(Leaf(new[]{first,second,third}));
        }
        return new(a,two.ToArray(),three.ToArray(),forecasts,contacts,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds);
    }
}
