using System.Diagnostics;
using System.Text.Json;
using Cyclic172;
using SharedWorldSpeedway;
namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void NativeLearned202(string workspace,string output,string variant,string training,string fixture)
    {
        var (seed,gate,spec)=Fixture200(workspace,fixture);
        seed=seed with {Start=seed.Start with {Pending=[],Review=0}};
        var settings=Settings202(variant);
        var learned=Read199<Training199>(training).Winner.Result;
        if(!learned.Legal||learned.Controls is null)throw new InvalidOperationException("No complete learned H26 continuation; native world not opened.");
        var controls=Pad200(learned.Controls);
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var model=new Predictor198(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia);
        var certificate=model.Begin(seed.Start,controls,0,seed.Goal,.502,constraint:corridor.Check);
        certificate.Advance(10000);
        if(!certificate.Reached||Math.Abs(certificate.GoalTime-learned.Finish)>1e-6)
            throw new InvalidOperationException("Learned H26 candidate failed exact complete replay; native world not opened.");
        var agent=new Receding198(model,new(.002,.012,.02,.08,.012),seed,controls,certificate.GoalTime,style:corridor.Check);
        string arm=variant+"-learned-"+fixture;
        Reserve202(output,arm);
        var rig=new HandlingRig(settings);
        using var world=new TrafficWorld191(seed.Archive.Run.Options,Frame,[spec],[rig],Timing,[gate]);
        var run=world.Run("202-"+arm,"K1",(input,shaft,_,snapshot)=>{
            var actual=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,shaft.Omega),
                input.Active,input.Pending,input.Time);
            var timer=Stopwatch.StartNew();
            var action=agent.Choose(actual);
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),timer.Elapsed.TotalSeconds);
        });
        var r=run.Riders.Single();
        double? min=r.Samples.Where(s=>s.Certificate.HasValue).Min(s=>s.Certificate);
        var durations=agent.Journal.Select(x=>x.Milliseconds).Order().ToArray();
        double P(double q)=>durations.Length==0?0:durations[(int)Math.Floor((durations.Length-1)*q)];
        var result=new{Variant=variant,Controller="H26 learned feedback with FAST50 receding verification",Fixture=fixture,
            Complete=run.Outcome=="completed"&&r.CompletedLaps==3&&min>=.5&&run.Contacts.Length==0,
            run.Outcome,r.FinishSeconds,run.EndSeconds,run.TotalCpuSeconds,r.CompletedLaps,run.NativeSteps,
            Contacts=run.Contacts.Length,MinSweep=min,EndReference=r.Samples[^1].Input.Rider.TotalReferenceMeters,
            DecisionP50Milliseconds=P(.5),DecisionP95Milliseconds=P(.95),DecisionP99Milliseconds=P(.99),
            MaxMilliseconds=durations.DefaultIfEmpty().Max(),run.Fingerprint,agent.AcceptedPatches};
        Save(Path.Combine(output,arm+".json.gz"),new{Run=run,Result=result,Timing,Handling=settings,
            Lean=new[]{rig.LeanSamples},Loads=new[]{rig.LoadSamples},Journal=new[]{Array.Empty<object>()},
            Planner=agent.Journal,agent.Patches,world.ContactPoints,world.Consequences},true);
        Save(Path.Combine(output,arm+".result.json"),result);
        Console.WriteLine(JsonSerializer.Serialize(result,Json));
    }
}
