using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static readonly string[] TrainingStarts207=["C","C-train-minus","C-train-plus"];
    private static readonly Move207 Neutral207=default;

    private sealed record SearchBudget207(int BeamWidth,int MaxPhaseTrials,int MaxForecastSteps,double MaxCpuSeconds,
        int Parallelism,int MaxRepairs,bool Pilot,bool Recovery=false,bool PaceRecovery=false);
    private sealed record Frontier207(int Phase,int Round,int[] Entered,int Evaluated,int Legal,int[] Retained,
        int[] Spare,double? WorstProgress,double? BestReserve,double? FastestReserve,double? FastestSwept);
    private sealed record Plan207(int Branch,Move207[] Moves);
    private sealed record FullRun207(string Fixture,bool Complete,string Stop,double? Finish,double Sweep,double LastLegalS,double LegalSweep,HeadingSample198 End,
        RacingFailure206? Failure,ContactFailure203? Contact,Profile198 Profile,AuditPoint207[]? Trace);
    private sealed record Selected207(Plan207 Plan,FullRun207[] Results);
    private sealed record Node207(int Id,Move207[] Moves,Start197[] Checkpoints);

    private static (Seed198 Seed,StartingGate Gate,RiderSpec Spec) Fixture207(string root,string name)
    {
        if(name is not ("C-train-minus" or "C-train-plus")) return Fixture206(root,name);
        var (seed,gate,spec)=Fixture206(root,"C");
        float delta=name=="C-train-minus"?-.01f:.01f;
        spec=spec with {OffsetMeters=spec.OffsetMeters+delta,TargetOffsetMeters=spec.TargetOffsetMeters+delta};
        var pose=Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
        var rider=seed.Start.State.Rider with {Position=pose.Position,Road=Frame.Project(pose.Position),
            Heading=(float)pose.Road.Heading,TargetOffsetMeters=(float)spec.TargetOffsetMeters};
        return (seed with {Start=seed.Start with {State=seed.Start.State with {Rider=rider}}},gate,spec);
    }

    private static GeometryPolicy207 Geometry207(string root)=>new(Map199,Frame.LapMeters,Policy205(root).Knots!);

    private static Move207 Blend207(Move207 before,Move207 target,double advance)
    {
        double u=Math.Clamp(advance/3,0,1);
        if(u<=0) return before;
        if(u>=1) return target;
        double w=u*u*(3-2*u);
        double L(double a,double b)=>a+(b-a)*w;
        return new(L(before.DriveBias,target.DriveBias),L(before.YawBias,target.YawBias),L(before.LineBias,target.LineBias),
            L(before.GasLeadSeconds,target.GasLeadSeconds),L(before.SteeringLeadSeconds,target.SteeringLeadSeconds),
            before.StraightYawTarget==target.StraightYawTarget?target.StraightYawTarget:
                L(before.StraightYawTarget==0?1.25:before.StraightYawTarget,target.StraightYawTarget==0?1.25:target.StraightYawTarget),
            L(before.DriftFeedback,target.DriftFeedback),
            before.ThrottleCeiling==target.ThrottleCeiling?target.ThrottleCeiling:L(before.ThrottleCeiling??1,target.ThrottleCeiling??1));
    }

    private static Move207[] Moves207(Phase207 phase,int round,Move207 previous,bool recovery,bool paceRecovery)
    {
        if(phase.End<31) return [Neutral207,new(-.08,0,.12,0,0,0),new(0,-.05,-.10,0,0,0)];
        bool approach=phase.Name is "straight stabilization" or "early approach" or "turn preparation" or
            "release from yaw core" or "entry gate" or "strict S3 finish";
        var result=new List<Move207> {Neutral207,previous};
        foreach(double gas in new[]{-.18,-.07,0,.12})
        foreach(double yaw in new[]{-.16,-.07,0,.07,.16})
            result.Add(new(gas,yaw,0,0,0,0));
        foreach(double side in new[]{-.24,.24})
        foreach(double yaw in new[]{-.10,.10})
        foreach(double gas in new[]{-.08,.05})
            result.Add(new(gas,yaw,side,0,0,0));
        foreach(double lead in new[]{-.16,.12})
        foreach(double steeringLead in new[]{-.20,.12})
            result.Add(new(-.07,0,.08,lead,steeringLead,approach?.18:0));
        if(approach)
        foreach(double cap in new[]{.13,.18,.205})
        foreach(double gas in new[]{-.12,.05})
        foreach(double side in new[]{-.14,.16})
            result.Add(new(gas,0,side,0,-.025,cap));
        if(paceRecovery&&phase.Name is "apex" or "holding slide" or "exit preparation" or "bend exit" or "straight stabilization")
        {
            foreach(double ceiling in new[]{0,.18,.38,.60})
            foreach(double yaw in new[]{-.15,.05,.28})
            foreach(double line in new[]{-.62,.15})
                result.Add(new(-.14,yaw,line,0,.13,approach?.18:0,.06,ceiling));
        }
        if(recovery&&phase.Name is "holding slide" or "exit preparation" or "bend exit" or "straight stabilization")
        {
            foreach(double gas in new[]{-.28,-.15,0})
            foreach(double yaw in new[]{-.24,0,.22})
            foreach(double offset in new[]{-.63,-.24,.18})
                result.Add(new(gas,yaw,offset,0,.06,approach?.18:0,.045));
            foreach(double gain in new[]{.025,.070,.105})
            foreach(double gas in new[]{-.24,-.08,.10})
                result.Add(new(gas,.08,-.45,.09,.16,approach?.19:0,gain));
        }
        if(round>0)
        {
            foreach(double yaw in new[]{-.30,-.22,-.05,.05,.22,.30})
            foreach(double gas in new[]{-.30,-.12,.16,.24})
            foreach(double side in new[]{-.37,0,.37})
                result.Add(new(gas,yaw,side,round==1?-.07:.08,round==1?-.14:.10,0));
            if(approach)
            foreach(double cap in new[]{.10,.16,.196})
            foreach(double yaw in new[]{-.11,.11})
            foreach(double offset in new[]{-.25,.25})
                result.Add(new(-.09,yaw,offset,.09,round==1?-.15:.045,cap));
        }
        return result.Distinct().ToArray();
    }

    private static PhaseRun207 Roll207(string fixture,Predictor198 model,GeometryPolicy207 policy,Phase207 phase,
        Start197 root,Move207 previous,Move207 move,double goal,bool recovery)
    {
        var cursor=model.BeginFeedback207(root,at=>policy.At(at,Blend207(previous,move,
            at.State.Rider.TotalReferenceMeters-phase.Start)),goal,StudyClearance207,profile:true);
        double startCourse=Corridor199.CourseError(root.State.Rider);
        double t=root.Review;
        double budget=root.State.Time+(root.State.Rider.Speed<8?14:Math.Clamp(
            (phase.End-root.State.Rider.TotalReferenceMeters)/Math.Max(6,root.State.Rider.Speed)*2.5+.6,1.2,5.5));
        do
        {
            t+=Predictor198.Review;
            cursor.Advance(22,t);
        }
        while(cursor.Outcome is null&&cursor.Snapshot.S<phase.End&&t<budget&&cursor.Steps<1500);
        var snapshot=cursor.Snapshot;
        var end=cursor.Checkpoint();
        bool accepted=cursor.Reached||cursor.Outcome is null&&snapshot.S>=phase.End&&phase.End<goal;
        if(accepted&&end.TechniqueHistory is null) throw new InvalidOperationException("Legal checkpoint lost technique history.");
        double course=Corridor199.CourseError(end.State.Rider);
        var f=cursor.LastContact?.Forces;
        double? front=f?.TyreLoads?.FrontNormalNewtons;
        double? utilisation=cursor.LastPower?.Wheel.RearUtilization;
        double? torque=f?.NetTorque;
        double? travel=f.HasValue?TwoContactBikeParameters.Default.MaximumSteeringRadians-Math.Abs(f.Value.SteeringAfter):null;
        double sweep=cursor.Sweep;
        double lineRisk=Math.Max(0,snapshot.Outer-1.45)/.35+Math.Max(0,.82-snapshot.Outer)/.32;
        double yawRisk=policy.StraightCoreRisk(end.State.Rider)+(recovery?policy.PreExitRisk(end.State.Rider,snapshot.Outer):0);
        double drift=Vector2.Dot(end.State.Rider.Velocity,end.State.Rider.Road.Outward);
        double trend=(course-startCourse)/Math.Max(.05,snapshot.Time-root.State.Time);
        double correction=end.TechniqueHistory?.CorrectionSince is {} since?Math.Max(0,.12-(snapshot.Time-since)):.12;
        double queued=end.Pending.LastOrDefault() is {} item?Math.Max(0,item.ActivationAt-snapshot.Time):0;
        double secondsToEnd=Math.Max(0,phase.End-snapshot.S)/Math.Max(1,snapshot.Speed);
        double quality=Math.Min(2.2,Math.Min(sweep,Math.Min(snapshot.Inner,snapshot.Outer)))-
            .18*yawRisk-.20*lineRisk-.05*Math.Abs(drift)-.07*Math.Abs(trend)+
            .12*Math.Min(1,(front??0)/400)+.10*(1-(utilisation??1));
        var reserve=new Reserve207(sweep,course,snapshot.Yaw,snapshot.Beta,snapshot.Speed,snapshot.Outer,snapshot.Inner,
            front,utilisation,torque,travel,drift,yawRisk,lineRisk,correction,queued,secondsToEnd,quality);
        return new(fixture,accepted,accepted?(cursor.Reached?"goal":"checkpoint"):cursor.Outcome??"phase horizon",
            root,accepted?end:null,snapshot,sweep,reserve,cursor.StrictFailure206,cursor.ContactFailure,cursor.Steps);
    }

    private static Func<Start197,FreeRacingAction> Decide207(GeometryPolicy207 policy,Phase207[] phases,Plan207 plan)
    {
        return at=>
        {
            int low=0,high=phases.Length-1;
            double s=at.State.Rider.TotalReferenceMeters;
            while(low<high)
            {
                int mid=low+(high-low)/2;
                if(s>=phases[mid].End) low=mid+1;
                else high=mid;
            }
            var start=phases[low].Start;
            Move207 target=low<plan.Moves.Length?plan.Moves[low]:Neutral207;
            Move207 previous=low>0&&low<=plan.Moves.Length?plan.Moves[low-1]:Neutral207;
            return policy.At(at,Blend207(previous,target,s-start));
        };
    }

    private static FullRun207 VerifyRun207(string root,string fixture,GeometryPolicy207 policy,Phase207[] phases,
        Plan207 plan,bool capture=false,double? assignedFinish=null)
    {
        var (seed,_,spec)=Fixture207(root,fixture);
        var profile=new RacingProfile206(Map199);
        var model=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia,profile);
        var law=Decide207(policy,phases,plan);
        var decisions=new Dictionary<int,FreeRacingAction>();
        FreeRacingAction Record(Start197 live)
        {
            var a=law(live);
            if(capture) decisions[Index207(live.Review)]=a;
            return a;
        }
        var cursor=model.BeginFeedback207(seed.Start,Record,seed.Goal,StudyClearance207,profile:capture);
        List<AuditPoint207>? trace=capture?[]:null;
        if(capture)
        {
            var first=cursor.Checkpoint();
            trace!.Add(Point207(first,first,cursor.Snapshot.Outer,null,null,null,first.Active,null));
            while(cursor.Outcome is null&&cursor.Steps<10000&&cursor.Snapshot.Time<(assignedFinish??85)-1e-9)
            {
                var before=cursor.Checkpoint();
                cursor.Advance(1);
                var next=cursor.Checkpoint();
                if(next.State.Time<=before.State.Time+1e-9) break;
                var action=decisions.TryGetValue(Index207(before.State.Time),out var request)&&
                    Math.Abs(before.State.Time-before.Review)<1e-8?(FreeRacingAction?)request:null;
                trace.Add(Point207(next,before,cursor.Snapshot.Outer,cursor.LastSweep,
                    cursor.LastContact?.Forces,cursor.LastPower,before.Active,action));
            }
        }
        else cursor.Advance(10000);
        return new(fixture,cursor.Reached,cursor.Outcome??"prefix",cursor.Reached?cursor.GoalTime:null,cursor.Sweep,
            cursor.LastLegalS207,cursor.LegalSweep207,cursor.Snapshot,cursor.StrictFailure206,cursor.ContactFailure,cursor.Counters,trace?.ToArray());
    }

    private sealed record Link207(int Id,int Parent,int Phase,Move207 Move,Start197[] Checkpoints);

    private static void PlanSearch207(string root,string output,bool pilot,bool greedy,string? recoverFrom=null,bool paceRecovery=false)
    {
        var policy=Geometry207(root);
        var phases=policy.Phases();
        bool recovery=recoverFrom is not null;
        SearchBudget207 budget=pilot?new(3,2200,450000,95,4,1,true):recovery?
            new(5,36000,5400000,500,5,2,false,true,paceRecovery):new(greedy?1:5,48000,7900000,640,5,2,false);
        var starts=TrainingStarts207.Select(x=>Fixture207(root,x)).ToArray();
        var models=starts.Select(x=>new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,x.Spec,Mass,Inertia,
            new RacingProfile206(Map199))).ToArray();
        var goal=starts[0].Seed.Goal;
        var active=new[]{new Node207(0,[],starts.Select(x=>x.Seed.Start).ToArray())};
        var known=new Dictionary<int,Node207>{{0,active[0]}};
        var attempts=new List<Branch207>();
        var frontier=new List<Frontier207>();
        Node207[] spare=[];
        int next=1,phaseTrials=0,steps=0;
        var watch=Stopwatch.StartNew();
        using var process=Process.GetCurrentProcess();
        var cpuStart=process.TotalProcessorTime;
        int firstPhase=0;
        int[] restored=[];
        if(recoverFrom is not null)
        {
            using var compressed=new GZipStream(File.OpenRead(recoverFrom),CompressionMode.Decompress);
            using var archived=JsonDocument.Parse(compressed);
            var prior=archived.RootElement.GetProperty("Frontier").Deserialize<Frontier207[]>(Json)!;
            var stall=prior[^1].Phase;
            // Rewind four stages to the approach to the previous apex. The
            // resulting phase is derived from the recorded failure frontier.
            firstPhase=Math.Max(1,stall-4);
            var prefix=prior.Last(x=>x.Phase==firstPhase-1&&x.Legal>0);
            var byId=archived.RootElement.GetProperty("Attempts").EnumerateArray()
                .ToDictionary(x=>x.GetProperty("Id").GetInt32());
            next=byId.Keys.Max()+1;
            active=prefix.Retained.Select(Restore).ToArray();
            spare=prefix.Spare.Take(budget.BeamWidth).Select(Restore).ToArray();
            restored=active.Concat(spare).Select(x=>x.Id).ToArray();

            Node207 Restore(int id)
            {
                var chain=new Stack<Link207>();
                for(var node=ReadLink(id); ;node=ReadLink(node.Parent))
                {
                    chain.Push(node);
                    if(node.Parent==0) break;
                }
                var checkpoints=starts.Select(x=>x.Seed.Start).ToArray();
                var moves=new List<Move207>();
                foreach(var node in chain)
                {
                    var priorMove=moves.LastOrDefault();
                    var stage=phases[node.Phase];
                    for(int j=0;j<checkpoints.Length;j++)
                    {
                        var verified=Roll207(TrainingStarts207[j],models[j],policy,stage,checkpoints[j],priorMove,node.Move,goal,false);
                        phaseTrials++;
                        steps+=verified.Steps;
                        if(!verified.Accepted||!Beam207.CanResume(node.Id,node.Id,verified.Checkpoint!,node.Checkpoints[j]))
                            throw new InvalidOperationException($"Saved checkpoint {node.Id} changed after full prefix replay.");
                        checkpoints[j]=verified.Checkpoint!;
                    }
                    moves.Add(node.Move);
                }
                var restoredNode=new Node207(id,moves.ToArray(),checkpoints);
                known.Add(id,restoredNode);
                return restoredNode;
            }

            Link207 ReadLink(int id)
            {
                var row=byId[id];
                var outcomes=row.GetProperty("Outcomes").EnumerateArray().ToArray();
                if(outcomes.Any(x=>!x.GetProperty("Accepted").GetBoolean()))
                    throw new InvalidOperationException("A failed branch cannot seed a resumed prefix.");
                return new(id,row.GetProperty("Parent").GetInt32(),row.GetProperty("Phase").GetInt32(),
                    row.GetProperty("Move").Deserialize<Move207>(Json),
                    outcomes.Select(x=>x.GetProperty("Checkpoint").Deserialize<Start197>(Json)!).ToArray());
            }
        }
        string termination="phases exhausted";
        foreach(var phase in phases.Skip(firstPhase))
        {
            if(pilot&&phase.Start>mapFirstBendExit())
            {
                termination="pilot end after first problematic approach";
                break;
            }
            bool progressed=false;
            var tried=new HashSet<(int,Move207)>();
            var legalAtPhase=new List<Branch207>();
            for(int round=0;round<=budget.MaxRepairs;round++)
            {
                int available=Math.Min((budget.MaxPhaseTrials-phaseTrials)/models.Length,
                    Math.Max(0,(budget.MaxForecastSteps-steps)/(models.Length*160)));
                if(available<=0||(process.TotalProcessorTime-cpuStart).TotalSeconds>=budget.MaxCpuSeconds)
                {
                    termination="bounded CPU/evaluation budget";
                    break;
                }
                var roots=round==0?active:active.Concat(spare.Take(budget.BeamWidth*2)).ToArray();
                var work=roots.SelectMany(node=>Moves207(phase,round,node.Moves.LastOrDefault(),recovery,paceRecovery)
                    .Where(move=>tried.Add((node.Id,move))).Select(move=>(Id:next++,Node:node,Move:move)))
                    .Take(available).ToArray();
                var results=new Branch207[work.Length];
                int actualSteps=0;
                Parallel.For(0,work.Length,new ParallelOptions{MaxDegreeOfParallelism=budget.Parallelism},i=>
                {
                    var job=work[i];
                    var outcomes=models.Select((model,j)=>Roll207(TrainingStarts207[j],model,policy,phase,
                        job.Node.Checkpoints[j],job.Node.Moves.LastOrDefault(),job.Move,goal,recovery)).ToArray();
                    results[i]=new(job.Id,job.Node.Id,phase.Index,job.Move,outcomes);
                    Interlocked.Add(ref actualSteps,outcomes.Sum(x=>x.Steps));
                });
                phaseTrials+=work.Length*models.Length;
                steps+=actualSteps;
                attempts.AddRange(results);
                legalAtPhase.AddRange(results.Where(x=>x.Accepted));
                // The progress-only arm has the same physics, action menu and
                // hard gate. It retains only one lineage using endpoint progress.
                var ranked=legalAtPhase.OrderByDescending(x=>x.WorstProgress).ThenBy(x=>x.FastestWorstClock)
                    .ThenByDescending(x=>x.WorstSwept).ThenBy(x=>x.Id).ToArray();
                var retained=greedy?ranked.Take(1).ToArray():Beam207.Retain(legalAtPhase,budget.BeamWidth);
                var rest=greedy?[]:Beam207.Retain(legalAtPhase.Where(x=>!retained.Any(y=>x.Id==y.Id)),budget.BeamWidth*3);
                frontier.Add(new(phase.Index,round,roots.Select(x=>x.Id).ToArray(),results.Length,results.Count(x=>x.Accepted),
                    retained.Select(x=>x.Id).ToArray(),rest.Select(x=>x.Id).ToArray(),
                    results.Length>0?results.Max(x=>x.WorstProgress):null,
                    legalAtPhase.Count>0?legalAtPhase.Max(x=>x.WorstReserve):null,
                    ranked.FirstOrDefault()?.WorstReserve,ranked.FirstOrDefault()?.WorstSwept));
                if(retained.Length==0) continue;
                active=retained.Select(Remember).ToArray();
                spare=rest.Select(Remember).ToArray();
                progressed=true;
                Console.WriteLine($"[{(pilot?"pilot":greedy?"greedy":paceRecovery?"pace":recovery?"recovery":"beam")}] {phase.Index}:{phase.Name} " +
                    $"S={phase.End:F2}: {results.Count(x=>x.Accepted)}/{results.Length}, retained {active.Length}");
                break;
            }
            if(!progressed)
            {
                if(termination=="phases exhausted") termination=$"no eligible outgoing state at {phase.Index}:{phase.Name}";
                break;
            }
        }

        // Only training states decide the shortlist, including on a stalled
        // search. Final evaluation starts independently from the exact holds.
        var locked=active.Concat(spare.Take(4)).Take(8).Select(x=>new Plan207(x.Id,x.Moves)).ToArray();
        var rankedHeld=Locked207(root,policy,phases,locked,goal);
        var winner=rankedHeld.FirstOrDefault(x=>x.Results.All(y=>y.Complete));
        var best=winner??rankedHeld.First();
        var trainingReplay=TrainingStarts207.Select(f=>VerifyRun207(root,f,policy,phases,best.Plan)).ToArray();
        var deepest=VerifyRun207(root,"C",policy,phases,best.Plan,capture:true);
        if(winner is not null) Save(Path.Combine(output,"robust-policy.json.gz"),winner,true);
        else if(rankedHeld.FirstOrDefault(x=>x.Results.Any(r=>r.Complete)) is {} eligible)
            Save(Path.Combine(output,"single-fixture-policy.json.gz"),eligible,true);
        Save(Path.Combine(output,"beam.json.gz"),new {Budget=budget,LockedFixtures=Fixtures204,TrainingStarts=TrainingStarts207,
            Phases=phases,Frontier=frontier,Attempts=attempts,Holdouts=rankedHeld,Best=best.Plan.Branch,Winner=winner?.Plan.Branch,
            TrainingReplay=trainingReplay,NominalTrace=deepest,Termination=termination,PhaseTrials=phaseTrials,ForecastSteps=steps,
            Restored=restored,FirstPhase=firstPhase,
            WallSeconds=watch.Elapsed.TotalSeconds,CpuSeconds=(process.TotalProcessorTime-cpuStart).TotalSeconds,NativeWorlds=0},true);
        Console.WriteLine($"{termination}: locked candidate {best.Plan.Branch}, {best.Results.Count(x=>x.Complete)}/5; " +
            $"worst S={best.Results.Min(x=>x.End.S):F3}, trials={phaseTrials}, steps={steps}");

        Node207 Remember(Branch207 child)
        {
            if(known.TryGetValue(child.Id,out var existing)) return existing;
            var parent=known[child.Parent];
            if(parent.Moves.Length!=child.Phase||!child.Accepted||
                !parent.Checkpoints.Zip(child.Outcomes).All(pair=>Beam207.CanResume(parent.Id,child.Parent,pair.First,pair.Second.Origin)))
                throw new InvalidOperationException("Phase checkpoint has an invalid prefix or a hard failure.");
            var node=new Node207(child.Id,[..parent.Moves,child.Move],
                child.Outcomes.Select(x=>x.Checkpoint!).ToArray());
            known.Add(node.Id,node);
            return node;
        }

        double mapFirstBendExit()=>Map199.At(0,0).NextEntry+1;
    }

    private static Selected207[] Locked207(string root,GeometryPolicy207 policy,Phase207[] phases,
        Plan207[] shortlist,double goal)
    {
        var held=shortlist.Select(plan=>new Selected207(plan,Fixtures204.Select(fixture=>
            VerifyRun207(root,fixture,policy,phases,plan)).ToArray()));
        return Beam207.RankRobust(held,x=>x.Results.Min(y=>Math.Min(goal,y.LastLegalS)),
            x=>x.Results.Count(y=>y.Complete),x=>x.Results.Min(y=>y.LegalSweep),
            x=>x.Results.Average(y=>y.Finish??85)).ToArray();
    }

    private static Selected207[] Recheck207(string root,string search)
    {
        using var stream=new GZipStream(File.OpenRead(search),CompressionMode.Decompress);
        using var archive=JsonDocument.Parse(stream);
        var shortlist=archive.RootElement.GetProperty("Holdouts").EnumerateArray()
            .Select(x=>x.GetProperty("Plan").Deserialize<Plan207>(Json)!).ToArray();
        var policy=Geometry207(root);
        return Locked207(root,policy,policy.Phases(),shortlist,3*Frame.LapMeters);
    }

    private static void RecheckLocked207(string root,string output,string search,string arm)
    {
        var results=Recheck207(root,search);
        Save(Path.Combine(output,"locked-"+arm+".json.gz"),
            new {Best=results[0].Plan.Branch,Holdouts=results,NativeWorlds=0},true);
    }
    private static void Replay207(string root,string output,string search)
    {
        var plan=Recheck207(root,search)[0].Plan;
        var policy=Geometry207(root);
        var phases=policy.Phases();
        var runs=Fixtures204.Select(fixture=>VerifyRun207(root,fixture,policy,phases,plan,capture:true)).ToArray();
        Save(Path.Combine(output,"selected-replays.json.gz"),new {plan,Runs=runs,NativeWorlds=0},true);
    }

    private static void Benchmark207(string root,string output,string search)
    {
        var plan=Recheck207(root,search)[0].Plan;
        var policy=Geometry207(root);
        var phases=policy.Phases();
        var modelTrace=VerifyRun207(root,"C",policy,phases,plan,capture:true);
        var source=modelTrace.Trace!.Where(point=>point.Start.State.Time<modelTrace.End.Time-1e-8&&
            Math.Abs(point.Start.State.Time-point.Start.Review)<1e-8).ToArray();
        var (seed,_,spec)=Fixture207(root,"C");
        var law=Decide207(policy,phases,plan);
        var model=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia,new RacingProfile206(Map199));
        var rows=new List<object>();
        for(int repeat=0;repeat<3;repeat++)
        {
            var timers=new List<double>();
            var details=new List<object>();
            int rejected=0;
            using var process=Process.GetCurrentProcess();
            var cpu=process.TotalProcessorTime;
            var wall=Stopwatch.StartNew();
            foreach(var obs in source)
            {
                long start=Stopwatch.GetTimestamp();
                var at=obs.Start;
                var action=law(at);
                var cursor=model.BeginFeedback207(at,law,seed.Goal,.5,profile:true);
                double until=at.State.Time+.25;
                cursor.Advance(40,until);
                double ms=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                bool accepted=cursor.Reached||cursor.Outcome is null&&cursor.Snapshot.Time>=until-1e-8;
                if(!accepted) rejected++;
                timers.Add(ms);
                details.Add(new {at.State.Time,S=at.State.Rider.TotalReferenceMeters,Action=action,Milliseconds=ms,
                    Accepted=accepted,cursor.Outcome,cursor.Steps,cursor.Sweep,cursor.Counters,cursor.StrictFailure206});
            }
            wall.Stop();
            rows.Add(new {Repeat=repeat+1,Statistics=DecisionTiming207(timers),Rejected=rejected,
                CpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,WallSeconds=wall.Elapsed.TotalSeconds,Decisions=details});
        }
        Save(Path.Combine(output,"model-guard-timing.json.gz"),new {Plan=plan.Branch,SourceOutcome=modelTrace.Stop,
            Mode="MODEL accepted prefixes, frozen feedback and strict 0.25 s continuation; no native timing",
            Runs=rows,NativeWorlds=0},true);

        static object DecisionTiming207(IEnumerable<double> times)
        {
            var x=DecisionTiming206.Measure(times);
            return new {x.Count,x.P50,x.P95,x.P99,x.Max,x.Over50};
        }
    }

    private static void Native207(string root,string output,string fixture,string selected)
    {
        if(!Fixtures204.Contains(fixture)) throw new ArgumentException("Unknown registered start.");
        var plan=Read199<Selected207>(selected).Plan;
        var policy=Geometry207(root);
        var phases=policy.Phases();
        var preflights=Fixtures204.Select(x=>VerifyRun207(root,x,policy,phases,plan)).ToArray();
        if(!preflights.Single(x=>x.Fixture==fixture).Complete)
        {
            Save(Path.Combine(output,"ineligible.json"),new {preflights,NativeWorlds=0});
            return;
        }

        var (seed,gate,spec)=Fixture207(root,fixture);
        var law=Decide207(policy,phases,plan);
        var settings=Settings202("H28-F");
        var strict=new RacingProfile206(Map199);
        var nativeStyle=strict.Begin(seed.Start);
        var model=new Predictor198(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia,strict);
        var rig=new HandlingRig(settings);
        string scenario="H29-R-"+fixture;
        RequireLedger203(scenario);
        using var world=new TrafficWorld191(seed.Archive.Run.Options,Frame,[spec],[rig],Timing,[gate]);
        var records=new List<object>();
        var timing=new List<double>();
        RacingFailure206? failure=null;
        var sw=Stopwatch.StartNew();
        var run=world.Run(scenario,"K1",(input,crank,_,_)=>
        {
            long begin=Stopwatch.GetTimestamp();
            var state=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,crank.Omega),
                input.Active,input.Pending,input.Time)
                {RiderHeightOffsetMeters=rig.RiderHeightOffsetMeters,TechniqueHistory=nativeStyle.Capture()};
            var action=law(state);
            var cursor=model.BeginFeedback207(state,law,seed.Goal,StudyClearance207,profile:true);
            cursor.Advance(40,input.Time+.25);
            double duration=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
            timing.Add(duration);
            records.Add(new {state,action,duration,cursor.Outcome,cursor.Sweep,cursor.Steps,Work=cursor.Counters});
            if(cursor.Outcome is not (null or "goal")||!cursor.Reached&&cursor.Snapshot.Time<input.Time+.25-1e-8)
            {
                failure=cursor.StrictFailure206;
                throw new InvalidOperationException($"Strict receding guard failed at {input.Time:F3}: {cursor.Outcome}.");
            }
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),duration/1000);
        },strictCheck:(sample,finish)=>
        {
            failure=nativeStyle.Check(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate!.Value,
                StudyClearance207,RacingProfile206.Physical(sample.Input.Rider,sample.Input.Time,
                    (float)sample.Powertrain!.Seconds,sample.Forces!.Value,sample.Powertrain),finish);
            return failure?.Condition;
        });
        sw.Stop();
        var rider=run.Riders.Single();
        var final=strict.Begin();
        RacingFailure206? independent=null;
        final.ObserveInitial(rider.Samples[0].Input.Rider,rider.Samples[0].Input.Time);
        foreach(var sample in rider.Samples.Skip(1))
        {
            independent=final.Check(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate!.Value,.5,
                RacingProfile206.Physical(sample.Input.Rider,sample.Input.Time,(float)sample.Powertrain!.Seconds,
                    sample.Forces!.Value,sample.Powertrain),sample.Input.Rider.TotalReferenceMeters>=seed.Goal);
            if(independent is not null) break;
        }
        var result=new {Fixture=fixture,run.Outcome,rider.FinishSeconds,rider.CompletedLaps,Contacts=run.Contacts.Length,
            MinSwept=rider.Samples.Min(x=>x.Certificate),run.TotalCpuSeconds,WallSeconds=sw.Elapsed.TotalSeconds,
            Decisions=DecisionTiming206.Measure(timing),AcceptedPatches=0,StrictFailure=failure,FinalAudit=independent};
        Save(Path.Combine(output,scenario+".json.gz"),new {Result=result,Run=run,Handling=settings,Lean=new[]{rig.LeanSamples},
            Loads=new[]{rig.LoadSamples},Posture=rig.PostureSamples,Decisions=records,Preflights=preflights},true);
        Save(Path.Combine(output,scenario+".result.json"),result);
        Console.WriteLine(JsonSerializer.Serialize(result,Json));
    }
}
