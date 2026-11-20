using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Cyclic172;
using CoreSim;
using SharedWorldSpeedway;
namespace SpeedwayHandling;

internal sealed record PublishedPolicy202(string Format,string Variant,HandlingSettings Settings,
    RiderSpec Spec,StartingGate Gate,StandingOptions144 Options,Start197 Start,double Goal,
    double PredictedFinish,FreeRacingAction[] Controls,string NativeSha256);

internal static partial class Study191
{
    private static PublishedPolicy202[] Policies202()
    {
        using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("SpeedwayHandling.Policies202")!;
        return JsonSerializer.Deserialize<PublishedPolicy202[]>(stream,Json)!;
    }

    private static void PublishPolicies202(string workspace,string output)
    {
        var (seed,gate,spec)=Fixture200(workspace,"C");
        seed=seed with {Start=seed.Start with {Pending=[],Review=0}};
        var book=new List<PublishedPolicy202>();
        foreach(var (variant,folder) in new[]{("H26-L","l-cycle"),("H26-E","e-cycle"),("H26-LE","cycle-v8")}){
            string native=Path.Combine(output,variant+"-learned-C.json.gz");
            using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(output,variant+"-learned-C.result.json")));
            if(!result.RootElement.GetProperty("Complete").GetBoolean())throw new InvalidOperationException("Native validation required for publication.");
            var learned=Read199<Training199>(Path.Combine(output,folder,variant+"-training.json.gz")).Winner.Result;
            book.Add(new("speedway-h26-policy-v1",variant,Settings202(variant),spec,gate,seed.Archive.Run.Options,
                seed.Start,seed.Goal,learned.Finish,learned.Controls!,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(native)))));
        }
        Save(Path.Combine(output,"policies202.json"),book);
    }

    private static void CheckPolicies202(string output)
    {
        var book=Policies202();
        if(book.Length!=3)throw new InvalidOperationException("Three separately learned configurations required.");
        var rows=new List<object>();
        foreach(var policy in book){
            ValidatePolicy202(policy);
            var model=new Predictor198(Frame,Surface,Physics,policy.Settings,Timing,policy.Spec,Mass,Inertia);
            var corridor=new Corridor199(Map199,double.PositiveInfinity);
            var cursor=model.Begin(policy.Start,Pad200(policy.Controls),0,policy.Goal,.502,constraint:corridor.Check);
            cursor.Advance(10000);
            if(!cursor.Reached||Math.Abs(cursor.GoalTime-policy.PredictedFinish)>1e-6)
                throw new InvalidOperationException("Embedded policy replay differs from the learned certificate.");
            rows.Add(new{policy.Variant,cursor.GoalTime,cursor.Sweep,NativeWorlds=0});
        }
        foreach(var p in book){
            try { SelectPolicy202(new([8.4],p.Variant.ToLowerInvariant()));throw new InvalidOperationException("Incompatible ratio was accepted."); }
            catch(ArgumentException) { }
        }
        Save(Path.Combine(output,"published-policies-check.json"),rows);
        Console.WriteLine("PASS three compatible embedded H26 policies; no native worlds opened.");
    }

    private static void ValidatePolicy202(PublishedPolicy202 p)
    {
        if(p.Format!="speedway-h26-policy-v1"||p.Settings!=Settings202(p.Variant)||p.Start.Review!=0||
            p.Start.Pending.Length!=0||p.Start.State.Time!=0||p.Spec.SpeedMetersPerSecond!=0||
            10-2*p.Spec.Setup.Gearing!=8||p.NativeSha256.Length!=64||p.Goal!=3*Frame.LapMeters||p.Controls.Length<500)
            throw new InvalidOperationException("Explicit, compatible and natively verified H26 policy required.");
    }

    private static void Stadium202(string configPath,string output)
    {
        var config=JsonSerializer.Deserialize<StadiumConfig>(File.ReadAllText(configPath),Json)!;
        var policy=SelectPolicy202(config);
        string variant=policy.Variant;
        Directory.CreateDirectory(output);
        var model=new Predictor198(Frame,Surface,Physics,policy.Settings,Timing,policy.Spec,Mass,Inertia);
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var controls=Pad200(policy.Controls);
        var startup=Stopwatch.StartNew();
        var certificate=model.Begin(policy.Start,controls,0,policy.Goal,.502,constraint:corridor.Check);
        certificate.Advance(10000);
        if(!certificate.Reached)throw new InvalidOperationException("H26 initial continuation is not certified.");
        double initialization=startup.Elapsed.TotalSeconds;
        var seed=new Seed198(variant,null!,0,policy.Start,policy.Goal,controls);
        var agent=new Receding198(model,new(.002,.012,.02,.08,.012),seed,controls,certificate.GoalTime,style:corridor.Check);
        var rig=new HandlingRig(policy.Settings);
        using var world=new TrafficWorld191(policy.Options,Frame,[policy.Spec],[rig],Timing,[policy.Gate]);
        var run=world.Run("stadium-202-"+variant,"K1",(input,shaft,_,snapshot)=>{
            var actual=new Start197(new(input.Rider,input.Time,input.Omega,input.Steering,rig.Lean,shaft.Omega),input.Active,input.Pending,input.Time);
            var timer=Stopwatch.StartNew();var action=agent.Choose(actual);
            return new(new(input.Time,action,FreeActionSafety.Uncertain,false,[],0,1),timer.Elapsed.TotalSeconds);
        });
        var rider=run.Riders.Single();
        var result=new{Controller=config.Controller,Variant=variant,Complete=run.Outcome=="completed"&&rider.CompletedLaps==3,
            run.Outcome,run.EndSeconds,run.NativeSteps,run.TotalCpuSeconds,run.Fingerprint,
            Finish=new[]{new{rider.FinishSeconds,rider.CompletedLaps,rider.Outcome}},
            InitializationWallSeconds=initialization,DeadlineMisses=agent.Journal.Count(x=>x.Milliseconds>50),
            world.ContactManifolds,world.SolvedPoints};
        Save(Path.Combine(output,"race.json.gz"),new{Run=run,Result=result,Timing,Handling=policy.Settings,
            Lean=new[]{rig.LeanSamples},Loads=new[]{rig.LoadSamples},Journal=new[]{Array.Empty<object>()},
            Planner=agent.Journal,agent.Patches,world.ContactPoints,world.Consequences},true);
        Save(Path.Combine(output,"race.result.json"),result);
    }

    private static PublishedPolicy202 SelectPolicy202(StadiumConfig config)
    {
        var variant=config.Controller switch {"h26-l"=>"H26-L","h26-e"=>"H26-E","h26-le"=>"H26-LE",
            _=>throw new ArgumentException("Select an explicit H26 physics configuration.")};
        if(config.Ratios is not [8])throw new ArgumentException("H26 #202: solo 8:1 only. Other setups need a separately certified compatible policy.");
        var policy=Policies202().Single(x=>x.Variant==variant);
        ValidatePolicy202(policy);
        return policy;
    }

    private static void ReplayControllers202(string output)
    {
        var rows=new List<object>();
        foreach(var p in Policies202()){
            var source=Read199<Archive198>(Path.Combine(output,p.Variant+"-learned-C.json.gz"));
            var r=source.Run.Riders.Single();
            var model=new Predictor198(Frame,Surface,Physics,p.Settings,Timing,p.Spec,Mass,Inertia);
            var corridor=new Corridor199(Map199,double.PositiveInfinity);
            var controls=Pad200(p.Controls);
            var seed=new Seed198(p.Variant,null!,0,p.Start,p.Goal,controls);
            var agent=new Receding198(model,new(.002,.012,.02,.08,.012),seed,controls,p.PredictedFinish,style:corridor.Check);
            int matched=0;
            foreach(var expected in r.Decisions){
                double t=expected.Choice.Time;
                int i=Array.FindIndex(r.Samples,s=>Math.Abs(s.Input.Time-t)<1e-9);
                var s=r.Samples[i];var x=s.Input;
                var active=x.Active;
                var pending=x.Pending.Where(q=>q.ObservedAt<t-1e-9).ToList();
                Schedule188.Activate(pending,[],t,ref active);
                var actual=new Start197(new(x.Rider with {Drive=active.Drive},t,x.Omega,x.Steering,
                    source.Lean[0][i].ActualRadians,s.CrankOmega),active,pending.ToArray(),t);
                var action=agent.Choose(actual);
                if(action!=expected.Choice.Action)throw new InvalidOperationException($"Published controller mismatch {p.Variant} at {t:R}.");
                matched++;
            }
            rows.Add(new{p.Variant,DecisionsMatched=matched,agent.AcceptedPatches,NativeWorlds=0});
        }
        Save(Path.Combine(output,"runtime-controller-parity.json"),rows);
        Console.WriteLine("PASS all published decisions replayed against accepted native observations; zero new worlds.");
    }

    private static void StyleAudit202(string output)
    {
        var rows=new List<object>();
        foreach(string name in new[]{"H25-same","H26-L-learned-C","H26-E-learned-C","H26-LE-learned-C","H26-LE-learned-C-alt"}){
            var source=Read199<Archive198>(Path.Combine(output,name+".json.gz"));
            var strict=new Corridor199(Map199);
            var visual=new Technique200(strict.Windows).Begin();
            object? techniqueFailure=null,corridorFailure=null;
            foreach(var sample in source.Run.Riders.Single().Samples){
                var x=sample.Input;
                if(corridorFailure is null&&strict.Check(x.Rider,sample.Outer) is {} reason)
                    corridorFailure=new{Reason=reason,x.Time,x.Rider.TotalReferenceMeters,x.Rider.AngularVelocity};
                if(techniqueFailure is null&&visual.Observe(x.Rider,x.Time) is {} why)
                    techniqueFailure=new{Reason=why,x.Time,x.Rider.TotalReferenceMeters};
            }
            rows.Add(new{Name=name,StrictCorridorFailure=corridorFailure,TechniqueFailure=techniqueFailure});
        }
        Save(Path.Combine(output,"style-audit.json"),rows);
    }

    private static void AuditAcceptedForces202(string output)
    {
        var rows=new List<object>();
        foreach(string name in new[]{"H25-same","H26-L-same","H26-E-same","H26-LE-same",
            "H26-L-learned-C","H26-E-learned-C","H26-LE-learned-C","H26-LE-learned-C-alt"}){
            var archive=Read199<Archive198>(Path.Combine(output,name+".json.gz"));
            var r=archive.Run.Riders.Single();
            double force=0,shaftError=0,steer=0,lean=0;
            int exact=0;
            for(int i=1;i<r.Samples.Length;i++){
                var previous=r.Samples[i-1];var now=r.Samples[i];var x=previous.Input;
                var power=now.Powertrain!;var expected=now.Forces!.Value;
                var shaft=new CrankClutch144(omega:previous.CrankOmega,lossH26:archive.Handling.H26?.EngineLoss);
                var wheel=Speed148.ForecastFork148.Wheel(x.Omega,Physics.RearWheel);
                // Read the saved accepted lean; no state or command is advanced into the future.
                var road=x.Rider.Road;
                var usable=LaneModel.UsableRacingWidthMeters(Frame.Track.Segments[road.Segment].Type,Frame.Track.Geometry);
                var grip=Surface.SampleSurface(road.Segment,Math.Clamp((float)(road.OffsetMeters/usable)*4,0,4));
                var step=HandlingContact.Prepare(archive.Handling,grip,x.Rider with {Drive=now.Input.Active.Drive},r.Spec,
                    Physics,x.Time,Mass,Inertia,wheel,x.Steering,archive.Lean[0][i-1].ActualRadians,
                    (float)power.Seconds,now.Input.Active.SteeringRadians,shaft,RiderController.ReactionSeconds(r.Spec));
                var actual=step.Forces;
                double delta=Math.Max(System.Numerics.Vector2.Distance(actual.FrontForce,expected.FrontForce),
                    System.Numerics.Vector2.Distance(actual.RearForce,expected.RearForce));
                force=Math.Max(force,delta);shaftError=Math.Max(shaftError,Math.Abs(shaft.Prepared.AfterOmega-power.AfterOmega));
                steer=Math.Max(steer,Math.Abs(actual.SteeringAfter-expected.SteeringAfter));
                lean=Math.Max(lean,Math.Abs(step.LeanAfter-archive.Lean[0][i].ActualRadians));
                if(delta==0&&shaft.Prepared.AfterOmega==power.AfterOmega&&actual.SteeringAfter==expected.SteeringAfter)exact++;
            }
            if(force>1e-3||shaftError>1e-5||steer>1e-6||lean>1e-9)
                throw new InvalidOperationException("Final physics differs from archived native forces: "+name);
            rows.Add(new{Name=name,Steps=r.Samples.Length-1,ExactSteps=exact,MaxForceNewtons=force,
                MaxShaftOmega=shaftError,MaxSteeringRadians=steer,MaxLeanRadians=lean,NativeWorlds=0});
        }
        Save(Path.Combine(output,"final-physics-parity.json"),rows);
        Console.WriteLine("PASS final physics reevaluated on all accepted native states; no new worlds.");
    }

    private static void CheckMatched202(string output,string input)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(input));
        var start=doc.RootElement.GetProperty("Root").Deserialize<Start197>(Json)!;
        double goal=doc.RootElement.GetProperty("Goal").GetDouble();
        var policy=Policies202().Single(x=>x.Variant=="H26-LE");
        var model=new Predictor198(Frame,Surface,Physics,policy.Settings,Timing,policy.Spec,Mass,Inertia);
        var cursor=model.Begin(start,[],0,goal,.5);
        cursor.Advance(20,start.Review);
        Save(Path.Combine(output,"matched-irrevocable-prefix.json"),new{Start=start,Goal=goal,cursor.Outcome,
            End=cursor.Snapshot,Explanation="The active/pending prefix fails before the first new review. No new controller command can change it.",NativeWorlds=0});
    }
}
