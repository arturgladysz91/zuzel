using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Native199(string workspace,string output,string arm,bool dry)
    {
        if(arm is not ("A" or "B" or "A-held" or "B-held" or "A-second" or "B-second" or "B-smooth"))
            throw new ArgumentException("Expected a legal solo fixture A/B, optionally held or second.");
        string variant=arm[..1];
        var certificate=Corridor(arm=="B-smooth");
        var seed=Seed199(workspace,arm);
        var learned=(arm.Length==1 || arm=="B-smooth")?Read199<Training199>(Path.Combine(output,"training-"+arm+".json.gz")).Winner.Result:
            Read199<Trial199>(Path.Combine(output,"transfer-"+arm+".json.gz"));
        if(!learned.Legal || learned.Controls is null)
            throw new InvalidOperationException("The changed state has no certified complete continuation.");
        var controls=learned.Controls.Concat(Enumerable.Repeat(learned.Controls[^1],12)).ToArray();
        var calibration=JsonSerializer.Deserialize<Calibration198>(File.ReadAllText(
            Path.Combine(workspace,"outputs/badanie198/kalibracja.json")),Json)!;
        var driver=seed.Archive.Run.Riders.Single();
        var (gates,specs)=Grid194("solo-wide");
        if(!Equal199(specs[0],driver.Spec) || gates[0]!=driver.Gate)
            throw new InvalidOperationException("The source grid differs.");
        var model=Model199(specs[0]);
        var constraint=variant=="B"?certificate.Check:(Func<RiderView,double,string?>?)null;
        var complete=model.Begin(seed.Start,controls,0,seed.Goal,Predictor198.Clearance+calibration.BaseMargin,constraint:constraint);
        complete.Advance(10000);
        if(!complete.Reached || Math.Abs(complete.GoalTime-learned.Finish)>1e-7)
            throw new InvalidOperationException("The learned profile failed replay of the full certificate.");
        var agent=new Receding198(model,calibration,seed,controls,complete.GoalTime,style:constraint);
        if(dry)
        {
            var prep=model.Begin(seed.Start,[],0,seed.Goal,.5,constraint:constraint);
            prep.Advance(20,seed.Start.Review);
            var live=prep.Checkpoint();
            double sweep=100;
            string? exit=null;
            try
            {
                for(int i=0;i<1800;i++)
                {
                    var request=agent.Choose(live);
                    var cursor=model.Begin(live,[request],0,seed.Goal,.5,constraint:constraint);
                    cursor.Advance(10,live.State.Time+.05);
                    live=cursor.Checkpoint();sweep=Math.Min(sweep,cursor.Sweep);
                    if(cursor.Outcome is not null){exit=cursor.Outcome;break;}
                }
            }
            catch(InvalidOperationException error){exit=error.Message;}
            Save(Path.Combine(output,"dry-"+arm+".json.gz"),new {Arm=arm,Complete=exit=="goal",Reason=exit,End=live.State.Time,
                Sweep=sweep,Planner=agent.Journal,agent.Patches},true);
            Console.WriteLine($"dry {arm}: {exit}, s={live.State.Rider.TotalReferenceMeters:F2}, t={live.State.Time:F6}, swept={sweep:F6}, changes={agent.AcceptedPatches}");
            return;
        }

        using(var check=File.OpenRead(Path.Combine(output,"dry-"+arm+".json.gz")))
        using(var compressed=new System.IO.Compression.GZipStream(check,System.IO.Compression.CompressionMode.Decompress))
        using(var decision=JsonDocument.Parse(compressed))
            if(!decision.RootElement.GetProperty("Complete").GetBoolean())
                throw new InvalidOperationException("Offline receding-control trial did not finish.");
        using var budget=new FileStream(Path.Combine(output,"world-budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(Directory.GetFiles(output,"*.reserved").Length>=6)
            throw new InvalidOperationException("All approved solo worlds are used.");
        string executable=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Entry199).Assembly.Location)));
        Save(Path.Combine(output,arm+".reserved"),new{Arm=arm,Executable=executable,Goal=seed.Goal});
        int replayed=0,styleErrors=0;
        var rig=new HandlingRig(Handling);
        var launch=CreateWideMinds(specs).Single();
        using var world=new TrafficWorld191(seed.Archive.Run.Options,Frame,specs,[rig],Timing,gates);
        var run=world.Run("199-"+arm,"K1",(input,shaft,spec,snapshot)=>
        {
            var timer=Stopwatch.StartNew();
            FreeRacingAction action;
            if(input.Time<seed.Start.Review-1e-8)
                action=launch.Choose(input,shaft,rig.Lean,snapshot).Action;
            else
            {
                if(replayed!=seed.Branch+1)throw new InvalidOperationException("Unverified recorded start.");
                var x=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,shaft.Omega),
                    input.Active,input.Pending,input.Time);
                action=agent.Choose(x);
            }
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),timer.Elapsed.TotalSeconds);
        },(i,sample,lean)=>
        {
            if(replayed<=seed.Branch)
            {
                if(i!=0 || !Equal199(sample,driver.Samples[replayed]) || lean!=seed.Archive.Lean[0][replayed])
                    throw new InvalidOperationException($"Prefix mismatch, first unmatched sample {replayed}.");
                replayed++;
            }
            if(sample.Input.Time>=seed.Start.Review-1e-9 && constraint?.Invoke(sample.Input.Rider,sample.Outer) is not null)
                styleErrors++;
        });
        var rider=run.Riders.Single();
        double? minSwept=rider.Samples.Select(x=>x.Certificate).Where(x=>x.HasValue).DefaultIfEmpty().Min();
        bool stable=run.Outcome=="completed" && rider.CompletedLaps==Laps && minSwept>=.5 &&
            run.Contacts.Length==0 && replayed==seed.Branch+1;
        bool legal=stable && styleErrors==0;
        var result=new{Arm=arm,Variant=variant,Legal=legal,Stable=stable,StyleErrors=styleErrors,Finish=rider.FinishSeconds,
            GoalReference=seed.Goal,run.Outcome,run.EndSeconds,run.TotalCpuSeconds,run.NativeSteps,
            run.Fingerprint,rider.CompletedLaps,MinSweep=minSwept,Prefix=seed.Branch+1,PrefixMatched=replayed,
            StartTime=seed.Start.Review,agent.AcceptedPatches,Executable=executable,world.ContactManifolds,world.SolvedPoints};
        Save(Path.Combine(output,arm+"-native.json.gz"),new{Run=run,Result=result,Timing,Handling,
            Lean=new[]{rig.LeanSamples},Journal=new[]{launch.Journal},Planner=agent.Journal,agent.Patches,
            world.ContactPoints,world.Consequences},true);
        Save(Path.Combine(output,arm+"-native.result.json"),result);
        Console.WriteLine($"#199 {arm}: stable={stable} legal={legal} style errors={styleErrors}, t={rider.FinishSeconds:F6}, " +
            $"swept={minSwept:F6}, switches={agent.AcceptedPatches}, {run.Outcome}");
    }

    private static bool Equal199<T>(T a,T b)=>JsonSerializer.SerializeToUtf8Bytes(a,Json).AsSpan()
        .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(b,Json));
}
