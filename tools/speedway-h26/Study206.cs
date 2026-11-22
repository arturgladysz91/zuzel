using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Evidence205="data/experiments/speedway205";
    private const string FrozenPolicy206=Evidence205+"/H28-C-training.json.gz";

    private static string NativeFile206(string root,string fixture) =>
        Path.Combine(root,Evidence205,"native","H28-C-learned-"+fixture+".json.gz");

    private static Trial199 Policy205(string root)=>Read199<Training199>(Path.Combine(root,FrozenPolicy206)).Winner.Result;

    private sealed record PreflightResult206(string Variant,string Fixture,bool Strict,string? Outcome,double? Finish,
        double Swept,HeadingSample198 End,RacingFailure206? Failure,ContactFailure203? Contact,
        Profile198? Work,Start197[] Predecessors)
    {
        public bool Legal=>Outcome=="goal";
    }

    private static (Seed198 Seed,StartingGate Gate,RiderSpec Spec) Fixture206(string root,string fixture)
    {
        var (seed,gate,spec)=Fixture200(root,fixture);
        return (seed with {Start=seed.Start with {Pending=[],Review=0}},gate,spec);
    }

    private static PreflightResult206 Preflight206(string root,string fixture,FreeRacingAction[] controls,
        string variant="H28-F",bool strict=true,bool capture=false,bool detailed=false)
    {
        var (seed,_,spec)=Fixture206(root,fixture);
        var profile=strict?new RacingProfile206(Map199):null;
        var settings=Settings202(variant) with {ProfileCertificate=detailed};
        var predictor=new Predictor198(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia,profile);
        var corridor=strict?null:new Corridor199(Map199,double.PositiveInfinity);
        var cursor=predictor.Begin(seed.Start,controls,0,seed.Goal,strict?.502:.5,profile:detailed,constraint:corridor is null?null:corridor.Check);
        var tail=new Queue<Start197>();
        if(capture)
        {
            for(int i=1;i<=1700&&cursor.Outcome is null;i++)
            {
                cursor.Advance(25,i*Predictor198.Review);
                tail.Enqueue(cursor.Checkpoint());
                if(tail.Count>11) tail.Dequeue();
            }
        }
        else cursor.Advance(10000);
        return new(variant,fixture,strict,cursor.Outcome,cursor.Reached?cursor.GoalTime:null,cursor.Sweep,
            cursor.Snapshot,cursor.StrictFailure206,cursor.ContactFailure,detailed?cursor.Counters:null,tail.ToArray());
    }

    private static void Style206(string root,string output)
    {
        var policy=Pad200(Policy205(root).Controls!);
        var preflights=(from variant in new[]{"H28-C","H28-F"}
                        from fixture in Fixtures204
                        from strict in new[]{false,true}
                        select Preflight206(root,fixture,policy,variant,strict,capture:true)).ToArray();

        var archives=new List<object>();
        foreach(string fixture in new[]{"C","C-hold-minus"})
        {
            var archive=Read199<Archive204>(NativeFile206(root,fixture));
            var rider=archive.Run.Riders.Single();
            var strict=new RacingProfile206(Map199).Begin();
            RacingFailure206? corridor=null,technique=null;
            foreach(var sample in rider.Samples)
            {
                var seen=strict.InspectStyle(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate??0,.5);
                corridor??=seen.Corridor;
                technique??=seen.Technique;
                if(corridor is not null&&technique is not null) break;
            }
            if(corridor is null||technique is null) throw new InvalidOperationException("Frozen style faults failed to reproduce.");
            var causal=rider.Samples.Where(x=>x.Input.Time>=corridor.Time-.45&&x.Input.Time<=technique.Time+.05)
                .Select(x=>new {x.Input.Time,x.Input.Rider,x.Input.Active,x.Input.Pending,x.Input.Steering,
                    x.RequestedDrive,x.RequestedSteering,x.Outer,x.Certificate,
                    Course=Corridor199.CourseError(x.Input.Rider)}).ToArray();
            archives.Add(new {Fixture=fixture,rider.FinishSeconds,archive.Run.Fingerprint,Corridor=corridor,
                Technique=technique,Causal=causal,Thresholds=new {Technique200.RequiredSeconds,
                    Technique200.CorrectionYawThreshold,Technique200.CorrectionCourseLimit}});
        }
        Save(Path.Combine(output,"strict-style.json.gz"),new {Archives=archives,Preflights=preflights,NativeWorlds=0},true);
        foreach(var row in preflights.Where(x=>x.Variant=="H28-F"))
            Console.WriteLine($"{row.Fixture} strict={row.Strict} {row.Outcome} {row.End.Time:F3} S={row.End.S:F3}");
    }

    private static void Decisions206(string root,string output,string fixture,string variant,bool detailed)
    {
        if(fixture is not ("C" or "C-hold-minus")||variant is not ("H28-C" or "H28-F"))
            throw new ArgumentException("A frozen H28-C archive and the compared kernels are required.");
        var archive=Read199<Archive204>(NativeFile206(root,fixture));
        var rider=archive.Run.Riders.Single();
        var controls=Pad200(Policy205(root).Controls!);
        var (seed,_,spec)=Fixture206(root,fixture);
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var settings=Settings202(variant) with {ProfileCertificate=detailed};
        var model=new Predictor198(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia);
        var preflight=model.Begin(seed.Start,controls,0,seed.Goal,.502,profile:detailed,constraint:corridor.Check);
        var rollout=Stopwatch.StartNew();
        preflight.Advance(10000);
        rollout.Stop();
        if(!preflight.Reached) throw new InvalidOperationException("Frozen relaxed benchmark needs the original continuation.");
        var agent=new Receding198(model,new(.002,.012,.02,.08,.012),seed,controls,preflight.GoalTime,
            style:corridor.Check,profile206:detailed);
        var divergences=new List<object>();
        var decisionStates=new List<object>();
        var startCpu=Process.GetCurrentProcess().TotalProcessorTime;
        var elapsed=Stopwatch.StartNew();
        int sampleIndex=0;
        foreach(var decision in rider.Decisions)
        {
            double t=decision.Choice.Time;
            while(sampleIndex+1<rider.Samples.Length&&rider.Samples[sampleIndex+1].Input.Time<=t+1e-8) sampleIndex++;
            var sample=rider.Samples[sampleIndex];
            if(Math.Abs(sample.Input.Time-t)>1e-8) throw new InvalidOperationException("Missing archived review checkpoint.");
            var actual=State205(archive,sampleIndex);
            var active=actual.Active;
            var pending=new List<FreeRacingCommand>(actual.Pending.Length);
            foreach(var command in actual.Pending.Where(x=>x.ObservedAt<t-1e-9))
            {
                if(command.ActivationAt<=t+1e-10) active=command.Action;
                else pending.Add(command);
            }
            var own=actual with {Review=t,Active=active,Pending=pending.ToArray(),
                State=actual.State with {Rider=actual.State.Rider with {Drive=active.Drive}}};
            if(agent.Choose(own)!=decision.Choice.Action&&divergences.Count<20)
                divergences.Add(new {Time=t,own.State.Rider.TotalReferenceMeters,Frozen=decision.Choice.Action,
                    Replayed=agent.Journal[^1].Action});
            var journal=agent.Journal[^1];
            if(journal.Milliseconds>50||detailed&&agent.Work206[^1].WallMs>50)
            {
                var where=Map199.Locate(own.State.Rider.TotalReferenceMeters);
                var f=sample.Forces;
                var load=sampleIndex>0?archive.Loads[0][sampleIndex-1]:null;
                double? applied=load?.Acceleration-load?.Precision?.AppliedResidual;
                decisionStates.Add(new {t,Phase=own.State.Rider.Road.IsTurn?"bend":"straight",
                    where.Lap,Own=own,Speed=own.State.Rider.Speed,Beta=own.State.Rider.SlideRadians,
                    Yaw=own.State.Rider.AngularVelocity,Steering=own.State.Steer,Requested=journal.Action,
                    AxSolved=load?.Acceleration,AxApplied=applied,SteerLimited=f?.SteeringAfter!=f?.SteeringRequest,
                    journal.Reason});
            }
        }
        elapsed.Stop();
        var cpu=Process.GetCurrentProcess().TotalProcessorTime-startCpu;
        var decisions=DecisionTiming206.Measure(agent.Journal.Select(x=>x.Milliseconds));
        var summary=new {Variant=variant,Fixture=fixture,Detailed=detailed,Mode="MODEL archived decision snapshots, frozen relaxed gate",
            decisions.Count,decisions.P50,decisions.P95,decisions.P99,decisions.Max,decisions.Over50,
            CpuSeconds=cpu.TotalSeconds,WallSeconds=elapsed.Elapsed.TotalSeconds,
            RolloutMs=rollout.Elapsed.TotalMilliseconds,Rollout=preflight.Counters,agent.AcceptedPatches,
            ActionDivergence=divergences,NativeWorlds=0,Frequency=Stopwatch.Frequency,ProcessorCount=Environment.ProcessorCount};
        Save(Path.Combine(output,"decision-profile.json.gz"),new {Summary=summary,agent.Journal,agent.Patches,
            Detail=agent.Work206,SlowStates=decisionStates},true);
        Console.WriteLine(JsonSerializer.Serialize(summary,Json));
    }

    private sealed record Candidate206(int Id,int Round,Edit199[] Edits,bool Legal,string Stop,double Progress,
        double Finish,double Swept,RacingFailure206? Failure);
    private sealed record RobustPolicy206(int Id,FreeRacingAction[] Controls,PreflightResult206[] Validation);

    private sealed record BoundedSearch206(Candidate206[] Attempts);

    private static void Search206(string root,string output,int rounds,string? previous)
    {
        if(rounds is <1 or >6) throw new ArgumentException("Each exploratory series allows one to six short rounds.");
        var (seed,_,spec)=Fixture206(root,"C");
        var frozen=Policy205(root);
        var profile=new RacingProfile206(Map199);
        var model=new Predictor198(Frame,Surface,Physics,Settings202("H28-F"),Timing,spec,Mass,Inertia,profile);
        var planner=new CyclePlanner199(model,Frame.LapMeters,seed.Goal,new Corridor199(Map199));
        var log=previous is null?new List<Candidate206>():Read199<BoundedSearch206>(previous).Attempts.ToList();
        int completedRounds=log.Count==0?0:log.Max(x=>x.Round);
        if(completedRounds+rounds>10) throw new ArgumentException("At most ten short rounds across both series.");
        int nextId=log.Count==0?0:log.Max(x=>x.Id)+1;
        Candidate206 Consider(Edit199[] edits,int round)
        {
            var trial=planner.TryEdits206(seed.Start,frozen,edits);
            return new(nextId++,round,edits,trial.Legal,trial.Stop,trial.Progress,trial.Finish,trial.Sweep,trial.StrictFailure);
        }
        static IOrderedEnumerable<Candidate206> Rank(IEnumerable<Candidate206> pool)=>pool.OrderByDescending(x=>x.Legal)
            .ThenByDescending(x=>x.Legal?0:x.Progress).ThenBy(x=>x.Finish).ThenByDescending(x=>x.Swept);
        if(log.Count==0) log.Add(Consider([],0));
        var incumbent=Rank(log).First();
        for(int round=completedRounds+1;round<=completedRounds+rounds&&!incumbent.Legal;round++)
        {
            double center=Math.Max(12,incumbent.Progress-10);
            var pool=(from span in new[]{14d,28d,45d}
                      from steer in new[]{-.013,-.004,.004,.013}
                      from gas in new[]{-.20,0,.15}
                      select new Edit199(center,span,gas,steer,0,false))
                .Concat(from phase in new[]{-4d,-2d,2d,4d} from gas in new[]{-.16,0d}
                    select new Edit199(center,28,gas,0,phase,false)).ToArray();
            var roundResults=new Trial199[pool.Length];
            Parallel.For(0,pool.Length,new ParallelOptions{MaxDegreeOfParallelism=4},i=>
                roundResults[i]=planner.TryEdits206(seed.Start,frozen,[..incumbent.Edits,pool[i]]));
            for(int i=0;i<pool.Length;i++)
            {
                var t=roundResults[i];
                log.Add(new(nextId++,round,[..incumbent.Edits,pool[i]],t.Legal,t.Stop,t.Progress,t.Finish,t.Sweep,t.StrictFailure));
            }
            incumbent=Rank(log).First();
            Console.WriteLine($"Round {round}: best {incumbent.Id} {incumbent.Stop} t={incumbent.Finish:F3} S={incumbent.Progress:F3}");
            if(incumbent.Legal) break;
        }

        // Lock the shortlist before observing any offset result. Its tapes, not
        // the feedback variants themselves, are held fixed on all five starts.
        var shortlist=Rank(log).Take(5).ToArray();
        var evaluated=shortlist.Select(candidate=>
        {
            var trace=planner.TryEdits206(seed.Start,frozen,candidate.Edits,record:true);
            var controls=Pad200(CompleteTape(trace.Controls!,frozen.Controls!));
            var validation=Fixtures204.Select(f=>Preflight206(root,f,controls,capture:true)).ToArray();
            return new {Candidate=candidate,Controls=controls,Validation=validation};
        }).ToArray();
        var winner=evaluated.Where(x=>x.Validation.All(y=>y.Legal)).OrderBy(x=>x.Validation.Average(y=>y.Finish!.Value))
            .ThenByDescending(x=>x.Validation.Min(y=>y.Swept)).FirstOrDefault();
        if(winner is not null)
            Save(Path.Combine(output,"robust-policy.json.gz"),new RobustPolicy206(winner.Candidate.Id,winner.Controls,winner.Validation),true);
        Save(Path.Combine(output,"controlled-search.json.gz"),new {Attempts=log,LockedShortlist=shortlist.Select(x=>x.Id),
            Offsets=evaluated,Winner=winner?.Candidate.Id,NativeWorlds=0},true);

        // Failed training prefixes retain the unchanged suffix for independent
        // offset predictions, which remain subject to the entire strict audit.
        static FreeRacingAction[] CompleteTape(FreeRacingAction[] prefix,FreeRacingAction[] old) =>
            prefix.Concat(old.Skip(prefix.Length)).ToArray();
    }

    private sealed record ValidationProbe206(Candidate206 Candidate,FreeRacingAction[] Controls,PreflightResult206[] Validation);
    private sealed record ValidationSet206(ValidationProbe206[] Offsets);

    private static void Recheck206(string root,string output,string search)
    {
        var candidates=Read199<ValidationSet206>(search).Offsets;
        var replay=candidates.Select(candidate=>new
        {
            candidate.Candidate.Id,
            Fixtures=Fixtures204.Select(fixture=>
            {
                var recorded=candidate.Validation.Single(row=>row.Fixture==fixture);
                var current=Preflight206(root,fixture,candidate.Controls,capture:true);
                return new {Exact=recorded.Outcome==current.Outcome&&recorded.Swept==current.Swept&&
                    recorded.Finish==current.Finish&&recorded.End==current.End,Current=current};
            }).ToArray()
        }).ToArray();
        Save(Path.Combine(output,"final-policy-recheck.json.gz"),new {Candidates=replay,NativeWorlds=0},true);
        if(replay.Any(x=>x.Fixtures.Any(f=>!f.Exact)))
            throw new InvalidOperationException("A locked policy changed its strict preflight result.");
    }

    private static void NativeStrict206(string root,string output,string fixture,string policyPath)
    {
        if(!Fixtures204.Contains(fixture)) throw new ArgumentException("Unknown study fixture.");
        var policy=Read199<RobustPolicy206>(policyPath);
        var checkedFixtures=Fixtures204.Select(name=>Preflight206(root,name,policy.Controls)).ToArray();
        if(checkedFixtures.Any(x=>!x.Legal))
        {
            Save(Path.Combine(output,"H28-F-"+fixture+".preflight.json"),new {Checked=checkedFixtures,NativeWorlds=0});
            return;
        }
        var certificate=checkedFixtures.Single(x=>x.Fixture==fixture);
        var (seed,gate,spec)=Fixture206(root,fixture);
        var settings=Settings202("H28-F");
        var profile=new RacingProfile206(Map199);
        var nativeStyle=profile.Begin(seed.Start);
        var model=new Predictor198(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia,profile);
        var agent=new Receding198(model,new(.002,.012,.02,.08,.012),seed,policy.Controls,certificate.Finish!.Value);
        string arm="H28-F-"+fixture;
        RequireLedger203(arm);
        var rig=new HandlingRig(settings);
        using var world=new TrafficWorld191(seed.Archive.Run.Options,Frame,[spec],[rig],Timing,[gate]);
        RacingFailure206? failure=null;
        var timer=Stopwatch.StartNew();
        var run=world.Run("206-"+arm,"K1",(input,crank,_,_)=>
        {
            var live=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,crank.Omega),
                input.Active,input.Pending,input.Time)
                {RiderHeightOffsetMeters=rig.RiderHeightOffsetMeters,TechniqueHistory=nativeStyle.Capture()};
            var decisionTimer=Stopwatch.StartNew();
            var action=agent.Choose(live);
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),decisionTimer.Elapsed.TotalSeconds);
        },strictCheck:(sample,finish)=>
        {
            failure=nativeStyle.Check(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate!.Value,.5,
                RacingProfile206.Physical(sample.Input.Rider,sample.Input.Time,(float)sample.Powertrain!.Seconds,
                    sample.Forces!.Value,sample.Powertrain),finish);
            return failure?.Condition;
        });
        timer.Stop();
        var rider=run.Riders.Single();
        var audit=profile.Begin();
        audit.ObserveInitial(rider.Samples[0].Input.Rider,rider.Samples[0].Input.Time);
        RacingFailure206? postAudit=null;
        foreach(var sample in rider.Samples.Skip(1))
        {
            postAudit=audit.Check(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate!.Value,.5,
                RacingProfile206.Physical(sample.Input.Rider,sample.Input.Time,(float)sample.Powertrain!.Seconds,
                    sample.Forces!.Value,sample.Powertrain),sample.Input.Rider.TotalReferenceMeters>=seed.Goal);
            if(postAudit is not null) break;
        }
        var decisions=DecisionTiming206.Measure(agent.Journal.Select(x=>x.Milliseconds));
        var result=new {Fixture=fixture,run.Outcome,rider.FinishSeconds,run.TotalCpuSeconds,WallSeconds=timer.Elapsed.TotalSeconds,
            rider.CompletedLaps,MinSwept=rider.Samples.Min(x=>x.Certificate),Contacts=run.Contacts.Length,
            Failure=failure??agent.LastStrictFailure206,PostAudit=postAudit,Decisions=decisions,
            agent.AcceptedPatches,run.Fingerprint};
        Save(Path.Combine(output,arm+".json.gz"),new {Result=result,Run=run,Handling=settings,Lean=new[]{rig.LeanSamples},
            Loads=new[]{rig.LoadSamples},Posture=rig.PostureSamples,Planner=agent.Journal,agent.Patches,Preflights=checkedFixtures},true);
        Save(Path.Combine(output,arm+".result.json"),result);
    }
}
