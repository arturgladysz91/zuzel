using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static class Entry202
{
    internal static int Main(string[] args)
    {
        try {
            if(args is ["--stadium-race",var config,var output])Study191.StadiumEntry202(config,output);
            else Study191.Run202(args);
            return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}

internal static partial class Study191
{
    private static HandlingSettings Settings202(string variant)=>Handling with {
        H26=variant=="H25"?null:H26Configuration.For(variant is "H27-P" or "H27-N" or "H28-C" or "H28-F"?"H26-LE":variant),
        PostureH27 = variant is "H27-P" or "H27-N" or "H28-C" or "H28-F" ? new() : null,
        DoubleContactSolve = variant is "H27-N" or "H28-C" or "H28-F",
        CoupledRearTyre = variant is "H28-C" or "H28-F",
        FastCoupledCertificate=variant=="H28-F" };
    internal static void StadiumEntry202(string config,string output)=>Stadium202(config,output);
    internal static void Run202(string[] args)
    {
        string mode=args[0],workspace=args[1],output=args[2];
        if(args.Skip(3).Contains("H28-F")&&!mode.EndsWith("-206",StringComparison.Ordinal))
            throw new ArgumentException("H28-F must use the strict study entry points.");
        researchRepository202=Directory.Exists(Path.Combine(workspace,"tools/speedway-h26"))?workspace:Path.Combine(workspace,"work/zuzel");
        Directory.CreateDirectory(output);
        switch(mode){
            case "search-211":Search211(workspace,output,args[3],args.Length>4?args[4]:null);break;
            case "root-211":AuditRoot211(workspace,output);break;
            case "select-211":Select211(workspace,output,args[3]);break;
            case "admit-211":Admit211(workspace,output,args[3]);break;
            case "native-211":Native211(workspace,output,args[3],args[4]);break;
            case "freeze-211":Freeze211(workspace,output);break;
            case "entries-211":HighEntries211(workspace,output,args[3]);break;
            case "search-210": Search210(workspace,output,args[3],args.Length>4?args[4]:null);break;
            case "freeze-reference-210": FreezeReference210(workspace,output);break;
            case "admit-210": Admit210(workspace,output,args[3]);break;
            case "native-210": Native210(workspace,output,args[3],args[4]);break;
            case "probes-210": Probes210(workspace,output,args[3]);break;
            case "recovery-210": RecoveryTrial210(workspace,output,args[3]);break;
            case "reserve-210": ReserveTrial210(workspace,output,args[3]);break;
            case "repair-210": Repair210(workspace,output,args[3],args[4]);break;
            case "fresh-210": Fresh210(workspace,output,args[3]);break;
            case "arm-traces-210": ArmTraces210(workspace,output,args[3]);break;
            case "trace-210": TracePolicy210(workspace,output,args[3],args[4]);break;
            case "audit-209": Audit209(workspace,output);break;
            case "diagnostics-209": Diagnostics209(workspace,output);break;
            case "events-209": Events209(workspace,output);break;
            case "arms-209": Arms209(workspace,output);break;
            case "search-209": Search209(workspace,output,args[3],args.Length>4?args[4]:null);break;
            case "check-209": Check209(workspace,output,args[3]);break;
            case "screen-209": Screen209(workspace,output,args[3]);break;
            case "native-209": Native209(workspace,output,args[3],args[4]);break;
            case "check-arms-209": CheckArms209(workspace,output);break;
            case "reconstruct-209": Reconstruct209(workspace,output);break;
            case "pair-208": Compare208(workspace,output,args.Skip(3).ToArray());break;
            case "freeze-208": Freeze208(workspace,output,args.Skip(3).ToArray());break;
            case "native-208": Native208(workspace,output,args[3],args[4]);break;
            case "audit-208": Audit208(workspace,output);break;
            case "lock-208": Lock208(workspace,output,args[3]);break;
            case "time-208": Time208(workspace,output,args[3]);break;
            case "plan-208": Plan208(workspace,output,args[3],args.Length>4?args[4]:null);break;
            case "plan-207":
                if(args[3] is not ("pilot" or "greedy" or "beam" or "recovery" or "pace"))
                    throw new ArgumentException("Specify pilot, greedy, beam, recovery, or pace.");
                PlanSearch207(workspace,output,args[3]=="pilot",args[3]=="greedy",args[3] is "recovery" or "pace"?args[4]:null,args[3]=="pace");
                break;
            case "recheck-207": RecheckLocked207(workspace,output,args[3],args[4]);break;
            case "replay-207": Replay207(workspace,output,args[3]);break;
            case "decisions-207": Benchmark207(workspace,output,args[3]);break;
            case "native-207": Native207(workspace,output,args[3],args[4]);break;
            case "audit-207": Audit207(workspace,output);break;
            case "parity-206": Parity206(workspace,output);break;
            case "decisions-206": Decisions206(workspace,output,args[3],args[4],args.Length>5&&args[5]=="detail");break;
            case "style-206": Style206(workspace,output);break;
            case "recheck-206": Recheck206(workspace,output,args[3]);break;
            case "search-206": Search206(workspace,output,args.Length>3?int.Parse(args[3]):2,args.Length>4?args[4]:null);break;
            case "native-206": NativeStrict206(workspace,output,args[3],args[4]);break;
            case "preflight-205": Preflight205(workspace,output,args.Length>3?args[3]:null);break;
            case "model-205": Audit205(workspace,output,args.Length>3&&args[3]=="focused");break;
            case "certify-205": Certify205(workspace,output);break;
            case "transients-205": Transients205(workspace,output);break;
            case "equilibria-205": Equilibria205(workspace,output);break;
            case "throttle-205": AuditThrottle204(workspace,output,args.Length>3?args[3]:null,combinedStudy:true);break;
            case "native-205": Native205(workspace,output,args[3],args.Length>4?args[4]:null);break;
            case "model-204": Audit204(workspace,output);break;
            case "native-204": Native204(workspace,output,args[3]);break;
            case "throttle-204": AuditThrottle204(workspace,output,args.Length>3?args[3]:null);break;
            case "audit-203": Audit203(workspace,output);break;
            case "posture-check-203": CheckPosture203(workspace,output);break;
            case "native-check-203": CheckNative203(args[3],output);break;
            case "holdout-solver-203": CheckHoldoutSolver203(args[3],args[4],output);break;
            case "native-203-same": SameNative203(workspace,output,args[3],args[4]);break;
            case "native-203-learned":
                RequireLedger203(args[3]+"-learned-"+args[5]);
                NativeLearned202(workspace,output,args[3],args[4],args[5]);break;
            case "properties": Properties202(workspace,output);break;
            case "scenarios": Scenarios202(workspace,output);break;
            case "same-native": SameNative202(workspace,output,args[3]);break;
            case "train": Train202(workspace,output,args[3],args.Length>4?int.Parse(args[4]):90);break;
            case "train-physical": Train202(workspace,output,args[3],args.Length>4?int.Parse(args[4]):90,false);break;
            case "matched-train": MatchedTrain202(workspace,output);break;
            case "native-learned": NativeLearned202(workspace,output,args[3],args[4],args.Length>5?args[5]:"C");break;
            case "refine": Refine202(workspace,output,args[3],args[4],int.Parse(args[5]));break;
            case "cycle": Cycle202(workspace,output,args[3],args[4],args.Length>5?int.Parse(args[5]):120);break;
            case "changed": Changed202(workspace,output,args[3],args[4],args[5]);break;
            case "publish-policies": PublishPolicies202(workspace,output);break;
            case "check-policies": CheckPolicies202(output);break;
            case "replay-controllers": ReplayControllers202(output);break;
            case "style-audit": StyleAudit202(output);break;
            case "force-parity": AuditAcceptedForces202(output);break;
            case "check-matched": CheckMatched202(output,args[3]);break;
            default:throw new ArgumentException("properties|same-native|train WORKSPACE OUTPUT [H25|H26-L|H26-E|H26-LE]");
        }
    }

    private static void Properties202(string workspace,string output)
    {
        int assertions=0,negative=0,positive=0;
        double residual=0,loadResidual=0,frontMin=1e9,rearMin=1e9;
        var failures=new List<object>();
        void Check(bool ok,string name){assertions++;if(!ok)throw new InvalidOperationException("Property failed: "+name);}
        var bike=Physics.TwoContactBike??TwoContactBikeParameters.Default;
        var original=Read199<Archive198>(Path.Combine(workspace,"outputs/badanie200/C-native.json.gz"));
        var driver=original.Run.Riders.Single();
        var random=new Random(202202);
        for(int i=0;i<4000;i++){
            double h=.45+random.NextDouble()*.2;
            double ax=(random.NextDouble()*2-1)*8;
            var z=DynamicAxleLoad.AtAcceleration(ax,bike,h);
            Check(Math.Abs(z.FrontFraction+z.RearFraction-1)<1e-15,"mg");
            Check(z.WithinAssumptions,"positive axle loads");
            Check(Math.Sign(z.RearFraction-bike.FrontAxleFromComMeters/((double)bike.FrontAxleFromComMeters+bike.RearAxleFromComMeters))==Math.Sign(ax),"load sign");
        }
        var matrix=new List<object>();
        foreach(double scale in new[]{.5,1,1.5})
        foreach(double rpm in new[]{2000d,4000,6000,8500,10000,11000,12000})
        foreach(double gas in new[]{0d,.1,.25,.5,1}){
            var loss=new EngineLossH26(Scale:scale);
            double omega=rpm*Math.PI/30;
            double net=gas*Engine500.Torque(rpm)-loss.NetViscosity(gas)*omega;
            matrix.Add(new{scale,rpm,gas,Gross=gas*(Engine500.Torque(rpm)+loss.Mechanical(omega)),
                Mechanical=loss.Mechanical(omega),Pump=loss.Pumping(omega,gas),Net=net});
            Check(loss.Mechanical(omega)>=0&&loss.Pumping(omega,gas)>=0,"loss passivity");
            if(gas==1)Check(net==Engine500.Torque(rpm),"unchanged full throttle brake map");
        }
        var probes=new List<object>();
        foreach(double h in new[]{.45,.50,.55,.60,.65})
        foreach(double scale in new[]{.5,1,1.5})
        foreach(var sample in driver.Samples.Where((_,i)=>i%53==0))
        foreach(double gas in new[]{0d,.1,.25,.5,1})
        foreach(float dt in new[]{.005f,.01f,.02f}){
            var state=sample.Input;
            var body=state.Rider with {Drive=(float)gas};
            var settings=Handling with {H26=new(true,new(Scale:scale),h)};
            var shaft=new CrankClutch144(omega:sample.CrankOmega,lossH26:settings.H26.EngineLoss);
            var wheel=Speed148.ForecastFork148.Wheel(state.Omega,Physics.RearWheel);
            try{
                var step=HandlingContact.Prepare(settings,Surface.SampleSurface(0,2),body,driver.Spec,Physics,
                    state.Time,Mass,Inertia,wheel,state.Steering,original.Lean[0][Array.IndexOf(driver.Samples,sample)].ActualRadians,
                    dt,state.Active.SteeringRadians,shaft,0);
                var z=step.Forces.TyreLoads!;var p=shaft.Prepared;
                Check(z.FrontNormalNewtons>0&&z.RearNormalNewtons>0,"positive coupled loads");
                Check(Math.Abs(z.FrontNormalNewtons+z.RearNormalNewtons-Mass*9.81)<1e-8,"coupled mg");
                Check(Math.Abs(step.LoadH26!.Residual)<=settings.H26.AccelerationToleranceMetersPerSecondSquared,"coupled convergence");
                Check(p.Wheel.RearUtilization<=1+1e-6,"rear combined circle");
                Check(step.Forces.FrontForce.Length()<=z.FrontCapacityNewtons+1e-3,"front force cap");
                Check(p.ClutchHeatJoules>=-1e-7&&p.TransmissionLossJoules>=-1e-7,"passive clutch and gear");
                Check(Math.Abs(p.EnergyResidualJoules)<1e-6,"shaft energy");
                double wheelDelta=.5*wheel.Parameters.WheelInertiaKgMetersSquared*(p.Wheel.AfterOmega*p.Wheel.AfterOmega-state.Omega*state.Omega);
                double wheelResidual=p.Wheel.EngineWorkJoules-p.Wheel.BearingLossJoules-
                    p.Wheel.LongitudinalNewtons*wheel.Parameters.WheelRadiusMeters*p.Wheel.MidOmega*dt-wheelDelta;
                Check(Math.Abs(wheelResidual)<1e-6,"wheel energy");
                if(p.ClutchTorque<0)negative++;else positive++;
                residual=Math.Max(residual,Math.Max(Math.Abs(p.EnergyResidualJoules),Math.Abs(wheelResidual)));
                loadResidual=Math.Max(loadResidual,Math.Abs(step.LoadH26.Residual));
                frontMin=Math.Min(frontMin,z.FrontNormalNewtons);rearMin=Math.Min(rearMin,z.RearNormalNewtons);
                if(h==.55&&scale==1&&dt==.01f)probes.Add(new{state.Time,gas,z,p,Loads=step.LoadH26});
            }catch(InvalidOperationException e)when(e.Message.StartsWith("H26 wheel unloading")){
                failures.Add(new{state.Time,h,scale,gas,dt,Diagnostic=e.Message});
            }
        }
        Check(negative>0&&positive>0,"bidirectional clutch");
        // No native world has been created. Zero H26 losses must leave the old H25 contact solution identical.
        foreach(var sample in driver.Samples.Where((_,i)=>i%11==0)){
            var x=sample.Input;var w=Speed148.ForecastFork148.Wheel(x.Omega,Physics.RearWheel);
            var a=new CrankClutch144(omega:sample.CrankOmega);
            var b=new CrankClutch144(omega:sample.CrankOmega,lossH26:new(Scale:0));
            var aa=HandlingContact.Prepare(Handling,Surface.SampleSurface(0,2),x.Rider,driver.Spec,Physics,x.Time,Mass,Inertia,w,x.Steering,0,.01f,x.Active.SteeringRadians,a,0);
            var bb=HandlingContact.Prepare(Handling with {H26=new(false,new(Scale:0))},Surface.SampleSurface(0,2),x.Rider,driver.Spec,Physics,x.Time,Mass,Inertia,w,x.Steering,0,.01f,x.Active.SteeringRadians,b,0);
            Check(aa.Forces==bb.Forces&&a.Prepared.AfterOmega==b.Prepared.AfterOmega,"H25 disabled parity");
        }
        Save(Path.Combine(output,"engine-loss-matrix.json"),matrix);
        Save(Path.Combine(output,"properties-probes.json.gz"),probes,true);
        var result=new{Passed=true,Assertions=assertions,NegativeClutch=negative,PositiveClutch=positive,
            MaxEnergyResidual=residual,MaxLoadResidual=loadResidual,MinimumFront=frontMin,MinimumRear=rearMin,
            UnloadingDiagnostics=failures,NativeWorlds=0};
        Save(Path.Combine(output,"properties.json"),result);
        Console.WriteLine($"PASS {assertions} property assertions; {failures.Count} explicit unloading diagnostics; zero native worlds.");
    }

    private static void Reserve202(string output,string arm)
    {
        if(!File.Exists(Path.Combine(output,"properties.json")))throw new InvalidOperationException("Properties must pass first.");
        using var budget=new FileStream(Path.Combine(output,"world-budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        if(Directory.GetFiles(output,"*.reserved").Length>=8)throw new InvalidOperationException("Eight native worlds exhausted.");
        using var marker=new FileStream(Path.Combine(output,arm+".reserved"),FileMode.CreateNew);
        JsonSerializer.Serialize(marker,new{Arm=arm,Executable=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(Entry202).Assembly.Location)))},Json);
    }

    private static void SameNative202(string workspace,string output,string variant, string? archive = null, string? label = null)
    {
        var settings=Settings202(variant);
        if (variant is "H27-P" or "H28-C") RequireLedger203(label ?? variant+"-same");
        var source=Read199<Archive198>(archive ?? Path.Combine(workspace,"outputs/badanie200/C-native.json.gz"));
        var driver=source.Run.Riders.Single();
        var rig=new HandlingRig(settings);
        int command=0;
        string name = label ?? variant+"-same";
        if (label is null && variant is not ("H27-P" or "H28-C")) Reserve202(output,name);
        using var world=new TrafficWorld191(source.Run.Options,Frame,[driver.Spec],[rig],Timing,[driver.Gate]);
        var run=world.Run(label is null ? "202-"+name : name,"K1",(input,shaft,spec,snapshot)=>{
            if (command == driver.Decisions.Length)
                throw new InvalidOperationException("Archived command stream exhausted.");
            var original=driver.Decisions[command++];
            if(Math.Abs(original.Choice.Time-input.Time)>1e-8)throw new InvalidOperationException("Command clock mismatch.");
            return new(original.Choice,0);
        });
        var r=run.Riders.Single();
        bool equal=variant=="H25"&&r.Samples.Length==driver.Samples.Length&&r.Samples.Zip(driver.Samples).All(x=>Equal199(x.First,x.Second));
        var result=new{Variant=variant,Comparison="identical archived commands; no retraining",HistoricalParity=equal,
            run.Outcome,r.FinishSeconds,run.EndSeconds,run.TotalCpuSeconds,r.CompletedLaps,run.NativeSteps,
            Contacts=run.Contacts.Length,MinSweep=r.Samples.Where(s=>s.Certificate.HasValue).Min(s=>s.Certificate),
            EndReference=r.Samples[^1].Input.Rider.TotalReferenceMeters,run.Fingerprint};
        Save(Path.Combine(output,name+".json.gz"),new{Run=run,Result=result,Timing,Handling=settings,
            Lean=new[]{rig.LeanSamples},Loads=new[]{rig.LoadSamples},Posture=rig.PostureSamples},true);
        Save(Path.Combine(output,name+".result.json"),result);
        Console.WriteLine(JsonSerializer.Serialize(result,Json));
    }

    private static void Train202(string workspace,string output,string variant,int rounds,bool technique=true)
    {
        var settings=Settings202(variant);
        var (seed,_,spec)=Fixture200(workspace,"C");
        // New physics learns the launch too. The #200 root includes an irrevocable H25 launch command.
        seed=seed with {Start=seed.Start with {Pending=[],Review=0}};
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var planner=new CyclePlanner199(new(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia),Frame.LapMeters,seed.Goal,corridor,technique?new(corridor.Windows):null);
        var previous=Read199<Training199>(Path.Combine(workspace,"outputs/badanie200/training-C.json.gz")).Winner.Result;
        planner.ExplorationReference202(previous);
        var transfer=planner.Transfer(seed.Start,previous,true);
        Save(Path.Combine(output,variant+"-transfer.json.gz"),transfer,true);
        Console.WriteLine($"{variant} old policy: {transfer.Stop} s={transfer.Progress:F3} t={transfer.Finish:F4}");
        var sources=new List<(string,FreeRacingAction[])>{("H25 archive diagnostics",previous.Controls!)};
        if(transfer.Legal)sources.Add(("feedback transfer seed",transfer.Controls!));
        var probes=new List<object>();
        var partials=new List<Trial199>();
        foreach(double steer in new[]{.85,.925,1d,1.05,1.1,1.15})
        foreach(double gas in new[]{.4,.6,.8,1d})
        foreach(double launchGas in new[]{.25,.45,.6})
        foreach(double launchSpeed in new[]{4d,10d}){
            var trial=planner.SeedH26(seed.Start,previous,steer,gas,launchGas,launchSpeed);
            probes.Add(new{steer,gas,launchGas,launchSpeed,trial.Legal,trial.Stop,trial.Progress,trial.Finish,trial.Sweep});
            if(trial.Legal)sources.Add(($"H26 feedback seed {steer}/{gas}/{launchGas}/{launchSpeed}",trial.Controls!));
            else if(trial.Progress>50)partials.Add(trial);
        }
        foreach(var partial in partials.OrderByDescending(x=>x.Progress).Take(5))
            sources.Add(("partial H26 feedback to repair",Pad200(partial.Controls!)));
        foreach(string priorDir in new[]{"learn-v3","learn-v5"}){
            var path=Path.Combine(workspace,"outputs/badanie202",priorDir,variant+"-training.json.gz");
            if(File.Exists(path)){
                var learned=Read199<Training199>(path).Winner.Result;
                if(learned.Controls is {Length:>8})sources.Add(("prior H26 development",Pad200(learned.Controls)));
            }
        }
        Save(Path.Combine(output,variant+"-seeds.json"),probes);
        var trained=planner.Learn(variant,"B",seed.Start,sources,rounds,24,202202,explore:true);
        Save(Path.Combine(output,variant+"-training.json.gz"),trained,true);
        Console.WriteLine($"{variant} learned: {trained.Winner.Result.Stop} {trained.Winner.Result.Finish:F6}, s={trained.Winner.Result.Progress:F3}");
    }

    private static void MatchedTrain202(string workspace,string output)
    {
        var (source,root,index,goal)=MatchedRoot201(workspace);
        var spec=source.Run.Riders.Single().Spec;
        var settings=Settings202("H26-LE");
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var planner=new CyclePlanner199(new(Frame,Surface,Physics,settings,Timing,spec,Mass,Inertia),Frame.LapMeters,goal,corridor);
        var previous=Read199<Training199>(Path.Combine(workspace,"outputs/badanie200/training-C.json.gz")).Winner.Result;
        var sources=new List<(string,FreeRacingAction[])>{("196 original",Tape199(source.Run.Riders.Single(),root.Review))};
        var probes=new List<object>();
        var partials=new List<Trial199>();
        foreach(double steer in new[]{.8,.9,1d,1.1,1.2})
        foreach(double gas in new[]{.3,.5,.7,.9,1.1}){
            var trial=planner.Rebalance201(root,previous,steer,gas);
            probes.Add(new{steer,gas,trial.Legal,trial.Stop,trial.Progress,trial.Finish,trial.Sweep});
            if(trial.Controls is {Length:>8})partials.Add(trial);
        }
        foreach(var trial in partials.OrderByDescending(x=>x.Legal).ThenByDescending(x=>x.Progress).Take(8))
            sources.Add(("matched H26 feedback proposal",Pad200(trial.Controls!)));
        var learned=planner.Learn("H26-LE-matched","B",root,sources,160,24,202196,explore:true);
        Save(Path.Combine(output,"matched-training.json.gz"),learned,true);
        Save(Path.Combine(output,"matched-probes.json"),new{Root=root,Index=index,Goal=goal,Probes=probes});
        Console.WriteLine($"H26-LE matched {learned.Winner.Result.Stop}, finish={learned.Winner.Result.Finish:F6}, root={root.Review:F6}, progress={learned.Winner.Result.Progress:F3}");
    }

    private static void Refine202(string workspace,string output,string variant,string prior,int rounds)
    {
        var (seed,_,spec)=Fixture200(workspace,"C");
        seed=seed with {Start=seed.Start with {Pending=[],Review=0}};
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var planner=new CyclePlanner199(new(Frame,Surface,Physics,Settings202(variant),Timing,spec,Mass,Inertia),Frame.LapMeters,seed.Goal,corridor);
        var reference=Read199<Training199>(Path.Combine(workspace,"outputs/badanie200/training-C.json.gz")).Winner.Result;
        planner.ExplorationReference202(reference);
        var partial=Read199<Training199>(prior).Winner.Result;
        var learned=planner.Learn(variant+"-refine","B",seed.Start,new(){("H26 development prefix",Pad200(partial.Controls!))},rounds,24,202712,explore:true);
        Save(Path.Combine(output,variant+"-training.json.gz"),learned,true);
        Console.WriteLine($"{variant} refined {learned.Winner.Result.Stop}, t={learned.Winner.Result.Finish:F6}, S={learned.Winner.Result.Progress:F3}");
    }

    private static void Cycle202(string workspace,string output,string variant,string prior,int rounds=120)
    {
        var (seed,_,spec)=Fixture200(workspace,"C");
        seed=seed with {Start=seed.Start with {Pending=[],Review=0}};
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var planner=new CyclePlanner199(new(Frame,Surface,Physics,Settings202(variant),Timing,spec,Mass,Inertia),Frame.LapMeters,seed.Goal,corridor);
        var reference=Read199<Training199>(prior).Winner.Result;
        planner.ExplorationReference202(Read199<Training199>(Path.Combine(workspace,"outputs/badanie200/training-C.json.gz")).Winner.Result);
        var sources=new List<(string,FreeRacingAction[])>();
        var probes=new List<object>();
        foreach(double steer in new[]{.85,.925,1d,1.075,1.15})
        foreach(double gas in new[]{.8,.9,1d,1.1})
        foreach(double offset in new[]{0d,40d,75d}){
            var trial=planner.RepeatH26(seed.Start,reference,steer,gas,offset);
            probes.Add(new{steer,gas,offset,trial.Legal,trial.Stop,trial.Progress,trial.Finish,trial.Sweep});
            sources.Add(($"H26 second lap feedback {steer}/{gas}/{offset}",Pad200(trial.Controls!)));
        }
        Save(Path.Combine(output,"cycle-probes.json"),probes);
        var learned=planner.Learn(variant+"-cycle","B",seed.Start,sources,rounds,24,202747,explore:true);
        Save(Path.Combine(output,variant+"-training.json.gz"),learned,true);
        Console.WriteLine($"{variant} cyclic feedback {learned.Winner.Result.Stop}, t={learned.Winner.Result.Finish:F6}, S={learned.Winner.Result.Progress:F3}");
    }

    private static void Changed202(string workspace,string output,string variant,string prior,string fixture)
    {
        var (seed,_,spec)=Fixture200(workspace,fixture);
        seed=seed with {Start=seed.Start with {Pending=[],Review=0}};
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var planner=new CyclePlanner199(new(Frame,Surface,Physics,Settings202(variant),Timing,spec,Mass,Inertia),Frame.LapMeters,seed.Goal,corridor);
        var reference=Read199<Training199>(prior).Winner.Result;
        planner.ExplorationReference202(reference);
        var transfer=planner.Transfer(seed.Start,reference,true);
        var sources=new List<(string,FreeRacingAction[])>{("H26 autonomous transfer",Pad200(transfer.Controls!))};
        Save(Path.Combine(output,"changed-transfer.json.gz"),transfer,true);
        var learned=planner.Learn(variant+"-"+fixture,"B",seed.Start,sources,60,24,202840,explore:true);
        Save(Path.Combine(output,variant+"-training.json.gz"),learned,true);
        Console.WriteLine($"{variant} {fixture}: {learned.Winner.Result.Stop} {learned.Winner.Result.Finish:F6}, S={learned.Winner.Result.Progress:F3}");
    }
}
