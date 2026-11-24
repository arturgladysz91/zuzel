using System.Diagnostics;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private const string Evidence209="data/experiments/speedway209";
    private sealed record Passage209(int Visit,string Mark,double Time,double S,double Speed,double Sweep);
    private sealed record Tick209(Start197 Before,HeadingSample198 After,double RequestedAt,
        ThrottleTrace209? Command,TwoContactForces Forces,ClutchStep144 Power,double Acceleration,
        double ForceAcceleration,double ResistanceAcceleration,double FrontAcceleration,double RearAcceleration);
    private sealed record Run209(string Fixture,bool Complete,string? Stop,double? Finish,double Sweep,double LastLegalS,
        double PeakKph,double RequestedFullSeconds,double ActiveFullSeconds,double ZeroSeconds,
        double AcceleratingSeconds,double DeceleratingSeconds,int Reductions,double Cpu,double Wall,int Steps,
        RacingFailure206? Failure,ContactFailure203? Contact,Passage209[] Passages,Tick209[]? Trace);
    private sealed record Variant209(string Arm,string Name,CandidatePlan208 Plan);

    private static Func<Start197,FreeRacingAction> Law209(Base208 source,GeometryPolicy207 geometry,CandidatePlan208 plan)
    {
        var bank=source.Policy.WithBaseline(Decide207(geometry,geometry.Phases(),plan.Base));
        var maneuvers=plan.Maneuvers.ToDictionary(x=>x.Visit);
        var recoveryModel=plan.Recovery209?.Any(w=>w.Predictive)==true?Model208(Spec193(StartingGate.D,1,8)):null;
        FreeRacingAction Baseline(Start197 at)=>bank.Action(at,maneuvers);
        return state=>
        {
            var action=Baseline(state);
            if(plan.Recovery209 is not null)
                foreach(var window in plan.Recovery209)
                {
                    var bend=bank.Bend(window.Visit);
                    double relative=state.State.Rider.TotalReferenceMeters-bend.Exit;
                    if(relative>window.From&&relative<window.To)
                    {
                        var proposed=Recovery209.Apply(state,action,window,bend.Exit);
                        action=window.Predictive?Recovery209.Select(state,action,proposed,recoveryModel!,Baseline,3*Frame.LapMeters):proposed;
                        ThrottleTrace209.Current?.Add("209 recovery window",action.Drive,action.Drive);
                    }
                }
            return action;
        };
    }

    private static Run209 Follow209(string fixture,Predictor198 model,Start197 initial,double goal,
        Func<Start197,FreeRacingAction> law,bool capture=false,double horizon=double.PositiveInfinity,
        Func<Start197,PostureCommand212>? posture=null)
    {
        ThrottleTrace209? command=null;
        double requestAt=initial.State.Time,requested=initial.Active.Drive,previousRequest=requested;
        int reductions=0;
        FreeRacingAction Observe(Start197 state)
        {
            var result=ThrottleTrace209.Capture(()=>law(state));
            command=result.Trace;requestAt=state.State.Time;requested=result.Action.Drive;
            if(requested<previousRequest-.01) reductions++;
            previousRequest=requested;
            return result.Action;
        }
        var cursor=model.BeginFeedback207(initial,capture?Observe:law,goal,.5,profile:capture,posture:posture);
        List<Tick209>? rows=capture?[]:null;
        var passages=new List<Passage209>();
        var marks=(from visit in Enumerable.Range(0,6) let bend=Map199.At(visit/Map199.Count,visit%Map199.Count)
            from mark in new[]{("entry",bend.Entry),("apex",(bend.Entry+bend.Exit)/2),("exit",bend.Exit),
                ("straight",bend.Exit+20)} select (Visit:visit,Name:mark.Item1,S:mark.Item2)).OrderBy(x=>x.S).ToArray();
        int markIndex=0;
        while(markIndex<marks.Length&&marks[markIndex].S<initial.State.Rider.TotalReferenceMeters)markIndex++;
        double peak=initial.State.Rider.Speed,fullRequested=0,fullActive=0,zero=0,accelerating=0,decelerating=0;
        double legalS=initial.State.Rider.TotalReferenceMeters,acceptedSweep=100;
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var watch=Stopwatch.StartNew();
        while(cursor.Outcome is null&&cursor.Steps<10000&&cursor.Snapshot.Time<initial.State.Time+horizon-1e-9)
        {
            var before=cursor.Checkpoint();
            cursor.Advance(1,initial.State.Time+horizon);
            var after=cursor.Snapshot;
            double dt=after.Time-before.State.Time;
            if(dt<=1e-9)break;
            // Rejected states are preserved in Failure/Contact, never counted as accepted telemetry.
            if(cursor.Outcome is not (null or "goal"))break;
            legalS=after.S;acceptedSweep=Math.Min(acceptedSweep,cursor.LastSweep);
            peak=Math.Max(peak,after.Speed);
            double acceleration=(after.Speed-before.State.Rider.Speed)/dt;
            fullRequested+=requested>=.99?dt:0;fullActive+=before.Active.Drive>=.99?dt:0;
            zero+=before.Active.Drive<=1e-6?dt:0;
            accelerating+=acceleration>1e-4?dt:0;decelerating+=acceleration< -1e-4?dt:0;
            while(markIndex<marks.Length&&after.S>=marks[markIndex].S)
            {
                var mark=marks[markIndex++];
                passages.Add(new(mark.Visit,mark.Name,after.Time,after.S,after.Speed,cursor.LastSweep));
            }
            if(capture&&cursor.LastContact is {} contact&&cursor.LastPower is {} power)
            {
                var tangent=before.State.Rider.Speed>.1?Vector2.Normalize(before.State.Rider.Velocity):before.State.Rider.Road.Tangent;
                double Ax(Vector2 force)=>Vector2.Dot(force,tangent)/Mass;
                rows!.Add(new(before,after,requestAt,command,contact.Forces,power,acceleration,
                    Ax(contact.Forces.NetForce),Ax(contact.Forces.Resistance),Ax(contact.Forces.FrontForce),
                    Ax(contact.Forces.RearForce)));
            }
        }
        return new(fixture,cursor.Reached,cursor.Outcome??"horizon",cursor.Reached?cursor.GoalTime:null,
            acceptedSweep,legalS,peak*3.6,fullRequested,fullActive,zero,accelerating,
            decelerating,reductions,(process.TotalProcessorTime-cpu).TotalSeconds,watch.Elapsed.TotalSeconds,
            cursor.Steps,cursor.StrictFailure206,cursor.ContactFailure,passages.ToArray(),rows?.ToArray());
    }

    private static Run209 RunPlan209(string root,Base208 source,GeometryPolicy207 geometry,CandidatePlan208 plan,
        string fixture,bool capture=false)
    {
        var (seed,spec)=CourseFixture208(root,fixture);
        return Follow209(fixture,Model208(spec),seed.Start,seed.Goal,Law209(source,geometry,plan),capture);
    }

    private static void Audit209(string root,string output)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);
        var path=Path.Combine(root,"data/experiments/speedway208/frozen-candidate.json");
        var plan=ReadPlan208(path);var law=Law209(source,geometry,plan);
        var reproduction=new List<object>();
        foreach(var fixture in Official208)
        {
            var run=RunPlan209(root,source,geometry,plan,fixture,true);
            Save(Path.Combine(output,fixture+".json.gz"),run,true);
            using var compressed=new GZipStream(File.OpenRead(Path.Combine(root,
                "data/experiments/speedway208/native/H29-V-"+fixture+".json.gz")),CompressionMode.Decompress);
            using var archive=JsonDocument.Parse(compressed);
            int count=0,mismatch=0;double max=0;
            var nativeClamps=new List<object>();
            foreach(var decision in archive.RootElement.GetProperty("Decisions").EnumerateArray())
            {
                var state=decision.GetProperty("State").Deserialize<Start197>(Json)!;
                var expected=decision.GetProperty("Action").Deserialize<FreeRacingAction>(Json);
                var captured=ThrottleTrace209.Capture(()=>law(state));
                var actual=captured.Action;count++;
                nativeClamps.Add(new {State=state,Control=captured.Trace});
                if(actual!=expected)mismatch++;
                max=Math.Max(max,Math.Max(Math.Abs(actual.Drive-expected.Drive),Math.Abs(actual.SteeringRadians-expected.SteeringRadians)));
            }
            reproduction.Add(new {Fixture=fixture,run.Complete,run.Finish,run.Sweep,run.PeakKph,
                NativeDecisions=count,Mismatches=mismatch,MaximumActionDifference=max});
            Save(Path.Combine(output,"native-clamps-"+fixture+".json.gz"),nativeClamps,true);
            Console.WriteLine($"A {fixture}: {run.Stop} {run.Finish:F6}s {run.PeakKph:F3}km/h, native commands {count-mismatch}/{count}");
            if(!run.Complete||mismatch!=0)throw new InvalidOperationException("Frozen #208 reproduction failed.");
        }
        var phases=geometry.Phases();
        var inventory=phases.Select((p,i)=>new {Phase=p,Move=i<plan.Base.Moves.Length?plan.Base.Moves[i]:Neutral207}).ToArray();
        Save(Path.Combine(output,"baseline.json"),new {BaseCommit="e0607f555de2d4c764b4bf26bd9bcafc420db6ec",
            FrozenSHA256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),Reproduction=reproduction,
            Phases=inventory,plan.Maneuvers,NativeWorlds=0,BrakeCommand=false});
    }

    private static IEnumerable<Variant209> Variants209(CandidatePlan208 frozen)
    {
        yield return new("A","frozen",frozen);
        foreach(double restore in new[]{.01,.025,.05,.1,.25,.5,1})
        {
            yield return new("B",$"negative bias restore {restore}",frozen with {Base=frozen.Base with {
                Moves=frozen.Base.Moves.Select(m=>m.DriveBias<0?m with {DriveBias=m.DriveBias*(1-restore)}:m).ToArray()}});
            yield return new("C",$"ceiling restore {restore}",frozen with {Base=frozen.Base with {
                Moves=frozen.Base.Moves.Select(m=>m.ThrottleCeiling is {} c?m with {ThrottleCeiling=c+(1-c)*restore}:m).ToArray()}});
        }
        foreach(int visit in new[]{4,5})
        foreach(double restore in new[]{.0025,.005,.01,.025,.05,.1,.25,.5,1})
        {
            yield return new("D",$"bend {visit+1} pace restore {restore}",frozen with {
                Maneuvers=frozen.Maneuvers.Select(m=>m.Visit!=visit?m:m with {Shape=m.Shape with {
                    Coast=m.Shape.Coast*(1-restore),MidPace=m.Shape.MidPace*(1-restore),ExitPace=m.Shape.ExitPace*(1-restore)}}).ToArray()});
            yield return new("E",$"bend {visit+1} ramp restore {restore}",frozen with {
                Maneuvers=frozen.Maneuvers.Select(m=>m.Visit!=visit?m:m with {Shape=m.Shape with {
                    Ramp=m.Shape.Ramp*(1-restore)}}).ToArray()});
        }
    }

    private static void Arms209(string root,string output)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);
        var frozen=ReadPlan208(Path.Combine(root,"data/experiments/speedway208/frozen-candidate.json"));
        var variants=Variants209(frozen).ToArray();
        using var process=Process.GetCurrentProcess();var startCpu=process.TotalProcessorTime;var wall=Stopwatch.StartNew();
        var results=new List<object>();var viable=new List<(Variant209 Variant,Run209[] Runs)>();
        foreach(var variant in variants)
        {
            if((process.TotalProcessorTime-startCpu).TotalSeconds>900||wall.Elapsed.TotalSeconds>1200)break;
            var runs=TrainingStarts207.Select(f=>RunPlan209(root,source,geometry,variant.Plan,f,true)).ToArray();
            // Compact summaries retain every first rejection; full baseline telemetry is archived separately.
            var compact=runs.Select(r=>r with {Trace=null}).ToArray();
            results.Add(new {Variant=variant,Runs=compact});
            if(runs.All(r=>r.Complete))viable.Add((variant,compact));
            Console.WriteLine($"{variant.Arm} {variant.Name}: {runs.Count(r=>r.Complete)}/3; "+
                $"time={runs.Average(r=>r.Finish??85):F6}; stop={runs[0].Stop} S={runs[0].LastLegalS:F3}");
        }
        foreach(var arm in viable.GroupBy(x=>x.Variant.Arm))
        {
            var best=arm.OrderBy(x=>x.Runs.Average(r=>r.Finish)).First();
            Save(Path.Combine(output,"best-"+arm.Key+".json"),best.Variant.Plan);
        }
        Save(Path.Combine(output,"arms.json.gz"),new {Training=TrainingStarts207,Variants=results,
            Cpu=(process.TotalProcessorTime-startCpu).TotalSeconds,Wall=wall.Elapsed.TotalSeconds,
            Bounds=new {Proposals=variants.Length,CpuSeconds=900,WallSeconds=1200},NativeWorlds=0},true);
    }

    private static void Check209(string root,string output,string path)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);var plan=ReadPlan208(path);
        var results=Official208.Select(f=>RunPlan209(root,source,geometry,plan,f,true)).ToArray();
        foreach(var r in results)
        {
            Save(Path.Combine(output,r.Fixture+".json.gz"),r,true);
            Console.WriteLine($"{r.Fixture}: {r.Stop} time {r.Finish:F6} sweep {r.Sweep:F6} peak {r.PeakKph:F3}");
        }
        Save(Path.Combine(output,"results.json"),new {Plan=plan,Runs=results.Select(r=>r with {Trace=null}),
            Qualified=results.All(r=>r.Complete),NativeWorlds=0});
    }

    internal static (bool Complete,double? Finish,double Sweep)[] VerifyPolicy209(string root,string path)
    {
        researchRepository202=root;
        var source=ReadBase208(root);var geometry=Geometry207(root);var plan=ReadPlan208(path);
        return Official208.Select(f=>RunPlan209(root,source,geometry,plan,f)).Select(r=>(r.Complete,r.Finish,r.Sweep)).ToArray();
    }

    private static void CheckArms209(string root,string output)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);var rows=new List<object>();
        foreach(string arm in new[]{"A","B","C","D","E"})
        {
            var plan=ReadPlan208(Path.Combine(root,Evidence209,"controlled/best-"+arm+".json"));
            var runs=Official208.Select(f=>RunPlan209(root,source,geometry,plan,f,true) with {Trace=null}).ToArray();
            rows.Add(new {Arm=arm,Plan=plan,Runs=runs});
            Console.WriteLine($"{arm}: {runs.Count(r=>r.Complete)}/5; fastest legal {runs.Where(r=>r.Complete).Select(r=>r.Finish).Min():F6}");
        }
        Save(Path.Combine(output,"five-start-arms.json"),new {Trials=rows,NativeWorlds=0});
    }

    private static void Screen209(string root,string output,string path)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);
        using var compressed=new GZipStream(File.OpenRead(path),CompressionMode.Decompress);
        using var archive=JsonDocument.Parse(compressed);
        var plans=archive.RootElement.GetProperty("Trials").EnumerateArray().Where(t=>
            t.GetProperty("Runs").EnumerateArray().All(r=>r.GetProperty("Complete").GetBoolean()))
            .OrderBy(t=>t.GetProperty("Runs").EnumerateArray().Average(r=>r.GetProperty("Finish").GetDouble()))
            .Take(24).Select(t=>new {Id=t.GetProperty("Id").GetInt32(),Plan=t.GetProperty("Plan").Deserialize<CandidatePlan208>(Json)!}).ToArray();
        var fixtures=TrainingStarts207.Concat(Screening208.Keys).ToArray();
        var rows=plans.Select(p=>
        {
            var runs=fixtures.Select(f=>RunPlan209(root,source,geometry,p.Plan,f,true) with {Trace=null}).ToArray();
            Console.WriteLine($"screen {p.Id}: {runs.Count(r=>r.Complete)}/7 time {runs.Average(r=>r.Finish??85):F6} sweep {runs.Min(r=>r.Sweep):F4}");
            return new {p.Id,p.Plan,Runs=runs};
        }).ToArray();
        var best=rows.Where(x=>x.Runs.All(r=>r.Complete)).OrderBy(x=>x.Runs.Average(r=>r.Finish)).FirstOrDefault();
        Save(Path.Combine(output,"screen.json.gz"),new {Fixtures=fixtures,Trials=rows,Winner=best?.Id,NativeWorlds=0},true);
        if(best is not null)Save(Path.Combine(output,"candidate.json"),best.Plan);
    }

    private sealed record SearchTrial209(int Id,int Parent,CandidatePlan208 Plan,Run209[] Runs);

    private static void Search209(string root,string output,string arm,string? upstream)
    {
        int visit=arm switch {"F5"=>4,"F6"=>5,_=>throw new ArgumentException("Use F5 or F6.")};
        var source=ReadBase208(root);var geometry=Geometry207(root);
        var original=ReadPlan208(upstream??Path.Combine(root,"data/experiments/speedway208/frozen-candidate.json"));
        double begin=source.Policy.Bend(visit-1).Exit+20;
        var originalLaw=Law209(source,geometry,original);
        var roots=TrainingStarts207.Select(f=>
        {
            var (seed,_,spec)=Fixture207(root,f);var model=Model208(spec);
            var prefix=Prefix208(model,seed.Start,seed.Goal,originalLaw,begin);
            return new StartCase208(f,model,prefix,seed.Goal);
        }).ToArray();
        const int proposals=1536,batch=32,rounds=48;
        const double cpuCap=900,wallCap=1200;
        const long stepCap=9000000;
        var trials=new List<SearchTrial209>();var pool=new List<SearchTrial209>();
        var random=new Random(20900+visit);int next=0;long steps=0;
        using var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;var watch=Stopwatch.StartNew();
        var seeds=new List<CandidatePlan208>{original};
        foreach(double scale in new[]{.9,.75,.5,.25,0d})
        foreach(double ramp in new[]{0d,-.2,-.4})
            seeds.Add(Change(original,m=>m with {Shape=m.Shape with {Coast=m.Shape.Coast*scale,
                MidPace=m.Shape.MidPace*scale,ExitPace=m.Shape.ExitPace*scale,Ramp=ramp}}));
        for(int round=0;round<rounds&&next<proposals;round++)
        {
            // Stop between batches, recording the bounded batch overshoot explicitly.
            if((process.TotalProcessorTime-cpu).TotalSeconds>=cpuCap||watch.Elapsed.TotalSeconds>=wallCap||steps>=stepCap)break;
            var jobs=new List<(int Id,int Parent,CandidatePlan208 Plan)>();
            for(int n=0;n<batch;n++)
            {
                int id=++next;
                SearchTrial209? parent=pool.Count==0?null:pool[random.Next(pool.Count)];
                CandidatePlan208 plan;
                if(id<=seeds.Count)plan=seeds[id-1];
                else
                {
                    var basis=parent?.Plan??original;
                    double scale=round<8?1:round<22?.5:.23;
                    if(id%11==0)scale=1.8;
                    plan=Change(basis,m=>Mutate(m,scale));
                }
                jobs.Add((id,parent?.Id??0,plan));
            }
            var rows=new SearchTrial209[jobs.Count];
            Parallel.For(0,jobs.Count,new ParallelOptions {MaxDegreeOfParallelism=4},i=>
            {
                var job=jobs[i];var law=Law209(source,geometry,job.Plan);
                var runs=roots.Select(r=>Follow209(r.Key,r.Model,r.Prefix,r.Goal,law,true) with {Trace=null}).ToArray();
                rows[i]=new(job.Id,job.Parent,job.Plan,runs);
            });
            trials.AddRange(rows);steps+=rows.Sum(r=>r.Runs.Sum(x=>(long)x.Steps));
            var legal=trials.Where(x=>x.Runs.All(r=>r.Complete)).OrderBy(x=>x.Runs.Average(r=>r.Finish)).ToArray();
            pool=legal.Take(7).ToList();
            foreach(var reserve in legal.OrderByDescending(x=>x.Runs.Min(r=>r.Sweep)).Take(4))
                if(pool.All(x=>x.Id!=reserve.Id))pool.Add(reserve);
            if(pool.Count==0)
                pool=trials.OrderByDescending(x=>x.Runs.Min(r=>r.LastLegalS)).Take(8).ToList();
            var best=pool[0];
            Console.WriteLine($"{arm} round {round+1}: {best.Id}; {best.Runs.Count(r=>r.Complete)}/3; "+
                $"time {best.Runs.Average(r=>r.Finish??85):F6}; reserve {best.Runs.Min(r=>r.Sweep):F4}; steps {steps}");
            // Checkpoint each completed batch so interruptions cannot erase the search history.
            File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(new {Round=round+1,Best=best.Id,
                Cpu=(process.TotalProcessorTime-cpu).TotalSeconds,Steps=steps},Json));
            File.AppendAllLines(Path.Combine(output,"batches.jsonl"),rows.Select(r=>JsonSerializer.Serialize(r,Json)));
        }
        var winner=trials.Where(x=>x.Runs.All(r=>r.Complete)).OrderBy(x=>x.Runs.Average(r=>r.Finish)).FirstOrDefault();
        Save(Path.Combine(output,"search.json.gz"),new {Arm=arm,Training=TrainingStarts207,PrefixS=begin,
            Bounds=new {Proposals=proposals,Batch=batch,Steps=stepCap,CpuSeconds=cpuCap,WallSeconds=wallCap,Threads=4},
            Trials=trials,Winner=winner?.Id,Cpu=(process.TotalProcessorTime-cpu).TotalSeconds,
            Wall=watch.Elapsed.TotalSeconds,Steps=steps,NativeWorlds=0},true);
        if(winner is not null)Save(Path.Combine(output,"candidate.json"),winner.Plan);

        CandidatePlan208 Change(CandidatePlan208 p,Func<Maneuver208,Maneuver208> transform)=>p with {
            Maneuvers=p.Maneuvers.Select(m=>m.Visit==visit?transform(m):m).ToArray()};
        Maneuver208 Mutate(Maneuver208 m,double scale)
        {
            double N(double x,double width,double lo,double hi)=>Math.Clamp(x+(random.NextDouble()*2-1)*width*scale,lo,hi);
            var s=m.Shape;int mode=random.Next(6);
            if(mode is 0 or 1 or 5)s=s with {Coast=N(s.Coast,2.2,0,10),MidPace=N(s.MidPace,1.8,0,10),
                ExitPace=N(s.ExitPace,2.4,0,11),Ramp=N(s.Ramp,.35,-.65,.4)};
            if(mode is 1 or 2 or 4 or 5)s=s with {EntryLine=N(s.EntryLine,.35,-1.4,1.5),
                ApexLine=N(s.ApexLine,1,-4,5),ExitLine=N(s.ExitLine,.5,-2,2),ApexShift=N(s.ApexShift,4,-18,22),
                EntrySteer=N(s.EntrySteer,.013,-.08,.08),LateSteer=N(s.LateSteer,.016,-.08,.09)};
            if(mode is 3 or 4 or 5)s=s with {Turn=N(s.Turn,.16,-.7,.9),MidYaw=N(s.MidYaw,.20,-.8,1.2),
                ExitYaw=N(s.ExitYaw,.45,-1.3,3.2),ExitCourse=N(s.ExitCourse,.045,-.25,.2),
                Flatten=N(s.Flatten,6,6,44),Drift=N(s.Drift,.65,0,6),CourseGain=N(s.CourseGain,.6,0,5.5),
                Beta=N(s.Beta,.35,-1.5,2.2),RouteMix=N(s.RouteMix,.2,0,1),Geometry=N(s.Geometry,.2,0,1),
                Prepare=N(s.Prepare,6,18,58)};
            var tail=m.Recovery;
            if(mode is 0 or 3 or 5)tail=tail with {Steer=N(tail.Steer,.006,-.05,.05),
                Gas=N(tail.Gas,.12,-.4,.3),Gain=N(tail.Gain,.09,0,.7)};
            return m with {Shape=s,Recovery=tail};
        }
    }
}
