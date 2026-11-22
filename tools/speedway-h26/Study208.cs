using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Evidence207="data/experiments/speedway207";
    private static readonly string[] Official208=["C","C-hold-minus","C-hold-plus","C-alt","C-alt-plus"];
    private static readonly double[] Delta208=[0,-.02,.02,-.12,.12];
    private sealed record Budget208(int Proposals,int Batch,int Rounds,int Steps,double Cpu,double Wall,int Threads);
    private static readonly Budget208 LateBudget208=new(100,25,4,620000,120,90,5);
    private static readonly Budget208 SteeringBudget208=new(1088,64,17,3300000,420,260,6);
    private static readonly Budget208 JointBudget208=new(1456,64,23,4100000,510,315,6);
    private static readonly Budget208 ViabilityBudget208=new(1872,64,30,5400000,650,380,6);
    private static readonly Budget208 ExtensionBudget208=new(832,64,13,3300000,450,260,6);
    private sealed record Base208(Plan207 Plan,ExitPolicy208 Policy,FullRun207 Frozen);
    private sealed record StartCase208(string Key,Predictor198 Model,Start197 Prefix,double Goal);
    private sealed record Gate208(string Name,Start197 State,HeadingSample198 Sample,ExitMotion208 Motion,
        double? Inward,double? RearUse,double? FrontLoad,double? PhysicalSteerLimit,double PendingDelay);
    private sealed record Leg208(string Key,bool Stabilization,bool Continuation,bool Finish,string Stop,
        double LegalS,double LegalSweep,double? TimeToSettle,double? FutureSweep,double? ExitToEnd,
        Gate208[] Gates,HeadingSample198 End,Profile198 Work,RacingFailure206? Failure,ContactFailure203? Contact, double? FinishSeconds=null);
    private sealed record Suffix208(Recovery208 Recovery,Leg208[] Runs);
    private sealed record Proposal208(int Id,int? Parent,int Round,Maneuver208 Maneuver,Leg208[] Runs,
        Suffix208[]? Successors=null);
    private sealed record CandidatePlan208(Plan207 Base,Maneuver208[] Maneuvers);
    private sealed record Locked208(CandidatePlan208 Plan,Leg208[] Results);
    private sealed record Arm208(string Arm,int Visit,Budget208 Budget,string[] Training,Start197[] Prefixes,
        int[] Retained,Proposal208[] Trials,int? Winner,int Best,int Calls,int Steps,double CpuSeconds,double WallSeconds,string Stop);

    private static CandidatePlan208 ReadPlan208(string path)
    {
        using var file=File.OpenRead(path);
        return JsonSerializer.Deserialize<CandidatePlan208>(file,Json)
            ??throw new InvalidDataException("Invalid H29-V policy file.");
    }

    private static Base208 ReadBase208(string root)
    {
        var selected=Path.Combine(root,Evidence207,"selected-replays.json.gz");
        using var stream=new GZipStream(File.OpenRead(selected),CompressionMode.Decompress);
        using var document=JsonDocument.Parse(stream);
        var plan=document.RootElement.GetProperty("plan").Deserialize<Plan207>(Json)!;
        if(plan.Branch!=12152) throw new InvalidOperationException("The registered H29-R source is policy 12152.");
        var before=Geometry207(root);
        var phases=before.Phases();
        var frozen=VerifyRun207(root,"C",before,phases,plan,capture:true);
        var native=Archived207(Path.Combine(root,"data/experiments/speedway202/H25-same.json.gz"),"H25");
        // H25 contributes observed geometry only. Its speed and dynamics are not transferred.
        var h25=Route(native.Trace).Select(k=>k with {Speed=0,Yaw=0,Beta=0}).ToArray();
        var bank=new ExitPolicy208(Map199,Frame.LapMeters,Decide207(before,phases,plan),Route(frozen.Trace!),h25);
        return new(plan,bank,frozen);

        static RoutePoint208[] Route(AuditPoint207[] trace)=>trace.Where(r=>r.Start.State.Rider.TotalReferenceMeters>275)
            .Select(r=>new RoutePoint208(r.Start.State.Rider.TotalReferenceMeters,r.Start.State.Rider.Road.OffsetMeters,
                r.Course,r.Start.State.Rider.Speed,r.Start.State.Rider.AngularVelocity,r.Start.State.Rider.SlideRadians)).ToArray();
    }

    private static Predictor198 Model208(RiderSpec spec,bool timing=false)=>new(Frame,Surface,Physics,
        Settings202("H28-F") with {ProfileCertificate=timing},Timing,spec,Mass,Inertia,new RacingProfile206(Map199));

    private static Start197 Prefix208(Predictor198 model,Start197 initial,double goal,
        Func<Start197,FreeRacingAction> law,double s)
    {
        var cursor=model.BeginFeedback207(initial,law,goal,.5);
        for(int i=1;i<=1700&&cursor.Outcome is null&&cursor.Snapshot.S<s;i++)
            cursor.Advance(20,i*Predictor198.Review);
        if(cursor.Outcome is not null||cursor.Snapshot.S<s)
            throw new InvalidOperationException($"Invalid prefix at {cursor.Snapshot.S:F3}: {cursor.Outcome}.");
        return cursor.Checkpoint();
    }

    private static Gate208 Snapshot208(Predictor198.Cursor198 cursor,ExitPolicy208 bank,BendVisit bend,string name)
    {
        var state=cursor.Checkpoint();
        var f=cursor.LastContact?.Forces;
        var road=bank.ExitRoad(bend);
        return new(name,state,cursor.Snapshot,ExitMotion208.From(state.State.Rider,road,Mass),
            f.HasValue?ExitMotion208.Inward(f.Value.NetForce,road,Mass):null,cursor.LastPower?.Wheel.RearUtilization,
            f?.TyreLoads?.FrontNormalNewtons,
            TwoContactBikeParameters.Default.MaximumSteeringRadians-Math.Abs(state.State.Steer),
            Math.Max(0,(state.Pending.FirstOrDefault()?.ActivationAt??state.State.Time)-state.State.Time));
    }

    private static Leg208 Follow208(StartCase208 start,ExitPolicy208 bank,BendVisit bend,
        Func<Start197,FreeRacingAction> law,double target,int limit=1600,bool full=false,
        bool startAtExit=false,bool record=false)
    {
        var cursor=start.Model.BeginFeedback207(start.Prefix,law,start.Goal,.5,profile:true);
        var gates=new List<Gate208>();
        (double S,string Name)[] marks=[(bend.Entry-50,"preceding core"),(bend.Entry-8,"core release"),
            (bend.Entry,"entry"),(bend.Entry+bend.Length*.28,"slide"),
            (bend.Entry+bend.Length*.5,"apex"),(bend.Exit-15,"exit preparation"),
            (bend.Exit,"exit"),(bend.Exit+12,"stabilization"),(bend.Exit+28,"straight")];
        int mark=0;
        while(mark<marks.Length&&marks[mark].S<start.Prefix.State.Rider.TotalReferenceMeters-1e-8)mark++;
        double exitTime=startAtExit?start.Prefix.State.Time:double.NaN;
        double? future=null;
        double? settleTime=null;
        bool passed=false;
        while(cursor.Outcome is null&&cursor.Steps<limit)
        {
            double previous=cursor.Snapshot.S;
            cursor.Advance(1);
            if(cursor.Outcome is not (null or "goal")) break;
            var sample=cursor.Snapshot;
            if(sample.S>=bend.Exit&&double.IsNaN(exitTime))
                exitTime=sample.Time;
            if(sample.S>=bend.Exit)
                future=Math.Min(future??double.PositiveInfinity,sample.Sweep);
            if(record)
                while(mark<marks.Length&&sample.S>=marks[mark].S)
                {
                    gates.Add(Snapshot208(cursor,bank,bend,marks[mark].Name));
                    mark++;
                }
            else if(previous<bend.Exit&&sample.S>=bend.Exit)
                gates.Add(Snapshot208(cursor,bank,bend,"exit"));
            if(sample.S>=bend.Exit+12&&!double.IsNaN(exitTime))
            {
                passed=true;
                if(settleTime is null&&cursor.Checkpoint().TechniqueHistory is {Settled:true})
                    settleTime=sample.Time-exitTime;
            }
            if(!full&&sample.S>=target) break;
        }
        return new(start.Key,passed,(!full||cursor.Reached)&&cursor.LastLegalS207>=target,
            cursor.Reached,cursor.Outcome??(cursor.LastLegalS207>=target?"checkpoint":"step cap"),
            cursor.LastLegalS207,cursor.LegalSweep207,settleTime,future,
            double.IsNaN(exitTime)?null:cursor.Snapshot.Time-exitTime,gates.ToArray(),cursor.Snapshot,cursor.Counters,
            cursor.StrictFailure206,cursor.ContactFailure,cursor.Reached?cursor.GoalTime:null);
    }

    private static StartCase208[] Roots208(string root,Base208 source,Maneuver208[] prefix,int visit,bool late)
    {
        var bend=source.Policy.Bend(visit);
        double s=late?bend.Exit:Math.Max(0,source.Policy.Previous(visit).Exit+5);
        var plan=prefix.ToDictionary(x=>x.Visit);
        return TrainingStarts207.Select(fixture=>
        {
            var (seed,_,spec)=Fixture207(root,fixture);
            var model=Model208(spec);
            var state=Prefix208(model,seed.Start,seed.Goal,at=>source.Policy.Action(at,plan),s);
            return new StartCase208(fixture,model,state,seed.Goal);
        }).ToArray();
    }

    private static void Plan208(string root,string output,string arm,string? upstream=null)
    {
        var source=ReadBase208(root);
        var prefix=upstream is null?[]:ReadPlan208(upstream).Maneuvers;
        int visit=prefix.Length==0?3:prefix[^1].Visit+1;
        var bank=source.Policy;
        var bend=bank.Bend(visit);
        var budget=arm switch {"B"=>LateBudget208,"C"=>SteeringBudget208,"D"=>JointBudget208,
            "E"=>ViabilityBudget208,"X"=>ExtensionBudget208,_=>throw new ArgumentException("Use B, C, D, E or X.")};
        var starts=Roots208(root,source,prefix,visit,arm=="B");
        var random=new Random(20800+visit+(arm=="C"?1:0));
        var tried=new HashSet<Maneuver208>();
        var trials=new List<Proposal208>();
        var pool=new List<Proposal208>();
        var stopwatch=Stopwatch.StartNew();
        using var process=Process.GetCurrentProcess();
        var cpu=process.TotalProcessorTime;
        int steps=0,calls=0;
        int next=1;
        bool viability=arm is "E" or "X";
        var earlier=prefix.ToDictionary(x=>x.Visit);
        string stop="round budget";
        bool late=arm=="B",speed=arm!="C";
        double target=Math.Min(bend.Exit+28,starts[0].Goal);
        double scoreBest=double.NegativeInfinity;
        int stale=0;
        for(int round=0;round<budget.Rounds;round++)
        {
            if(next>budget.Proposals||steps+3000*budget.Batch>budget.Steps||
                stopwatch.Elapsed.TotalSeconds>=budget.Wall||(process.TotalProcessorTime-cpu).TotalSeconds>=budget.Cpu)
            {
                stop="CPU, forecast or wall cap";
                break;
            }
            var jobs=new List<(int Id,int? Parent,Maneuver208 Maneuver)>();
            int rejected=0;
            while(jobs.Count<Math.Min(budget.Batch,budget.Proposals-next+jobs.Count+1)&&rejected<500)
            {
                var parent=pool.Count==0||random.NextDouble()<(round<3?.35:.13)?null:pool[random.Next(pool.Count)];
                var proposal=Generate208(visit,late,speed,round,next,parent?.Maneuver,random,prefix.LastOrDefault());
                if(!tried.Add(proposal)){rejected++;continue;}
                jobs.Add((next++,parent?.Id,proposal));
            }
            if(jobs.Count==0){stop="proposal menu exhausted";break;}
            var rows=new Proposal208[jobs.Count];
            Parallel.For(0,jobs.Count,new ParallelOptions{MaxDegreeOfParallelism=budget.Threads},i=>
            {
                var job=jobs[i];
                rows[i]=RunCandidate(job.Id,job.Parent,round,job.Maneuver);
            });
            trials.AddRange(rows);
            var ranked=trials.OrderByDescending(x=>Score208(x,bend,viability)).ThenBy(x=>x.Id).ToArray();
            var leader=ranked[0];
            pool=SelectPool208(ranked,bend,viability);
            double score=Score208(leader,bend,viability);
            int count=leader.Runs.Count(x=>x.Stabilization);
            Console.WriteLine($"{arm}{visit+1} round {round+1}: {leader.Id}; legal {leader.Runs.Min(x=>x.LegalS):F3}; " +
                $"stabilized {count}/3; continuation {leader.Runs.Count(x=>x.Continuation)}/3; steps {steps}; quality {score:F3}");
            stale=score>scoreBest+.002?0:stale+1;
            scoreBest=Math.Max(scoreBest,score);
            if(leader.Runs.All(x=>x.Continuation)&&stale>=3){stop="certified continuation, four measured ranking rounds";break;}
            if(stale>=9){stop="nine stagnant ranking rounds";break;}
        }
        stopwatch.Stop();
        var final=trials.OrderByDescending(x=>Score208(x,bend,viability)).ThenBy(x=>x.Id).First();
        var winning=trials.Where(x=>x.Runs.All(y=>y.Stabilization))
            .OrderByDescending(x=>Score208(x,bend,viability)).FirstOrDefault();
        var selected=winning??final;
        var stats=new Arm208(arm,visit,budget,TrainingStarts207,starts.Select(x=>x.Prefix).ToArray(),
            pool.Select(x=>x.Id).ToArray(),trials.ToArray(),winning?.Id,selected.Id,calls,steps,
            (process.TotalProcessorTime-cpu).TotalSeconds,stopwatch.Elapsed.TotalSeconds,stop);
        Save(Path.Combine(output,$"arm-{arm.ToLowerInvariant()}{visit+1}.json.gz"),stats,true);
        Save(Path.Combine(output,"candidate.json"),new CandidatePlan208(source.Plan,[..prefix,selected.Maneuver]));
        Console.WriteLine($"{arm}{visit+1} selected {selected.Id}, {stop}, {calls} calls, {steps} steps");

        Proposal208 RunCandidate(int id,int? parent,int round,Maneuver208 proposal)
        {
            var choices=new Dictionary<int,Maneuver208>(earlier) {[visit]=proposal};
            var law=(Start197 at)=>bank.Action(at,choices);
            var runs=starts.Select(x=>Follow208(x,bank,bend,law,target)).ToArray();
            Interlocked.Add(ref steps,(int)runs.Sum(x=>x.Work.Steps));
            Interlocked.Add(ref calls,runs.Length);
            // E explicitly evaluates common post-exit actions from the exact candidate
            // checkpoints. Failed earlier prefixes never supply an exit evaluation.
            if(!viability||!runs.All(x=>x.Gates.Any(g=>g.Name=="exit")))return new(id,parent,round,proposal,runs);
            var exitStarts=runs.Zip(starts).Select(pair=>pair.Second with
                {Prefix=pair.First.Gates.Single(x=>x.Name=="exit").State}).ToArray();
            var available=new[]{new Recovery208(-.012,-.08,.11),new Recovery208(-.005,-.18,.25),
                new Recovery208(.005,0,.40)};
            var successors=new List<Suffix208>{new(proposal.Recovery,runs)};
            foreach(var recovery in available)
            {
                var option=proposal with {Recovery=recovery};
                if(option.Recovery==proposal.Recovery) continue;
                var suffix=exitStarts.Select(x=>Follow208(x,bank,bend,at=>bank.At(at,bend,option),target,430,startAtExit:true)).ToArray();
                successors.Add(new(recovery,suffix));
                Interlocked.Add(ref steps,(int)suffix.Sum(x=>x.Work.Steps));
                Interlocked.Add(ref calls,suffix.Length);
                var merged=suffix.Zip(runs).Select(pair=>pair.First with
                {
                    Gates=pair.Second.Gates, LegalSweep=Math.Min(pair.First.LegalSweep,pair.Second.LegalSweep)
                }).ToArray();
                if(Score208(new(id,parent,round,option,merged),bend,true)<=Score208(new(id,parent,round,proposal,runs),bend,true))continue;
                proposal=option;
                runs=merged;
            }
            return new(id,parent,round,proposal,runs,successors.ToArray());
        }
    }

    internal static double Quality208(double worstProgress,bool stabilized,bool continued,double? futureSweep,
        double worstPositiveDrift,double worstYaw,bool viability)
    {
        if(!viability)return worstProgress+(stabilized?50:0)+(continued?100:0);
        if(continued) return 3000+Math.Clamp(futureSweep??.5,.5,2)*8-worstPositiveDrift*.1;
        if(stabilized) return 2000+worstProgress*.2+(futureSweep??0)*2;
        return worstProgress-.4*worstPositiveDrift-.65*Math.Max(0,worstYaw-.23)+(futureSweep??0)*.2;
    }

    private static double Score208(Proposal208 trial,BendVisit bend,bool viability)
    {
        var exits=trial.Runs.SelectMany(x=>x.Gates.Where(g=>g.Name=="exit")).ToArray();
        double progress=trial.Runs.Min(x=>Math.Min(x.LegalS,bend.Exit+28));
        double drift=exits.Length==0?0:exits.Max(x=>Math.Max(0,x.Motion.Outward));
        double yaw=exits.Length==0?0:exits.Max(x=>Math.Abs(x.Motion.Yaw));
        double? future=trial.Runs.All(x=>x.FutureSweep.HasValue)?trial.Runs.Min(x=>x.FutureSweep):null;
        return Quality208(progress,trial.Runs.All(x=>x.Stabilization),trial.Runs.All(x=>x.Continuation),
            future,drift,yaw,viability);
    }

    private static List<Proposal208> SelectPool208(Proposal208[] ranked,BendVisit bend,bool viable)
    {
        var pool=ranked.Take(6).ToList();
        var crossed=ranked.Where(x=>x.Runs.All(r=>r.Gates.Any(g=>g.Name=="exit"))).ToArray();
        Add(crossed.OrderBy(x=>x.Runs.Max(r=>Math.Max(0,r.Gates[0].Motion.Outward))));
        Add(crossed.OrderByDescending(x=>x.Runs.Min(r=>r.FutureSweep??0)));
        Add(crossed.OrderBy(x=>x.Runs.Max(r=>Math.Abs(r.Gates[0].Motion.Yaw))));
        Add(ranked.OrderByDescending(x=>Score208(x,bend,viable)).Where(x=>x.Parent!=pool[0].Parent).Take(3));
        return pool;

        void Add(IEnumerable<Proposal208> ordered)
        {
            if(ordered.FirstOrDefault(x=>pool.All(p=>p.Id!=x.Id)) is {} option)pool.Add(option);
        }
    }

    private static Maneuver208 Generate208(int visit,bool late,bool speed,int round,int id,Maneuver208? parent,
        Random random,Maneuver208? earlier)
    {
        if(late)
        {
            var steers=new[]{0,-.004,.004,-.012,.012,-.025,.025,-.05,.05,-.08};
            var gas=new[]{0,-.17,.18,-.42};
            int i=id-1;
            return new(visit,default,new(steers[i%10],gas[i/10%4],i<40?0:i<80?.22:.48),true);
        }
        if(earlier is {} transfer&&id==1)return transfer with {Visit=visit,Speed=speed};
        if(id==1)return new(visit,new(42,0,0,0,0,0,0,0,0,18,0,0,0,0,0,0,0,0,0,0,0),default,Speed:speed);
        double L(double a,double b)=>a+(b-a)*random.NextDouble();
        var center=new Shape208(38,0,0,0,2,.03,.06,.6,0,18,1.9,.36,4,3,3,-.12,0,0,1.8,.25,.2);
        var widths=new Shape208(24,1.3,5,2,17,.48,.62,2,.16,22,2.8,.9,7,6,7,.44,.035,.049,3,1.6,.9);
        double mutate=round<3?.85:round<8?.55:round<14?.37:.24;
        if(parent is null) mutate=1;
        var c=parent?.Shape??center;
        double N(double value,double spread,double min,double max)=>
            Math.Clamp(value+L(-spread,spread)*mutate,min,max);
        var p=new Shape208(N(c.Prepare,widths.Prepare,19,55),
            N(c.EntryLine,widths.EntryLine,-1.0,1.25),N(c.ApexLine,widths.ApexLine,-4,4.5),
            N(c.ExitLine,widths.ExitLine,-1.4,1.8),N(c.ApexShift,widths.ApexShift,-16,19),
            N(c.Turn,widths.Turn,-.60,.9),N(c.MidYaw,widths.MidYaw,-.8,1.2),
            N(c.ExitYaw,widths.ExitYaw,-1.2,3.2),N(c.ExitCourse,widths.ExitCourse,-.23,.17),
            N(c.Flatten,widths.Flatten,6,42),N(c.Drift,widths.Drift,0,6),
            N(c.RouteMix,widths.RouteMix,0,1),speed?N(c.Coast,widths.Coast,0,10):0,
            speed?N(c.MidPace,widths.MidPace,0,10):0,speed?N(c.ExitPace,widths.ExitPace,0,11):0,
            speed?N(c.Ramp,widths.Ramp,-.65,.40):0,N(c.EntrySteer,widths.EntrySteer,-.075,.075),
            N(c.LateSteer,widths.LateSteer,-.075,.085),N(c.CourseGain,widths.CourseGain,0,5.5),
            N(c.Beta,widths.Beta,-1.5,2.2),N(c.Geometry,widths.Geometry,0,1));
        var tail=parent?.Recovery??default;
        if(id%5==0)tail=new(N(tail.Steer,.02,-.045,.045),speed?N(tail.Gas,.22,-.4,.25):0,N(tail.Gain,.3,0,.7));
        return new(visit,p,tail,Speed:speed);
    }
}
