using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Efficient147;
using Efficient152;
using LearnedCorner166;
using Safety153;
using SharedWorldSpeedway;
using WholeCorner171;

namespace Cyclic172;

internal sealed record Short174(string Name, FreeRacingAction Action, bool Legal, double Sweep, double Beta, int Queries);
internal sealed record Path174(string Kind, bool Reached, string? Failure, double Seconds, double Goal, double Sweep,
    double Pose, double Beta, int Reviews, int Forecasts, int Queries, double CpuSeconds, FreeRacingPoint[] Points);
internal sealed record Admission174(double Time, string StateSha256, double Reference, double CrankOmega,
    FreeRacingAction Historical, FreeRacingAction Proposed, bool OldCanPrepare, string HistoricalGate,
    bool Admitted, string Reason, double CpuSeconds, double Milliseconds, Short174[] Short,
    Path174? BaselinePath, Path174? ProposedPath);

internal sealed class StateFeasibility174(Context174 context, RiderSpec spec)
{
    private readonly TrackFrame frame=context.Frame;
    private readonly TrackStateSnapshot surface=MultiActionExperiment135.Surface(context.Frame);
    private readonly WorldParameters parameters=FreeLearningExperiment.Parameters();
    private readonly Boundary152 boundary=new(context.Frame);
    private readonly PredictionBoundary147 cache=new(context.Frame);
    private const float Mass=147, Inertia=52.72097f;
    private const double ModelMargin=.65, MaximumSeconds=15, MinimumPredictedGain=.005;
    private readonly CyclicReference172 targets=new(context.A,context.B,context.Weights,spec,context.Frame);

    public Admission174 Evaluate(FreeRacingInput input, CrankClutch144 shaft, CyclicDecision172 old,
        CyclicController172 afterHistoricalChoice)
    {
        context.Guard();double cpu=Study172.Cpu;long clock=Stopwatch.GetTimestamp();
        string hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{input,CrankOmega=shaft.Omega,spec},Context174.Json)));
        var next=targets.Map.Locate(input.Rider.TotalReferenceMeters).Next;
        var activation=Predict(input,shaft,.24,null).Points[^1].Rider;
        double speed=activation.Speed+3.5*.2;
        double remaining=Math.Max(0,next.Entry-activation.TotalReferenceMeters-speed*.2);
        bool oldGate=speed*speed<=Math.Pow(targets.FlyingEntrySpeed+context.Tuning.SpeedHeadroom,2)+2*.65*remaining;
        var shortlist=new List<Short174>();
        foreach(var item in new[]{("historical",old.Action),("plus20pp",old.Action with{Drive=Math.Min(.94f,old.Action.Drive+.2f)}),
            ("95-fixed",old.Action with{Drive=.95f}),("100-fixed",old.Action with{Drive=1f})})
        {
            var path=Predict(input,shaft,.44,item.Item2);var safe=Safety(path.Points);
            shortlist.Add(new(item.Item1,item.Item2,safe.Failure is null,safe.Sweep,safe.Beta,path.ForceEvaluations));
        }
        var proposed=shortlist.Single(x=>x.Name=="95-fixed");
        // Steering is an independent, small fallback factor, never an unrestricted optimizer.
        if(!proposed.Legal)foreach(float delta in new[]{-.01f,.01f})
        {
            float steer=Math.Clamp(old.Action.SteeringRadians+delta,-TwoContactBikeParameters.Default.MaximumSteeringRadians,TwoContactBikeParameters.Default.MaximumSteeringRadians);
            var action=new FreeRacingAction(steer,.95f);var path=Predict(input,shaft,.44,action);var safe=Safety(path.Points);
            shortlist.Add(new("95-coordinated-"+delta,action,safe.Failure is null,safe.Sweep,safe.Beta,path.ForceEvaluations));
        }
        if(!proposed.Legal)proposed=shortlist.Where(x=>x.Name.StartsWith("95-coordinated")&&x.Legal).MaxBy(x=>x.Sweep)??proposed;
        Path174? baseline=null,candidate=null;bool admitted=false;string reason;
        if(old.Action.Drive>=.95f)reason="historical already supplies 95 percent";
        else if(!proposed.Legal)reason="short prediction rejected";
        else
        {
            baseline=Continue(input,shaft,old.Action,afterHistoricalChoice,next.Exit+20,"historical");
            if(!baseline.Reached)reason="UNKNOWN: historical model continuation not established";
            else
            {
                candidate=Continue(input,shaft,proposed.Action,afterHistoricalChoice,next.Exit+20,"95-pulse");
                admitted=candidate.Reached&&candidate.Seconds+MinimumPredictedGain<baseline.Seconds;
                reason=!candidate.Reached?"whole-bend model rejected: "+candidate.Failure:admitted?
                    "admitted: causal whole-bend model predicts common-goal time gain":"rejected: no predicted common-goal time gain";
            }
        }
        return new(input.Time,hash,input.Rider.TotalReferenceMeters,shaft.Omega,old.Action,proposed.Action,oldGate,old.Tracking.Gate,
            admitted,reason,Study172.Cpu-cpu,Stopwatch.GetElapsedTime(clock).TotalMilliseconds,shortlist.ToArray(),baseline,candidate);
    }

    private Path174 Continue(FreeRacingInput initial,CrankClutch144 initialShaft,FreeRacingAction first,
        CyclicController172 source,double goal,string kind)
    {
        double began=Study172.Cpu;var controller=source.Fork174();controller.Applied174(first);
        var input=initial;var shaft=initialShaft.Copy();var action=first;
        var points=new List<FreeRacingPoint>();int reviews=0,forecasts=0,queries=0;
        double sweep=double.PositiveInfinity,pose=double.PositiveInfinity,beta=0,seconds=0;
        string? failure=null;bool reached=false;
        while(input.Time<initial.Time+MaximumSeconds-1e-8)
        {
            context.Guard();double crank=shaft.Omega;
            var prediction=Predict(input,shaft,.2,action,(_,rpm)=>crank=rpm);forecasts++;queries+=prediction.ForceEvaluations;
            var safety=Safety(prediction.Points);sweep=Math.Min(sweep,safety.Sweep);pose=Math.Min(pose,safety.Pose);beta=Math.Max(beta,safety.Beta);
            points.AddRange(points.Count==0?prediction.Points:prediction.Points.Skip(1));
            if(safety.Failure is not null){failure=safety.Failure;break;}
            int crossed=Array.FindIndex(prediction.Points,p=>p.Rider.TotalReferenceMeters>=goal);
            if(crossed>=0)
            {
                var b=prediction.Points[crossed];var a=prediction.Points[Math.Max(0,crossed-1)];
                double u=b.Rider.TotalReferenceMeters>a.Rider.TotalReferenceMeters?(goal-a.Rider.TotalReferenceMeters)/(b.Rider.TotalReferenceMeters-a.Rider.TotalReferenceMeters):1;
                seconds=a.Time+u*(b.Time-a.Time)-initial.Time;reached=true;break;
            }
            var end=prediction.Points[^1];
            var active=input.Pending.Where(c=>c.ActivationAt<=end.Time+1e-9).Select(c=>c.Action).Prepend(input.Active).Last();
            var pending=input.Pending.Where(c=>c.ActivationAt>end.Time+1e-9).Append(new FreeRacingCommand(input.Time,input.Time+.24,action)).ToArray();
            input=new(end.Rider,end.Time,end.Omega,end.Steering,active,pending,end.Time);
            shaft=new(initialShaft.Parameters,crank);
            var decision=controller.Choose(input,shaft);action=decision.Action;reviews++;
            forecasts+=decision.Forecasts;queries+=decision.Queries;
        }
        failure??=reached?null:"UNKNOWN: whole-bend model horizon exhausted";
        return new(kind,reached,failure,seconds,goal,sweep,pose,beta,reviews,forecasts,queries,Study172.Cpu-began,points.ToArray());
    }

    private FreeRacingPrediction Predict(FreeRacingInput input,CrankClutch144 shaft,double seconds,FreeRacingAction? action,Action<double,double>? observe=null)
    {
        context.Guard();return PredictorA153.Predict(input,spec,frame,surface,parameters,Mass,Inertia,seconds,candidate:action,
            powertrain:shaft,boundaryCache:cache,fastBoundary:boundary,exactInitialWheel:true,observeCrank:observe);
    }

    private static (string? Failure,double Sweep,double Pose,double Beta) Safety(FreeRacingPoint[] points)
    {
        double sweep=double.PositiveInfinity,pose=double.PositiveInfinity,beta=0;string? failure=null;
        for(int i=0;i<points.Length;i++)
        {
            var b=points[i];pose=Math.Min(pose,Math.Min(b.Inner,b.Outer));beta=Math.Max(beta,Math.Abs(b.Rider.SlideRadians));
            if(i>0){var a=points[i-1];sweep=Math.Min(sweep,Clearance153.SweepLowerBound(a.Rider,b.Rider,b.Time-a.Time,(a.Inner,a.Outer),(b.Inner,b.Outer)));}
            if(failure is null&&(pose<ModelMargin||sweep<ModelMargin||beta>Math.PI/4))failure=beta>Math.PI/4?"BETA45":"MODEL_SWEPT065";
        }
        return(failure,sweep,pose,beta);
    }
}

