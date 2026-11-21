using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static readonly string[] Physics205=["H27-N","H28-C"];
    private sealed record Shot205(string Name,Start197 Start,RiderSpec Spec,float Dt,string Origin);
    private sealed record Result205(string Variant,string Status,string? Diagnostic,object? State);

    private static (HandlingContact.ContactQuery64 Query,CrankClutch144 Shaft,double Lo,double Hi,PostureStepH27? Posture)
        Query205(Shot205 shot,string variant,double? frontNormal=null)
    {
        var settings=Settings202(variant);
        var state=shot.Start.State;
        var probe=new Probe203(settings,shot.Spec,state,shot.Start.Active,shot.Dt);
        var bike=Physics.TwoContactBike??TwoContactBikeParameters.Default;
        var budget=LoadBasedTyreGrip.Budget(Mass,probe.Grip,bike,Physics.LoadBasedTyres);
        var posture=settings.PostureH27?.Prepare(Mass,shot.Spec.Profile.Gameplay.Physical.MassKg,settings.H26!.ComHeightMeters,
            shot.Start.RiderHeightOffsetMeters,shot.Start.Active.Drive,shot.Dt);
        double height=posture?.CombinedMidpointHeightMeters??settings.H26!.ComHeightMeters;
        double gravity=LoadBasedTyreGrip.GravityMetersPerSecondSquared;
        double lo=-gravity*bike.FrontAxleFromComMeters/height+.001,hi=gravity*bike.RearAxleFromComMeters/height-.001;
        if(frontNormal.HasValue)
        {
            double bound=gravity*settings.GripScale*budget.EffectiveFrictionCoefficient+
                (LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons+
                 LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient*Math.Pow(state.Rider.Speed,2))/Mass+1;
            (lo,hi)=(-bound,bound);
        }
        var shaft=new CrankClutch144(omega:state.Shaft,lossH26:settings.H26!.EngineLoss);
        var query=new HandlingContact.ContactQuery64(settings,state.Rider with {Drive=shot.Start.Active.Drive},shot.Spec,bike,
            budget,Mass,Inertia,height,state.Time,shot.Dt,state.Steer,shot.Start.Active.SteeringRadians,state.Lean,
            Speed148.ForecastFork148.Wheel(state.Wheel,Physics.RearWheel),shaft,RiderController.ReactionSeconds(shot.Spec),
            frontNormal.HasValue?Mass*gravity-frontNormal:null);
        return (query,shaft,lo,hi,posture);
    }

    private static Result205 Measure205(Shot205 shot,string variant,double? fixedFront=null)
    {
        try
        {
            var q=Query205(shot,variant,fixedFront);
            var step=q.Query.Solve(q.Lo,q.Hi,q.Posture);
            var p=q.Shaft.Prepared;
            var f=step.Forces;
            var z=f.TyreLoads!;
            var w=p.Wheel;
            var inp=shot.Start.State.Rider;
            var tangent=inp.Speed>.01f?Vector2.Normalize(inp.Velocity):f.BodyForward;
            var normal=new Vector2(-tangent.Y,tangent.X);
            double aT=Vector2.Dot(f.NetForce,tangent)/Mass,aN=Vector2.Dot(f.NetForce,normal)/Mass;
            var reach=q.Query.FrontMomentSweep(z.FrontCapacityNewtons);
            double needed=q.Query.DesiredYawMoment-f.RearTorque;
            double miss=Math.Max(0,Math.Max(reach.Minimum-needed,needed-reach.Maximum));
            string status="Converged";
            if(!FreeLearningExperiment.SteeringStepIsPhysical(f,shot.Dt)||
                Math.Abs(step.LoadH26!.Precision!.AppliedResidual)>2e-5||
                w.RearUtilization>1.000001||w.LongitudinalLossJoules< -1e-6||
                step.LoadH26.Precision.Combined is {} proof&&
                (proof.LateralDissipationJoules< -1e-6||Math.Abs(proof.AppliedTyreFxResidual)>.001||
                Math.Abs(proof.AppliedTyreFyResidual)>.001))
                status="MechanicalMismatch";
            return new(variant,status,null,new {Time=shot.Start.State.Time,Speed=inp.Speed,Heading=inp.Heading,
                Direction=Math.Atan2(tangent.Y,tangent.X), Beta=inp.SlideRadians,Yaw=inp.AngularVelocity,
                Kappa=w.InstantSlipRatio,w.MidSlipVelocityMetersPerSecond,WheelAfter=w.AfterOmega,p.AfterOmega,p.Engine.EngineRpm,
                Fx=w.AppliedLongitudinalNewtons,Fy=f.RearLateral,FrontFx=Vector2.Dot(f.FrontForce,f.BodyForward),
                FrontFy=f.FrontLateral,RearMoment=f.RearTorque,NetMoment=f.NetTorque,YawDot=f.NetTorque/Inertia,
                BetaDot=inp.Speed>.01f?inp.AngularVelocity-aN/inp.Speed:(double?)null,
                q.Query.DesiredYawMoment,FrontMomentMin=reach.Minimum,FrontMomentMax=reach.Maximum,YawDeficit=miss,
                aT,aN,f.SteeringBefore,f.SteeringRequest,f.SteeringAfter,
                RateLimited=f.SteeringAfter!=f.SteeringRequest,w.RearUtilization,w.LongitudinalLossJoules,
                FrontDissipation=-Vector2.Dot(f.FrontForce,f.FrontVelocity)*shot.Dt,
                p.EnergyResidualJoules,p.ClutchHeatJoules,p.TransmissionLossJoules,step.LoadH26});
        }
        catch(ContactRootException e){return new(variant,e.Status.ToString(),e.Message,null);}
        catch(InvalidOperationException e){return new(variant,"PowertrainOrInvalid",e.Message,null);}
        catch(ArgumentException e){return new(variant,"InvalidPhysicalState",e.Message,null);}
    }

    private static Shot205 NativeShot205(Archive204 archive,int i,string name,string origin="H27-N native")
    {
        var run=archive.Run.Riders.Single();
        var state=State205(archive,i);
        float dt=i+1<run.Samples.Length?(float)run.Samples[i+1].Powertrain!.Seconds:.01f;
        return new(name,state,run.Spec,dt,origin);
    }

    private static List<Shot205> Shots205(string repository,Archive204 native)
    {
        var shots=new List<Shot205>();
        var driver=native.Run.Riders.Single();
        int At(double time)=>Enumerable.Range(0,driver.Samples.Length)
            .MinBy(i=>Math.Abs(driver.Samples[i].Input.Time-time));
        string data204=Path.Combine(repository,"data/experiments/speedway204");
        using var targets=JsonDocument.Parse(File.ReadAllText(Path.Combine(data204,"targeted-scenarios.json")));
        using var matched=JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "data/experiments/speedway202/matched-irrevocable-prefix.json")));
        var pending196=matched.RootElement.GetProperty("Start").Deserialize<Start197>(Json)!;
        using var p0=JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "data/experiments/speedway203/holdout-solver-model.json")));
        var prefix=p0.RootElement.GetProperty("Checkpoint").Deserialize<Start197>(Json)!;
        var frozen=Read199<Training199>(Path.Combine(repository,Training204)).Winner.Result;
        var replay=new Predictor198(Frame,Surface,Physics,Settings202("H27-N"),Timing,driver.Spec,Mass,Inertia);
        var cursor=replay.Begin(prefix,Pad200(frozen.Controls!),(int)Math.Round(prefix.Review/.05),Frame.LapMeters*3,.5);
        cursor.Advance(25,20.51);
        var p0Prefix=cursor.Checkpoint();
        foreach (var c in targets.RootElement.GetProperty("Cases").EnumerateArray())
        {
            string name=c.GetProperty("Name").GetString()!;
            var state=c.GetProperty("State").Deserialize<HandlingForecast>(Json)!;
            var action=c.GetProperty("Action").Deserialize<FreeRacingAction>(Json);
            double offset=c.GetProperty("RiderOffset").GetDouble();
            var same=name=="196"?pending196:name=="plus-2-cm"?p0Prefix with {State=state,RiderHeightOffsetMeters=offset,Active=action}:
                new Start197(state,action,[],Math.Ceiling((state.Time+1e-8)*20)/20){RiderHeightOffsetMeters=offset};
            shots.Add(new(name,same,driver.Spec,c.GetProperty("Dt").GetSingle(),
                name=="plus-2-cm"?"H27-P exact body, wheel, crank and posture; H27-N pre-prefix reconstructs pending":
                name=="196"?"#196 exact pending; neutral rider posture counterfactual":
                "Historical frozen forces input; neutral rider posture counterfactual, only current command available in digest"));
        }
        var archive203=Read199<Archive204>(Path.Combine(repository,"data/experiments/speedway203/native/H27-P-learned-C.json.gz"));
        foreach(var (archive,label) in new[] {(archive203,"min-front-native-203"),(native,"min-front-native-204")})
        {
            int j=Enumerable.Range(0,archive.Loads[0].Length).MinBy(i=>archive.Loads[0][i].FrontNormalNewtons);
            shots.Add(NativeShot205(archive,j,label,"Archive with exact rider posture and pending"));
        }
        for(int lap=0;lap<3;lap++)
        for(int bend=0;bend<Map199.Count;bend++)
        {
            var section=Map199.At(lap,bend);
            foreach(var (name,s) in new[]{("entry",section.Entry),("apex",(section.Entry+section.Exit)/2),("exit",section.Exit)})
            {
                int i=Enumerable.Range(0,driver.Samples.Length).MinBy(j=>Math.Abs(driver.Samples[j].Input.Rider.TotalReferenceMeters-s));
                shots.Add(NativeShot205(native,i,$"bend-{lap*Map199.Count+bend+1}-{name}"));
            }
        }
        using var parity=JsonDocument.Parse(File.ReadAllText(Path.Combine(data204,"native-parity-and-style.json")));
        var style=parity.RootElement.GetProperty("Checks").EnumerateArray().Single(x=>x.GetProperty("Name").GetString()=="H27-N-learned-C");
        foreach(string property in new[]{"CorridorFailure","TechniqueFailure"})
        {
            var e=style.GetProperty(property);
            shots.Add(NativeShot205(native,At(e.GetProperty("Time").GetDouble()),e.GetProperty("Reason").GetString()!));
        }
        var (seed,_,changed)=Fixture200(repository,"C-alt");
        var offsetModel=new Predictor198(Frame,Surface,Physics,Settings202("H27-N"),Timing,changed,Mass,Inertia);
        var corridor=new Corridor199(Map199,double.PositiveInfinity);
        var wide=offsetModel.Begin(seed.Start with {Pending=[],Review=0},Pad200(frozen.Controls!),0,Frame.LapMeters*3,.502,
            constraint:corridor.Check);
        wide.Advance(1000);
        shots.Add(new("wide-entry",wide.Checkpoint(),changed,.01f,"H27-N frozen −12 cm MODEL at first wide-entry"));
        int straight=Enumerable.Range(0,driver.Samples.Length).Where(j=>driver.Samples[j].Input.Rider.Speed>18&&
            !driver.Samples[j].Input.Rider.Road.IsTurn).MinBy(j=>Math.Abs(driver.Samples[j].Input.Rider.SlideRadians));
        shots.Add(NativeShot205(native,straight,"straight-min-beta"));
        using var files=new System.IO.Compression.GZipStream(File.OpenRead(Path.Combine(data204,"throttle-model.json.gz")),
            System.IO.Compression.CompressionMode.Decompress);
        using var throttle=JsonDocument.Parse(files);
        var gains=throttle.RootElement.GetProperty("Trials").EnumerateArray().Where(x=>
            x.GetProperty("Physics").GetString()=="H27-N"&&x.GetProperty("Archive").GetString()=="H27-N-learned-C.json.gz"&&
            x.GetProperty("Horizon").GetDouble()==.80&&x.GetProperty("TimeGain").ValueKind==JsonValueKind.Number&&
            x.GetProperty("TimeGain").GetDouble()>0).OrderByDescending(x=>x.GetProperty("TimeGain").GetDouble()).ToArray();
        foreach (var gain in gains.Take(3))
            shots.Add(NativeShot205(native,At(gain.GetProperty("Time").GetDouble()),"unused-throttle-"+gain.GetProperty("Time").GetDouble().ToString("F2")));
        return shots;
    }

    private static void Audit205(string repository,string output,bool focused)
    {
        var timer=Stopwatch.StartNew();
        var native=Read199<Archive204>(Path.Combine(repository,NativeSource205));
        var shots=Shots205(repository,native);
        var matched=new List<object>();
        foreach(var shot in shots)
        {
            var results=Physics205.Select(variant=>Measure205(shot,variant)).ToArray();
            matched.Add(new {Shot=shot,Results=results});
            Console.WriteLine($"{shot.Name}: {string.Join("/",results.Select(x=>x.Status))}");
        }
        Save(Path.Combine(output,"matched-model.json"),matched);
        if(focused) return;
        var baseShot=shots.First(x=>x.Name=="bend-1-apex");
        var rows=new List<object>();
        foreach(double kappa in new[] {-.50,0,.50})
        foreach(float beta in new[] {-.18f,0,.18f})
        foreach(float yaw in new[] {-.7f,0,.7f})
        foreach(double speedKph in new[] {40d,60,80,100,120})
        foreach(double fzf in new[] {.0622,5,200,665})
        foreach(float dt in new[] {.0025f,.005f,.01f,.02f,.002872f})
        foreach(float gas in new[] {0f,.10f,.25f,.50f,.75f,1f})
        {
            float speed=(float)(speedKph/3.6);
            var p=baseShot.Start.State;
            float angle=p.Rider.Heading-beta;
            var v=new Vector2(MathF.Cos(angle),MathF.Sin(angle))*speed;
            double forward=v.X*Math.Cos(p.Rider.Heading)+v.Y*Math.Sin(p.Rider.Heading);
            double omega=(forward+kappa*Math.Sqrt(forward*forward+.25))/.33;
            var state=p with {Rider=p.Rider with {Velocity=v,AngularVelocity=yaw},Wheel=omega};
            var shot=baseShot with {Start=baseShot.Start with {State=state,Active=baseShot.Start.Active with {Drive=gas}},Dt=dt};
            rows.Add(new {Kappa=kappa,Beta=beta,Yaw=yaw,SpeedKph=speedKph,FrontNormal=fzf,Dt=dt,Gas=gas,
                Results=Physics205.Select(x=>Measure205(shot,x,fzf)).ToArray()});
        }
        Save(Path.Combine(output,"synthetic-grid-model.json.gz"),rows,true);
        Save(Path.Combine(output,"model-info.json"),new {NativeWorlds=0,Stopwatch.Frequency,
            Seconds=timer.Elapsed.TotalSeconds,GridRows=rows.Count,Matched=shots.Count});
    }

    private static void Certify205(string repository,string output)
    {
        var archive=Read199<Archive204>(Path.Combine(repository,NativeSource205));
        var run=archive.Run.Riders.Single();
        var controlsResults=new List<object>();
        foreach(var variant in Physics205)
        {
            int total=0,checks=0;
            double maxForce=0,maxFy=0,maxApplied=0,maxWheel=0;
            var failures=new List<object>();
            var unresolvedReserve=new List<int>();
            for(int i=0;i<run.Samples.Length-1;i++)
            {
                var shot=NativeShot205(archive,i,"accepted #204 state");
                var old=run.Samples[i+1];
                shot=shot with {Start=shot.Start with {Active=old.Input.Active}};
                total++;
                try
                {
                    var q=Query205(shot,variant);
                    var step=q.Query.Solve(q.Lo,q.Hi,q.Posture);
                    var p=q.Shaft.Prepared;
                    var precision=step.LoadH26!.Precision!;
                    if(!FreeLearningExperiment.SteeringStepIsPhysical(step.Forces,shot.Dt)||
                        p.Wheel.LongitudinalLossJoules< -1e-6||p.Wheel.RearUtilization>1.000001||
                        Math.Abs(precision.AppliedResidual)>2e-5||Math.Abs(p.EnergyResidualJoules)>1e-6)
                        throw new InvalidOperationException("Archived state violated applied contact invariant.");
                    if(precision.Combined is {} e)
                    {
                        maxForce=Math.Max(maxForce,Math.Abs(e.AppliedTyreFxResidual));
                        maxFy=Math.Max(maxFy,Math.Abs(e.AppliedTyreFyResidual));
                        if(e.LateralDissipationJoules< -1e-6||Math.Abs(e.AppliedTyreFxResidual)>.001||
                            Math.Abs(e.AppliedTyreFyResidual)>.001)
                            throw new InvalidOperationException("Archived state violated combined tyre invariant.");
                        var (rear,front)=q.Query.ReserveEvidence(q.Lo,q.Hi);
                        if(!rear.ExcludesZero||!front.ExcludesZero) unresolvedReserve.Add(i);
                    }
                    maxApplied=Math.Max(maxApplied,Math.Abs(precision.AppliedResidual));
                    maxWheel=Math.Max(maxWheel,Math.Abs(precision.WheelImpulseResidual));
                    checks++;
                }
                catch(InvalidOperationException e){failures.Add(new {Index=i,shot.Start,shot.Dt,Error=e.Message});}
            }
            controlsResults.Add(new {Variant=variant,Total=total,Accepted=checks,MaxFxMismatch=maxForce,MaxFyMismatch=maxFy,
                MaxAppliedResidual=maxApplied,MaxWheelImpulse=maxWheel,Failures=failures,UnresolvedReserve=unresolvedReserve});
        }
        var traces=new List<object>();
        foreach(var shot in Shots205(repository,archive).Where(x=>new[]{"196","start-0.370","plus-2-cm","bend-1-apex",
            "min-rear-archive","min-front-alt"}.Contains(x.Name)))
        foreach(var variant in Physics205)
        {
            var q=Query205(shot,variant);
            var points=new List<object>();
            for(int i=0;i<=256;i++)
            {
                double ax=q.Lo+(q.Hi-q.Lo)*i/256;
                try
                {
                    var trial=q.Query.Evaluate(ax);
                    var v=trial.Value;
                    points.Add(new {Ax=ax,trial.Residual,Fx=v.Power.Wheel.Force,Fy=v.RearFy,
                        Fzf=v.Loads.FrontNormalNewtons,Fzr=v.Loads.RearNormalNewtons,
                        Steer=v.AfterSteering,Torque=v.Power.Torque,Rpm=v.Power.EngineMid*30/Math.PI});
                }
                catch(InvalidOperationException e){points.Add(new {Ax=ax,Error=e.Message});}
            }
            object? reserve=null;
            if(variant=="H28-C")
            {
                var (rear,front)=q.Query.ReserveEvidence(q.Lo,q.Hi);
                reserve=new {RearUnloaded=rear,FrontUnloaded=front};
            }
            traces.Add(new {shot.Name,Variant=variant,q.Lo,q.Hi,Points=points,Outcome=Measure205(shot,variant),Reserve=reserve});
        }
        Save(Path.Combine(output,"certification-model.json"),new {Archive=controlsResults,ResidualTraces=traces,NativeWorlds=0});
    }
}
