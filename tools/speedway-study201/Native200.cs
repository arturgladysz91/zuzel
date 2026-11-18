using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim.PhysicalSpace;
using Cyclic172;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Native200(string workspace,string output,string arm,bool dry)
    {
        if(arm is not ("C" or "B" or "C-alt" or "C-gear" or "C-confirm"))
            throw new ArgumentException("Expected a solo #200 fixture.");
        string variant=arm[..1];
        var (seed,gate,fixtureSpec)=Fixture200(workspace,arm);
        var (certificate,technique)=StyleFor200(arm);
        if(arm=="C")
        {
            certificate=new Corridor199(Map199,double.PositiveInfinity);
            technique=new(certificate.Windows);
        }
        var visual=technique?.Begin();
        string selected=arm=="C"?"C-refined":arm;
        string dryFile=Path.Combine(output,"dry-"+selected+".json.gz");
        var learned=Read199<Training199>(Path.Combine(output,"training-"+selected+".json.gz")).Winner.Result;
        if(!learned.Legal || learned.Controls is null)
            throw new InvalidOperationException("The changed state has no certified complete continuation.");
        var controls=learned.Controls.Concat(Enumerable.Repeat(learned.Controls[^1],12)).ToArray();
        var calibration=JsonSerializer.Deserialize<Calibration198>(File.ReadAllText(
            Path.Combine(workspace,"outputs/badanie198/kalibracja.json")),Json)!;
        var driver=seed.Archive.Run.Riders.Single();
        StartingGate[] gates=[gate];
        RiderSpec[] specs=[fixtureSpec];
        bool matched=Equal199(fixtureSpec,driver.Spec) && gate==driver.Gate;
        var model=Model199(fixtureSpec);
        Func<RiderView,double,string?> constraint=certificate.Check;
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
                    while(cursor.Outcome is null && cursor.Snapshot.Time<live.State.Time+.05-1e-9)
                    {
                        cursor.Advance(1,live.State.Time+.05);
                        if(visual?.Observe(cursor.Checkpoint().State.Rider,cursor.Snapshot.Time) is {} violation)
                            throw new InvalidOperationException(violation);
                    }
                    live=cursor.Checkpoint();sweep=Math.Min(sweep,cursor.Sweep);
                    if(cursor.Outcome is not null){exit=cursor.Outcome;break;}
                }
            }
            catch(InvalidOperationException error){exit=error.Message;}
            Save(dryFile,new {Arm=arm,Complete=exit=="goal",Reason=exit,End=live.State.Time,
                Sweep=sweep,Planner=agent.Journal,agent.Patches},true);
            Console.WriteLine($"dry {arm}: {exit}, s={live.State.Rider.TotalReferenceMeters:F2}, t={live.State.Time:F6}, swept={sweep:F6}, changes={agent.AcceptedPatches}");
            return;
        }

        using(var check=File.OpenRead(dryFile))
        using(var compressed=new System.IO.Compression.GZipStream(check,System.IO.Compression.CompressionMode.Decompress))
        using(var decision=JsonDocument.Parse(compressed))
            if(!decision.RootElement.GetProperty("Complete").GetBoolean())
                throw new InvalidOperationException("Offline receding-control trial did not finish.");
        using var budget=new FileStream(Path.Combine(output,"world-budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(Directory.GetFiles(output,"*.reserved").Length>=5)
            throw new InvalidOperationException("All approved solo worlds are used.");
        string executable=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Entry201).Assembly.Location)));
        Save(Path.Combine(output,arm+".reserved"),new{Arm=arm,Executable=executable,Goal=seed.Goal});
        int replayed=0,styleErrors=0;
        var styleReasons=new Dictionary<string,int>();
        var rig=new HandlingRig(Handling);
        var launch=CreateWideMinds(specs).Single();
        using var world=new TrafficWorld191(seed.Archive.Run.Options,Frame,specs,[rig],Timing,gates);
        var run=world.Run("200-"+arm,"K1",(input,shaft,spec,snapshot)=>
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
                if(i!=0 || matched && (!Equal199(sample,driver.Samples[replayed]) || lean!=seed.Archive.Lean[0][replayed]))
                    throw new InvalidOperationException($"Prefix mismatch, first unmatched sample {replayed}.");
                replayed++;
            }
            if(sample.Input.Time>=seed.Start.Review-1e-9)
            {
                var issue=constraint(sample.Input.Rider,sample.Outer)??visual?.Observe(sample.Input.Rider,sample.Input.Time);
                if(issue is not null)
                {
                    styleErrors++;
                    styleReasons[issue]=styleReasons.GetValueOrDefault(issue)+1;
                }
            }
        });
        var rider=run.Riders.Single();
        double? minSwept=rider.Samples.Select(x=>x.Certificate).Where(x=>x.HasValue).DefaultIfEmpty().Min();
        bool stable=run.Outcome=="completed" && rider.CompletedLaps==Laps && minSwept>=.5 &&
            run.Contacts.Length==0 && replayed==seed.Branch+1;
        bool legal=stable && styleErrors==0;
        var result=new{Arm=arm,Variant=variant,Legal=legal,Stable=stable,StyleErrors=styleErrors,Finish=rider.FinishSeconds,
            GoalReference=seed.Goal,run.Outcome,run.EndSeconds,run.TotalCpuSeconds,run.NativeSteps,
            run.Fingerprint,rider.CompletedLaps,MinSweep=minSwept,Prefix=seed.Branch+1,PrefixMatched=replayed,MatchedNominal=matched,Ratio=10-2*fixtureSpec.Setup.Gearing,StyleReasons=styleReasons,
            StartTime=seed.Start.Review,agent.AcceptedPatches,Executable=executable,world.ContactManifolds,world.SolvedPoints};
        Save(Path.Combine(output,arm+"-native.json.gz"),new{Run=run,Result=result,Timing,Handling,
            Lean=new[]{rig.LeanSamples},Journal=new[]{launch.Journal},Planner=agent.Journal,agent.Patches,
            world.ContactPoints,world.Consequences},true);
        Save(Path.Combine(output,arm+"-native.result.json"),result);
        Console.WriteLine($" #200 {arm}: stable={stable} legal={legal} style errors={styleErrors}, t={rider.FinishSeconds:F6}, " +
            $"swept={minSwept:F6}, switches={agent.AcceptedPatches}, {run.Outcome}");
    }

}