internal sealed class Controller174(Context174 context,RiderSpec spec)
{
    public readonly CyclicController172 Base=context.Controller(spec);
    private readonly StateFeasibility174 feasibility=new(context,spec);
    private readonly Dictionary<(int,int),int> attempts=[];
    private double retryAt=double.NegativeInfinity,committedUntil=double.NegativeInfinity;
    public List<Admission174> Admissions{get;}=[];
    public CyclicDecision172 Choose(FreeRacingInput input,CrankClutch144 crank,bool enabled=true)
    {
        double cpu=Study172.Cpu;long clock=Stopwatch.GetTimestamp();var old=Base.Choose(input,crank);
        var map=new BendMap(context.Frame);var where=map.Locate(input.Rider.TotalReferenceMeters);var visit=where.Next;
        double distance=visit.Entry-input.Rider.TotalReferenceMeters;
        if(!enabled||where.Current is not null||input.Rider.TotalReferenceMeters<map.At(0,0).Exit||distance is <8 or >45||
            old.Action.Drive>=.95f||input.Rider.Speed<12||input.Rider.TotalReferenceMeters<Math.Max(retryAt,committedUntil))return old;
        var key=(visit.Lap,visit.Index);if(attempts.GetValueOrDefault(key)>=3)return old;
        attempts[key]=attempts.GetValueOrDefault(key)+1;retryAt=input.Rider.TotalReferenceMeters+8;
        var evaluation=feasibility.Evaluate(input,crank,old,Base);Admissions.Add(evaluation);
        if(!evaluation.Admitted)return old with{CpuSeconds=Study172.Cpu-cpu,DecisionMilliseconds=Stopwatch.GetElapsedTime(clock).TotalMilliseconds};
        committedUntil=visit.Exit+20;Base.Applied174(evaluation.Proposed);
        return old with{Action=evaluation.Proposed,Proposal=evaluation.Proposed,Transition="174 admitted whole-bend MODEL; not native certificate",
            CpuSeconds=Study172.Cpu-cpu,DecisionMilliseconds=Stopwatch.GetElapsedTime(clock).TotalMilliseconds};
    }
}
