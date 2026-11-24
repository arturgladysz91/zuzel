using System.Diagnostics;
using System.Text.Json;
using System.IO.Compression;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Reconstruct209(string root,string output)
    {
        var source=ReadBase208(root);var geometry=Geometry207(root);
        var plan=ReadPlan208(Path.Combine(root,Evidence209,"frozen-candidate.json"));
        var law=Law209(source,geometry,plan);var rows=new List<object>();
        foreach(string fixture in Official208)
        {
            using var stream=new GZipStream(File.OpenRead(Path.Combine(root,Evidence209,"native/H29-S-"+fixture+".json.gz")),CompressionMode.Decompress);
            using var doc=JsonDocument.Parse(stream);int count=0,differences=0;
            var clamps=new List<object>();
            foreach(var decision in doc.RootElement.GetProperty("Decisions").EnumerateArray())
            {
                var state=decision.GetProperty("State").Deserialize<Start197>(Json)!;
                var expected=decision.GetProperty("Action").Deserialize<FreeRacingAction>(Json);
                var captured=ThrottleTrace209.Capture(()=>law(state));
                if(captured.Action!=expected)differences++;
                clamps.Add(new {State=state,Control=captured.Trace});
                count++;
            }
            rows.Add(new {Fixture=fixture,Count=count,Differences=differences});
            Save(Path.Combine(output,"native-clamps-"+fixture+".json.gz"),clamps,true);
            if(differences!=0)throw new InvalidOperationException("Published native action mismatch.");
            Console.WriteLine($"{fixture}: {count}/{count} final native commands exact");
        }
        Save(Path.Combine(output,"native-command-reconstruction.json"),new {Rows=rows,NativeWorlds=0});
    }

    private static void Native209(string root,string output,string fixture,string planPath)
    {
        if(!Official208.Contains(fixture)) throw new ArgumentException("Unknown official fixture.");
        var source=ReadBase208(root);
        var plan=ReadPlan208(planPath);
        var geometry=Geometry207(root);
        var preflight=Official208.Select(f=>RunPlan209(root,source,geometry,plan,f)).ToArray();
        if(preflight.Any(x=>!x.Complete))
        {
            Save(Path.Combine(output,"ineligible-native.json.gz"),new {Preflight=preflight,NativeWorlds=0},true);
            return;
        }

        RunNativeFeedback210(root,output,fixture,"H29-S",preflight,Law209(source,geometry,plan),
            Law209(source,geometry,plan with {Recovery209=null}));
    }

    private static void RunNativeFeedback210(string root,string output,string fixture,string label,Run209[] preflight,
        Func<Start197,FreeRacingAction> law,Func<Start197,FreeRacingAction> fallback)
    {
        int fixtureIndex=Array.IndexOf(Official208,fixture);
        foreach(var previous in Official208.Take(fixtureIndex))
        {
            string resultPath=Path.Combine(output,label+"-"+previous+".result.json");
            if(!File.Exists(resultPath)||JsonSerializer.Deserialize<NativeResult208>(File.ReadAllText(resultPath),Json) is not {Finished:true,Contacts:0})
                throw new InvalidOperationException("Native series requires every previous fixture to finish without a technical failure.");
        }

        var (seed,gate,spec)=Fixture206(root,fixture);
        var settings=Settings202("H28-F");
        var strict=new RacingProfile206(Map199);
        var online=strict.Begin(seed.Start);
        var model=Model208(spec,timing:true);
        var rig=new HandlingRig(settings);
        string scenario=label+"-"+fixture;
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
                var forecast=model.BeginFeedback207(observed,at=>at.State.Time<observed.State.Time+.025?action:fallback(at),seed.Goal,.5,profile:true);
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
