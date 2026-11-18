using System.Security.Cryptography;
using System.Text.Json;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void NativeMatched201(string workspace,string output,bool archivedController=false)
    {
        var (source,start,index,goal)=MatchedRoot201(workspace);
        var driver=source.Run.Riders.Single();
        var learned=Read199<Training199>(Path.Combine(output,"matched-training.json.gz")).Winner.Result;
        if(archivedController)
        {
            var original=Read199<Training199>(Path.Combine(workspace,"outputs","badanie200","training-C.json.gz")).Winner.Result;
            learned=Planner201(driver.Spec,goal,true).Transfer(start,original,true);
        }
        string arm=archivedController?"matched-C200":"matched";
        if(!learned.Legal)throw new InvalidOperationException("No legal complete matched candidate.");
        var model=Model199(driver.Spec);
        var commands=Pad200(learned.Controls!);
        var calibration=JsonSerializer.Deserialize<Calibration198>(File.ReadAllText(
            Path.Combine(workspace,"outputs","badanie198","kalibracja.json")),Json)!;
        var seed=new Seed198("201-matched",source,index,start,goal,commands);
        var complete=model.Begin(start,commands,0,goal,.502,constraint:Corridor().Check);
        complete.Advance(10000);
        if(!complete.Reached)throw new InvalidOperationException("Matched certificate failed.");
        var controller=new Receding198(model,calibration,seed,commands,complete.GoalTime,style:Corridor().Check);
        using var budget=new FileStream(Path.Combine(output,"world-budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(Directory.GetFiles(output,"*.reserved").Length>=5)throw new InvalidOperationException("Five native worlds used.");
        Save(Path.Combine(output,arm+".reserved"),new{Arm=arm,Goal=goal,
            Executable=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Entry201).Assembly.Location)))});
        int matched=0;
        var rig=new HandlingRig(Handling);
        var old=CreateWideMinds([driver.Spec]).Single();
        using var world=new TrafficWorld191(source.Run.Options,Frame,[driver.Spec],[rig],Timing,[driver.Gate],goal);
        var run=world.Run("201-matched","K1",(input,shaft,_,snapshot)=>
        {
            if(input.Time<start.Review-1e-9)
            {
                var prior=old.Choose(input,shaft,rig.Lean,snapshot);
                return new(new(input.Time,prior.Action,FreeActionSafety.Uncertain,false,[],0,1),0);
            }
            if(matched!=index+1)throw new InvalidOperationException("The #196 prefix was not reproduced.");
            var live=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,shaft.Omega),
                input.Active,input.Pending,input.Time);
            var clock=System.Diagnostics.Stopwatch.StartNew();
            var action=controller.Choose(live);
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),clock.Elapsed.TotalSeconds);
        },(_,sample,lean)=>
        {
            if(matched>index)return;
            if(!Equal199(sample,driver.Samples[matched]) || lean!=source.Lean[0][matched])
                throw new InvalidOperationException($"#196 prefix mismatch {matched}.");
            matched++;
        });
        var r=run.Riders.Single();
        double min=r.Samples.Where(x=>x.Certificate.HasValue).Min(x=>x.Certificate!.Value);
        var result=new{Prefix=matched,ExpectedPrefix=index+1,Goal=goal,End=r.Samples[^1].Input.Rider.TotalReferenceMeters,
            Legal=min>=.5 && run.Contacts.Length==0 && r.Samples[^1].Input.Rider.TotalReferenceMeters>=goal,
            MinSweep=min,run.Outcome,run.Fingerprint};
        Save(Path.Combine(output,arm+"-native.json.gz"),new{Run=run,Result=result,Timing,Handling,
            Lean=new[]{rig.LeanSamples},Planner=controller.Journal,controller.Patches},true);
        Console.WriteLine(JsonSerializer.Serialize(result,Json));
    }
}
