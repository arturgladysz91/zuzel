using System.Diagnostics;
using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Decision198(double Time,double S,FreeRacingAction Action,string Reason,int Version,
    double Milliseconds,double LocalSweep,double Reserve,int WorkerSteps,double PositionError,double CourseError,
    HeadingSample198 PredictedActivation,double? NextFullSeconds,double? AcceptedSweep);

internal sealed class Receding198(Predictor198 model,Calibration198 calibration,Seed198 seed,
    FreeRacingAction[] initial, double initialFinish,Reservations198? reservations=null,
    Func<RiderView,double,string?>? style=null,bool profile206=false)
{
    private const double LocalHorizon=.25;
    private const int BranchBudget=64;
    private readonly Random random=new(198);
    private FreeRacingAction[] incumbent=initial;
    private double finish=initialFinish;
    private int version,attempt,acceptedPatches;
    private Worker198? worker;
    private HeadingSample198? expectedActivation;
    internal int AcceptedPatches=>acceptedPatches;
    internal List<Decision198> Journal {get;}=[];
    internal List<PatchLog198> Patches {get;}=[];
    internal List<DecisionWork206> Work206 {get;}=[];
    internal RacingFailure206? LastStrictFailure206 {get;private set;}

    internal FreeRacingAction Choose(Start197 own,Func<HeadingSample198,bool>? peerCheck=null)
    {
        var timer=Stopwatch.StartNew();
        var cpu=profile206?Process.GetCurrentProcess().TotalProcessorTime:default;
        ForecastWork206 localWork=default,workerWork=default;
        long workerTicks=0;
        double t=own.State.Time;
        int index=(int)Math.Round((t-seed.Start.Review)/Predictor198.Review);
        if(index<0 || index>=incumbent.Length)
            throw new InvalidOperationException("#198 full continuation is no longer available.");
        double liveError=expectedActivation is null?0:PositionError(expectedActivation,own.State);
        double courseError=expectedActivation is null?0:CourseError(expectedActivation,own.State);
        double reserve=calibration.BaseMargin+liveError+Math.Abs(courseError)*.62;
        string reason="certified-continuation";
        double? acceptedSweep=null;
        if(worker is not null && index>=worker.Switch)
        {
            bool same=worker.Anchor is { } a && SameAnchor(a,own);
            if(index==worker.Switch && worker.Faster && same)
            {
                var local=Check(own,worker.Controls,index,reserve,peerCheck,out _,out bool open);
                if(profile206) localWork+=local.Work206;
                if(Accepted(local) && open && (reservations is null ||
                    reservations.TryPublish(own.State.Rider.Id,worker.ForecastTrace)))
                {
                    incumbent=worker.Controls;
                    finish=worker.Candidate!.GoalTime;
                    version++;
                    acceptedPatches++;
                    reason="verified-switch";
                    acceptedSweep=worker.Candidate.Sweep;
                }
            }
            Patches.Add(new(worker.CreatedAt,t,worker.Switch,reason=="verified-switch"?"adopted":
                    !worker.Completed?"expired":!same?"anchor-mismatch":"no-legal-time-gain",
                worker.Candidate?.Outcome,worker.Candidate is {Reached:true}?worker.Candidate.GoalTime:null,
                worker.Baseline is {Reached:true}?worker.Baseline.GoalTime:null,worker.TotalSteps,
                worker.Anchor is { } k?Vector2.Distance(k.State.Rider.Position,own.State.Rider.Position):null));
            worker=null;
        }

        var guard=Check(own,incumbent,index,reserve,peerCheck,out var activation,out bool unoccupied);
        if(profile206) localWork+=guard.Work206;
        if(!Accepted(guard) || !unoccupied)
        {
            LastStrictFailure206=guard.StrictFailure206;
            RecordCost(unoccupied?"no-safe-prefix":"occupied-prefix",0);
            Patches.Add(new(t,t,index,unoccupied?"no-safe-prefix":"occupied-prefix",guard.Outcome,null,null,guard.Steps,liveError));
            throw new InvalidOperationException($"#198 has no certified continuation at {t:F3} s ({(unoccupied?guard.Outcome:"occupied-corridor")}, swept {guard.Sweep:F6}).");
        }
        expectedActivation=activation;
        int steps=0;
        long workerStarted=profile206?Stopwatch.GetTimestamp():0;
        if(worker is null && finish-t>1.15)
            worker=StartWorker(own,index);
        int candidateCount=0;
        if(worker is not null)
        {
            var previousWork=profile206?worker.Work206:default;
            steps=worker.Advance(BranchBudget);
            candidateCount=worker.Candidate is null?1:2;
            if(profile206) workerWork=worker.Work206-previousWork;
        }
        if(profile206) workerTicks=Stopwatch.GetTimestamp()-workerStarted;
        RecordCost(reason,candidateCount);

        var action=incumbent[index];
        Journal.Add(new(t,own.State.Rider.TotalReferenceMeters,action,reason,version,timer.Elapsed.TotalMilliseconds,
            guard.Sweep,reserve,steps,liveError,courseError,activation,
            reason=="verified-switch"?finish-t:null,acceptedSweep));
        return action;

        void RecordCost(string outcome,int branches)
        {
            if(!profile206) return;
            var decisionCpu=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds;
            Work206.Add(new(t,own.State.Rider.TotalReferenceMeters,outcome,branches,timer.Elapsed.TotalMilliseconds,
                decisionCpu,workerTicks,localWork,workerWork));
        }

        static bool Accepted(Predictor198.Cursor198 trial) => trial.Outcome is null || trial.Reached;
    }

    private Predictor198.Cursor198 Check(Start197 own,FreeRacingAction[] actions,int start,double reserve,
        Func<HeadingSample198,bool>? peerCheck,out HeadingSample198 activation,out bool unoccupied)
    {
        var cursor=model.Begin(own,actions,start,seed.Goal,Predictor198.Clearance+reserve,profile:profile206,constraint:style);
        cursor.Advance(10,own.State.Time+Predictor198.Review);
        activation=cursor.Snapshot;
        unoccupied=true;
        if(peerCheck is null)
            cursor.Advance(30,own.State.Time+LocalHorizon);
        else
        {
            unoccupied=peerCheck(activation);
            for(int i=2;i<=5 && cursor.Outcome is null;i++)
            {
                cursor.Advance(10,own.State.Time+i*Predictor198.Review);
                unoccupied&=peerCheck(cursor.Snapshot);
            }
        }
        return cursor;
    }

    private Worker198? StartWorker(Start197 own,int from)
    {
        var left=finish-own.State.Time;
        // Certify the remainder in deterministic slices before the first changed request.
        int lead=(int)Math.Ceiling((2*left*100+48)/(2*BranchBudget))+3;
        int change=from+lead;
        int finishIndex=(int)Math.Floor((finish-seed.Start.Review)/Predictor198.Review);
        if(change+6>=Math.Min(finishIndex,incumbent.Length)) return null;
        int radius=(int)Math.Clamp((finishIndex-change)*.22,5,20);
        int core=change+radius;
        var steer=(random.NextDouble()*2-1)*.0028;
        var gas=(random.NextDouble()*2-1)*.10;
        if(attempt++%4==0)gas=0;
        else if(attempt%4==0)steer=0;
        var edits=Edit197.Apply(incumbent,[new(core,radius,steer,gas)]);
        double switchAt=seed.Start.Review+change*Predictor198.Review;
        return new(model,own,incumbent,edits,from,change,switchAt,seed.Goal,
            .50+Math.Ceiling((calibration.BaseMargin+calibration.PositionTolerance+
                .85*calibration.CourseTolerance)*200)/200,reservations is not null,style,profile206);
    }

    private bool SameAnchor(Start197 expected,Start197 actual) =>
        PositionError(ToHeading(expected.State),actual.State)<=calibration.PositionTolerance &&
        CourseError(ToHeading(expected.State),actual.State)<=calibration.CourseTolerance &&
        Math.Abs(expected.State.Rider.AngularVelocity-actual.State.Rider.AngularVelocity)<=calibration.YawTolerance &&
        Math.Abs(expected.State.Rider.Speed-actual.State.Rider.Speed)<=calibration.SpeedTolerance &&
        Math.Abs(expected.State.Steer-actual.State.Steer)<=.018 &&
        Math.Abs(expected.State.Lean-actual.State.Lean)<=.025 &&
        Math.Abs(expected.State.Wheel-actual.State.Wheel)<=1.2 &&
        Math.Abs(expected.State.Shaft-actual.State.Shaft)<=8 &&
        expected.TechniqueHistory==actual.TechniqueHistory &&
        expected.Active==actual.Active && expected.Pending.Length==actual.Pending.Length &&
        expected.Pending.Zip(actual.Pending).All(p=>p.First.Action==p.Second.Action &&
            Math.Abs(p.First.ObservedAt-p.Second.ObservedAt)<1e-8 && Math.Abs(p.First.ActivationAt-p.Second.ActivationAt)<1e-8);

    private static HeadingSample198 ToHeading(HandlingForecast s) => new(s.Time,s.Rider.TotalReferenceMeters,
        s.Rider.Position.X,s.Rider.Position.Y,s.Rider.Speed,s.Rider.Heading,s.Rider.AngularVelocity,s.Rider.SlideRadians,
        s.Wheel,s.Shaft,s.Steer,s.Lean,0,0,0,default);

    private static double PositionError(HeadingSample198 expected,HandlingForecast actual) =>
        Vector2.Distance(new(expected.X,expected.Y),actual.Rider.Position);
    private static double CourseError(HeadingSample198 expected,HandlingForecast actual) =>
        Math.Abs(BikeAngles.Wrap(expected.Heading-expected.Beta-(actual.Rider.Heading-actual.Rider.SlideRadians)));
}

