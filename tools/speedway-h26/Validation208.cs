using System.Diagnostics;
using System.IO.Compression;
using CoreSim.PhysicalSpace;
using System.Text.Json;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Compare208(string root,string output,string[] sources)
    {
        var bank=ReadBase208(root);
        var bend=bank.Policy.Bend(3);
        var starts=Roots208(root,bank,[],3,false);
        double target=bend.Exit+28;
        var cases=new List<object>();
        foreach(var path in sources)
        {
            var pathPlan=ReadPlan208(path);
            var original=pathPlan.Maneuvers[0];
            var variants=new List<(string Label,Maneuver208 Patch)> {
                ("original",original),("steering-only under identical geometry",original with {Speed=false})
            };
            foreach(var lead in new[]{6d,12,18,24,30,36,42,50})
                variants.Add(($"prepare {lead:F0} m",original with {Shape=original.Shape with {Prepare=lead}}));
            foreach(var (label,patch) in variants)
            {
                var set=new Dictionary<int,Maneuver208>{{3,patch}};
                var results=starts.Select(x=>Follow208(x,bank.Policy,bend,at=>bank.Policy.Action(at,set),target,record:true)).ToArray();
                cases.Add(new {Source=new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!).Name,
                    Label=label,Maneuver=patch,Runs=results});
            }
        }
        Save(Path.Combine(output,"paired-training.json.gz"),new {Marks=new {bend.Entry,bend.Exit,Target=target},
            Training=TrainingStarts207,NativeWorlds=0,Cases=cases},true);
    }

    private static readonly Dictionary<string,float> Screening208=new()
    {
        ["screen-minus-six"]=-.06f, ["screen-plus-six"]=.06f,
        ["screen-minus-sixteen"]=-.16f, ["screen-plus-sixteen"]=.16f
    };

    private static (Seed198 Seed,RiderSpec Spec) CourseFixture208(string root,string fixture)
    {
        if(!Screening208.TryGetValue(fixture,out float offset))
        {
            var (original,_,registered)=Fixture207(root,fixture);
            return (original,registered);
        }
        var (seed,_,spec)=Fixture206(root,"C");
        spec=spec with {OffsetMeters=spec.OffsetMeters+offset,TargetOffsetMeters=spec.TargetOffsetMeters+offset};
        var pose=Frame.At(spec.ReferenceMeters,spec.OffsetMeters);
        var rider=seed.Start.State.Rider with {Position=pose.Position,Road=Frame.Project(pose.Position),
            Heading=(float)pose.Road.Heading,TargetOffsetMeters=(float)spec.TargetOffsetMeters};
        return (seed with {Start=seed.Start with {State=seed.Start.State with {Rider=rider}}},spec);
    }

    private static Leg208[] FullPlan208(string root,Base208 source,CandidatePlan208 plan,IEnumerable<string> fixtures)
    {
        if(plan.Base.Branch!=source.Plan.Branch) throw new InvalidOperationException("Invalid upstream baseline identity.");
        var bank=source.Policy;
        var set=plan.Maneuvers.ToDictionary(x=>x.Visit);
        var law=(Start197 state)=>bank.Action(state,set);
        var bend=bank.Bend(3);
        return fixtures.Select(fixture=>
        {
            var (seed,spec)=CourseFixture208(root,fixture);
            return Follow208(new(fixture,Model208(spec),seed.Start,seed.Goal),bank,bend,law,seed.Goal,
                10000,full:true,record:true);
        }).ToArray();
    }

    private static void Freeze208(string root,string output,string[] input)
    {
        var before=ReadBase208(root);
        var trials=TrainingStarts207.Concat(Screening208.Keys).ToArray();
        var candidates=new List<(string Source,CandidatePlan208 Plan)>();
        foreach(var path in input)
        {
            var plan=ReadPlan208(path);
            var directory=Path.GetDirectoryName(Path.GetFullPath(path))!;
            string label=new DirectoryInfo(directory).Name;
            candidates.Add((label,plan));
            string archive=Path.Combine(directory,"arm-x6.json.gz");
            if(plan.Maneuvers.Length!=3||!File.Exists(archive))continue;
            using var compressed=new GZipStream(File.OpenRead(archive),CompressionMode.Decompress);
            using var doc=JsonDocument.Parse(compressed);
            // A top partial can carry reserve while missing the finish. Reconsider every
            // exact training-complete lineage, with a fixed shortlist cap per root.
            var qualifying=doc.RootElement.GetProperty("Trials").EnumerateArray()
                .Where(branch=>branch.GetProperty("Runs").EnumerateArray().All(x=>x.GetProperty("Finish").GetBoolean()))
                .OrderByDescending(branch=>branch.GetProperty("Runs").EnumerateArray().Min(x=>x.GetProperty("LegalSweep").GetDouble()))
                .ThenBy(branch=>branch.GetProperty("Id").GetInt32()).Take(12);
            foreach(var branch in qualifying)
            {
                int id=branch.GetProperty("Id").GetInt32();
                var final=branch.GetProperty("Maneuver").Deserialize<Maneuver208>(Json)!;
                candidates.Add(($"{label}/{id}",plan with {Maneuvers=[..plan.Maneuvers.SkipLast(1),final]}));
            }
        }
        var options=candidates.DistinctBy(x=>JsonSerializer.Serialize(x.Plan,Json)).Select((x,i)=>
        {
            var runs=FullPlan208(root,before,x.Plan,trials);
            return new {Index=i,x.Source,x.Plan,Runs=runs};
        }).ToArray();
        var leader=Beam207.RankRobust(options,x=>x.Runs.Min(r=>Math.Min(3*Frame.LapMeters,r.LegalS)),
            x=>x.Runs.Count(r=>r.Finish),x=>x.Runs.Min(r=>r.LegalSweep),
            x=>x.Runs.Average(r=>r.FinishSeconds??85)).First();
        Save(Path.Combine(output,"training-selection.json.gz"),new {Training=trials,Screening208,
            NativeWorlds=0,Winner=leader.Index,Options=options},true);
        Save(Path.Combine(output,"frozen-candidate.json"),leader.Plan);
        Console.WriteLine($"Frozen {leader.Source}; fourth {leader.Runs.Count(x=>x.Stabilization)}/{trials.Length}; " +
            $"S3 {leader.Runs.Count(x=>x.Finish)}/{trials.Length}; worst accepted {leader.Runs.Min(x=>x.LegalS):F3}.");
    }

    private sealed record NativeResult208(string Fixture,bool Eligible,bool Finished,string Outcome,int CompletedLaps,
        double? Finish,double? Sweep,int Contacts,DecisionTiming206 Decisions,RacingFailure206? FirstFailure,
        RacingFailure206? IndependentFailure,double Cpu,double Wall);

    private static void Native208(string root,string output,string fixture,string planPath)
    {
        if(!Official208.Contains(fixture)) throw new ArgumentException("Unknown official fixture.");
        var source=ReadBase208(root);
        var plan=ReadPlan208(planPath);
        var preflight=FullPlan208(root,source,plan,Official208);
        if(preflight.Any(x=>!x.Finish))
        {
            Save(Path.Combine(output,"ineligible-native.json.gz"),new {Preflight=preflight,NativeWorlds=0},true);
            return;
        }

        int fixtureIndex=Array.IndexOf(Official208,fixture);
        foreach(var previous in Official208.Take(fixtureIndex))
        {
            string resultPath=Path.Combine(output,"H29-V-"+previous+".result.json");
            if(!File.Exists(resultPath)||JsonSerializer.Deserialize<NativeResult208>(File.ReadAllText(resultPath),Json) is not {Finished:true,Contacts:0})
                throw new InvalidOperationException("Native series requires every previous fixture to finish without a technical failure.");
        }

        var (seed,gate,spec)=Fixture206(root,fixture);
        var set=plan.Maneuvers.ToDictionary(x=>x.Visit);
        var law=(Start197 state)=>source.Policy.Action(state,set);
        var settings=Settings202("H28-F");
        var strict=new RacingProfile206(Map199);
        var online=strict.Begin(seed.Start);
        var model=Model208(spec,timing:true);
        var rig=new HandlingRig(settings);
        string scenario="H29-V-"+fixture;
        RequireLedger203(scenario);
        // A reservation is made by the central adapter immediately before construction.
        using var world=new Cyclic172.TrafficWorld191(seed.Archive.Run.Options,Frame,[spec],[rig],Timing,[gate]);
        var timings=new List<double>();
        var details=new List<object>();
        RacingFailure206? failure=null;
        var watch=Stopwatch.StartNew();
        try
        {
            var run=world.Run(scenario,"K1",(input,crank,_,_)=>
            {
                long tick=Stopwatch.GetTimestamp();
                var observed=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,crank.Omega),
                    input.Active,input.Pending,input.Time)
                    {RiderHeightOffsetMeters=rig.RiderHeightOffsetMeters,TechniqueHistory=online.Capture()};
                var action=law(observed);
                var forecast=model.BeginFeedback207(observed,law,seed.Goal,.5,profile:true);
                forecast.Advance(40,input.Time+.25);
                double ms=Stopwatch.GetElapsedTime(tick).TotalMilliseconds;
                timings.Add(ms);
                details.Add(new {State=observed,Action=action,Milliseconds=ms,forecast.Outcome,forecast.Sweep,forecast.Counters});
                if(!forecast.Reached&&(forecast.Outcome is not null||forecast.Snapshot.Time<input.Time+.25-1e-8))
                {
                    failure=forecast.StrictFailure206;
                    throw new InvalidOperationException($"Strict native receding guard at {input.Time:F3}: {forecast.Outcome}.");
                }
                return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),ms/1000);
            },strictCheck:(sample,finish)=>
            {
                failure=online.Check(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate!.Value,.5,
                    RacingProfile206.Physical(sample.Input.Rider,sample.Input.Time,
                        (float)sample.Powertrain!.Seconds,sample.Forces!.Value,sample.Powertrain),finish);
                return failure?.Condition;
            });
            watch.Stop();
            var rider=run.Riders.Single();
            var independent=strict.Begin();
            RacingFailure206? first=null;
            if(rider.Samples.Length!=0)
            {
                independent.ObserveInitial(rider.Samples[0].Input.Rider,rider.Samples[0].Input.Time);
                foreach(var sample in rider.Samples.Skip(1))
                {
                    first=independent.Check(sample.Input.Rider,sample.Input.Time,sample.Outer,sample.Certificate!.Value,.5,
                        RacingProfile206.Physical(sample.Input.Rider,sample.Input.Time,
                            (float)sample.Powertrain!.Seconds,sample.Forces!.Value,sample.Powertrain),
                        sample.Input.Rider.TotalReferenceMeters>=seed.Goal);
                    if(first is not null)break;
                }
            }
            double? sweep=rider.Samples.Select(x=>x.Certificate).DefaultIfEmpty(null).Min();
            bool complete=run.Outcome=="completed"&&rider.CompletedLaps==3&&rider.FinishSeconds.HasValue&&
                run.Contacts.Length==0&&sweep>=.5&&first is null&&failure is null;
            var result=new NativeResult208(fixture,true,complete,run.Outcome,rider.CompletedLaps,rider.FinishSeconds,
                sweep,run.Contacts.Length,DecisionTiming206.Measure(timings),failure,first,run.TotalCpuSeconds,watch.Elapsed.TotalSeconds);
            Save(Path.Combine(output,scenario+".json.gz"),new {Result=result,Run=run,Handling=settings,
                Preflights=preflight,Decisions=details,Posture=rig.PostureSamples,Loads=rig.LoadSamples},true);
            Save(Path.Combine(output,scenario+".result.json"),result);
        }
        catch(InvalidOperationException error)
        {
            Save(Path.Combine(output,scenario+".technical-failure.json.gz"),new {Fixture=fixture,Message=error.Message,
                Failure=failure,Preflights=preflight,Decisions=details,Timing=DecisionTiming206.Measure(timings)},true);
            throw;
        }
    }
}
