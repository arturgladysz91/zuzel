using System.Numerics;
using SharedWorldSpeedway;
namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Scenarios202(string workspace,string output)
    {
        var source=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie200/C-native.json.gz"));
        var driver=source.Run.Riders.Single();
        int At(double seconds)=>Array.FindIndex(driver.Samples,s=>s.Input.Time>=seconds);
        var names=new[]{"standing launch","straight acceleration","full throttle fast straight","throttle closure",
            "controlled corner slip","corner exit drive","drive to engine braking","reopen after coast"};
        var first=Map199.At(0,0);var second=Map199.At(0,1);
        int Reference(double s)=>Array.FindIndex(driver.Samples,x=>x.Input.Rider.TotalReferenceMeters>=s);
        var fast=driver.Samples.Where(x=>x.Input.Rider.TotalReferenceMeters>first.Exit&&x.Input.Rider.TotalReferenceMeters<second.Entry-2)
            .MaxBy(x=>x.Input.Rider.Speed)!;
        var slide=driver.Samples.Where(x=>x.Input.Rider.TotalReferenceMeters>first.Entry&&x.Input.Rider.TotalReferenceMeters<first.Exit)
            .MaxBy(x=>Math.Abs(x.Input.Rider.SlideRadians))!;
        var seconds=new[]{.35,driver.Samples[Reference(first.Entry-15)].Input.Time,fast.Input.Time,fast.Input.Time,
            slide.Input.Time,driver.Samples[Reference(first.Exit+3)].Input.Time,fast.Input.Time,15d};
        var gas=new[]{.4,1d,1d,0d,.5,1d,0d,1d};
        var rows=new List<object>();
        for(int scenario=0;scenario<names.Length;scenario++)
        foreach(string variant in new[]{"H25","H26-L","H26-E","H26-LE"})
        foreach(float dt in new[]{.005f,.01f,.02f}){
            var scenarioSource=source;var scenarioDriver=driver;
            if(scenario==7){
                scenarioSource=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie202/H26-LE-learned-C.json.gz"));
                scenarioDriver=scenarioSource.Run.Riders.Single();
            }
            int ix=scenario==7?Array.FindIndex(scenarioDriver.Samples,s=>s.Forces?.TyreLoads is {RearNormalNewtons:<700}&&
                s.Input.Active.Drive<.001&&s.Input.Rider.Speed>10):At(seconds[scenario]);
            var sample=scenarioDriver.Samples[ix];var x=sample.Input;
            var settings=Settings202(variant);
            var shaft=new CrankClutch144(omega:sample.CrankOmega,lossH26:settings.H26?.EngineLoss);
            var wheel=Speed148.ForecastFork148.Wheel(x.Omega,Physics.RearWheel);
            try{
                var step=HandlingContact.Prepare(settings,Surface.SampleSurface(0,2),x.Rider with {Drive=(float)gas[scenario]},
                    scenarioDriver.Spec,Physics,x.Time,Mass,Inertia,wheel,x.Steering,scenarioSource.Lean[0][ix].ActualRadians,
                    dt,x.Active.SteeringRadians,shaft,0);
                rows.Add(new{Scenario=names[scenario],Variant=variant,Dt=dt,SourceTime=x.Time,
                    Classification="same archived state, force reevaluation; NOT native trajectory",Gas=gas[scenario],
                    SourceReferenceMeters=x.Rider.TotalReferenceMeters,
                    SpeedKmh=x.Rider.Speed*3.6,Ax=Vector2.Dot(step.Forces.NetForce,step.Forces.BodyForward)/Mass,
                    Forces=step.Forces,Powertrain=shaft.Prepared,Loads=step.LoadH26});
            }catch(InvalidOperationException e)when(e.Message.StartsWith("H26 wheel unloading")){
                rows.Add(new{Scenario=names[scenario],Variant=variant,Dt=dt,SourceTime=x.Time,
                    Classification="model validity rejection, NOT native DNF",Diagnostic=e.Message});
            }
        }
        var convergence=new List<object>();
        int origin=At(2);
        var start=Sample199(driver.Samples[origin],source.Lean[0][origin]);
        var center=Frame.At(start.State.Rider.TotalReferenceMeters,6);
        start=start with {State=start.State with {Rider=start.State.Rider with {
            Position=center.Position,Road=Frame.Project(center.Position),TargetOffsetMeters=6}}};
        start=start with {Pending=[],Review=start.State.Time};
        foreach(string variant in new[]{"H25","H26-L","H26-E","H26-LE"})
        foreach(double dt in new[]{.02,.01,.005,.0025}){
            var model=new Predictor198(Frame,Surface,Physics,Settings202(variant),Timing,driver.Spec,Mass,Inertia);
            var controls=Enumerable.Repeat(new FreeRacingAction(0,.4f),20).ToArray();
            var cursor=model.Begin(start,controls,0,1e5,.50,stepSeconds:dt);
            cursor.Advance(1000,start.State.Time+.4);
            convergence.Add(new{Variant=variant,Dt=dt,cursor.Outcome,End=cursor.Snapshot,cursor.Sweep});
        }
        Save(Path.Combine(output,"eight-scenarios-model.json.gz"),rows,true);
        Save(Path.Combine(output,"dt-convergence-model.json"),convergence);
        Console.WriteLine($"{rows.Count} contact cases, {convergence.Count} dt rollouts; zero native worlds.");
    }
}