internal sealed record DecisionWork206(double Time,double S,string Reason,int ActiveBranches,double WallMs,double CpuSeconds,
    long WorkerTicks,ForecastWork206 Guards,ForecastWork206 Worker);

internal sealed record PatchLog198(double CreatedAt,double ConsideredAt,int Switch,string Decision,string? Outcome,
    double? CandidateArrival,double? IncumbentArrival,int PhysicsSteps,double? AnchorDistance);

internal sealed class Worker198
{
    private readonly Predictor198 model;
    private readonly FreeRacingAction[] original;
    private readonly double switchTime,goal,margin;
    private readonly Predictor198.Cursor198 common;
    private readonly bool capture,profile206;
    private readonly Func<RiderView,double,string?>? style;
    internal List<HeadingSample198> ForecastTrace {get;}=[];
    internal Start197? Anchor {get;private set;}
    internal Predictor198.Cursor198? Candidate {get;private set;}
    internal Predictor198.Cursor198? Baseline {get;private set;}
    internal FreeRacingAction[] Controls {get;}
    internal int Switch {get;}
    internal double CreatedAt {get;}
    internal ForecastWork206 Work206=>common.Work206+(Candidate?.Work206??default)+(Baseline?.Work206??default);
    internal int TotalSteps=>common.Steps+(Candidate?.Steps??0)+(Baseline?.Steps??0);
    internal bool Completed=>Candidate?.Outcome is not null && Baseline?.Outcome is not null;
    internal bool Faster=>Completed && Candidate is {Reached:true} &&
        (Baseline is {Reached:false} || Candidate.GoalTime<Baseline!.GoalTime-1e-6);
    internal Worker198(Predictor198 model,Start197 start,FreeRacingAction[] original,FreeRacingAction[] edits,
        int from,int next,double at,double goal,double margin,bool capture,Func<RiderView,double,string?>? style,bool profile206=false)
    {
        this.model=model;
        this.original=original;
        this.switchTime=at;
        this.goal=goal;
        this.margin=margin;
        this.capture=capture;
        this.profile206=profile206;
        this.style=style;
        Switch=next;
        Controls=edits;
        CreatedAt=start.State.Time;
        common=model.Begin(start,original,from,goal,margin,profile:profile206,constraint:style);
    }

    internal int Advance(int budget)
    {
        int before=TotalSteps;
        if(Anchor is null)
        {
            common.Advance(budget,switchTime);
            if(common.Outcome is not null || common.Snapshot.Time<switchTime-1e-8)return TotalSteps-before;
            Anchor=common.Checkpoint();
            if(common.NextIndex!=Switch) throw new InvalidOperationException("A forecast crossed the proposed switch before certification.");
            Candidate=model.Begin(Anchor,Controls,Switch,goal,margin,profile:profile206,constraint:style);
            Baseline=model.Begin(Anchor,original,Switch,goal,margin,profile:profile206,constraint:style);
            if(capture)ForecastTrace.Add(Candidate.Snapshot);
        }
        if(capture)
        {
            var forecast=Candidate!;
            int limit=forecast.Steps+budget;
            while(forecast.Outcome is null && forecast.Steps<limit)
            {
                double next=ForecastTrace[^1].Time+Predictor198.Review;
                forecast.Advance(limit-forecast.Steps,next);
                if(forecast.Snapshot.Time>=next-1e-8)ForecastTrace.Add(forecast.Snapshot);
                else break;
            }
        }
        else Candidate!.Advance(budget);
        Baseline!.Advance(budget);
        return TotalSteps-before;
    }
}
