using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using CoreSim;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Learned200(string Format,double Ratio,double Goal,double PredictedFinish,FreeRacingAction[] Controls);

internal static partial class Study191
{
    private static void CheckStadium200()
    {
        Schedule188.Check();
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SpeedwayHandling.Learned200")!;
        var learned=JsonSerializer.Deserialize<Learned200>(stream,Json)!;
        if(learned.Format!="speedway-controller200-v1" || learned.Ratio!=8 ||
            learned.Controls.Length!=927 || Math.Abs(learned.PredictedFinish-46.38809650884079)>1e-10)
            throw new InvalidOperationException("The preserved C #200 model changed.");
        foreach(double[]? ratios in new double[]?[]{null,[],[8,8],[8.4]})
        {
            try {RunStadium200("",ratios);}
            catch(ArgumentException){continue;}
            throw new InvalidOperationException("Invalid #200 setup reached native initialization.");
        }
        Console.WriteLine("PASS C #200 model, invalid opt-in setups, FAST50 review and immutable pending.");
    }

    // Existing command-line entry point opts into this isolated solo controller.
    private static void RunStadium200(string output,double[]? ratios)
    {
        if(ratios is not [8d])throw new ArgumentException("C #200 wymaga solo z przełożeniem 8:1.");
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SpeedwayHandling.Learned200")!;
        var learned=JsonSerializer.Deserialize<Learned200>(stream,Json)??throw new InvalidOperationException("Brak modelu #200.");
        if(learned.Format!="speedway-controller200-v1" || learned.Ratio!=8 || learned.Goal!=Laps*Frame.LapMeters ||
            learned.Controls.Length<900 || learned.Controls.Any(c=>!float.IsFinite(c.Drive)||c.Drive is <0 or >1 ||
                !float.IsFinite(c.SteeringRadians)))
            throw new InvalidOperationException("Niezgodny model C #200.");
        Directory.CreateDirectory(output);
        var spec=Spec193(StartingGate.D,1,8);
        var rig=new HandlingRig(Handling);
        var launch=CreateWideMinds([spec]).Single();
        var corridor=new Corridor199(new(Frame));
        var model=new Predictor198(Frame,Surface,Physics,Handling,Timing,spec,Mass,Inertia);
        var calibration=new Calibration198(.002,.012,.02,.08,.012);
        var timing=new StandingOptions144(Enabled:true,RequiredLaps:Laps,MaximumSeconds:85);
        Receding198? controller=null;
        double initializationCpu=0,initializationWall=0;
        var clock=Stopwatch.StartNew();
        using var world=new TrafficWorld191(timing,Frame,[spec],[rig],Timing,[StartingGate.D]);
        var run=world.Run("race","K1",(input,shaft,_,snapshot)=>
        {
            var timer=Stopwatch.StartNew();
            if(input.Time<.05-1e-9)
            {
                var first=launch.Choose(input,shaft,rig.Lean,snapshot);
                return new(new(input.Time,first.Action,FreeActionSafety.Uncertain,false,[],0,1),timer.Elapsed.TotalSeconds);
            }
            var actual=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,shaft.Omega),
                input.Active,input.Pending,input.Time);
            if(controller is null)
            {
                var beforeCpu=Process.GetCurrentProcess().TotalProcessorTime;
                var beforeWall=Stopwatch.StartNew();
                var plan=learned.Controls.Concat(Enumerable.Repeat(learned.Controls[^1],12)).ToArray();
                var certificate=model.Begin(actual,plan,0,learned.Goal,.502,constraint:corridor.Check);
                certificate.Advance(10000);
                if(!certificate.Reached)throw new InvalidOperationException("Stan początkowy nie ma bezpiecznej kontynuacji C #200.");
                var seed=new Seed198("speedway200",null!,0,actual,learned.Goal,plan);
                controller=new(model,calibration,seed,plan,certificate.GoalTime,style:corridor.Check);
                initializationCpu=(Process.GetCurrentProcess().TotalProcessorTime-beforeCpu).TotalSeconds;
                initializationWall=beforeWall.Elapsed.TotalSeconds;
                timer.Restart(); // Certificate setup is recorded separately from FAST50.
            }
            var action=controller.Choose(actual);
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),timer.Elapsed.TotalSeconds);
        });
        var rider=run.Riders.Single();
        bool complete=run.Outcome=="completed" && rider.CompletedLaps==Laps;
        var result=new{Arm="race",Controller="speedway200",Complete=complete,run.EndSeconds,run.Outcome,run.NativeSteps,
            run.TotalCpuSeconds,run.Fingerprint,TotalWallSeconds=clock.Elapsed.TotalSeconds,
            InitializationCpuSeconds=initializationCpu,InitializationWallSeconds=initializationWall,
            Finish=new[]{new{rider.FinishSeconds,rider.CompletedLaps,rider.Outcome}},world.ContactManifolds,world.SolvedPoints};
        Save(Path.Combine(output,"race.json.gz"),new{Run=run,Result=result,Timing,Handling,Lean=new[]{rig.LeanSamples},
            Journal=new[]{launch.Journal},Planner=controller?.Journal, Patches=controller?.Patches,
            world.ContactPoints,world.Consequences},true);
        Save(Path.Combine(output,"race.result.json"),result);
    }
}
