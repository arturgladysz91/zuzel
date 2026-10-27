using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
namespace SharedWorldSpeedway;

public sealed record LocalState140(RiderView Rider,double Omega,float Steering,double Time);
public sealed record LocalRun140(double DtSeconds,bool Separated,LocalState140 End,TwoContactForces Endpoint,
    double EndKappa,double MaximumSolverResidualMetersPerSecond,int MaximumIterations,double MaximumEnergyGainJoules,
    double MaximumFrontPowerWatts,double CpuSeconds,int ContactQueries);

public static class Mathematics140
{
    public static readonly double[] Steps={.020,.010,.005,.0025};
    private static readonly Dictionary<string,(TrackFrame Frame,TrackStateSnapshot Surface)> Contexts=new();
    public static ContactState139[] States()=>JsonSerializer.Deserialize<ContactState139[]>(File.ReadAllText(Path.Combine(Experiment140.RepoRoot(),
        "docs/experiments/motorcycle-physics-validation/contact-states.json")),FreeLearningExperiment.Json)!;
    public static (TwoContactForces Force,RearWheelStep Wheel) Query(ContactState139 anchor,LocalState140 current,bool separated,double dt,double iw=.9){
        if(!Contexts.TryGetValue(anchor.Case.Geometry,out var context)){
            var frame=TrackAwarenessExperiment134.Frame(anchor.Case.Geometry);context=(frame,MultiActionExperiment135.Surface(frame));Contexts[anchor.Case.Geometry]=context;
        }
        var wp=RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw};var wheel=new RearWheelState(current.Omega*.33,wp);
        var f=Contact140.Forces(current.Rider,FreeLearningExperiment.Rider(anchor.Case.Scenario),context.Frame,context.Surface,
            FreeLearningExperiment.Parameters(),current.Time,147,wheel,new(current.Steering),(float)dt,
            steeringOverride:current.Steering,inertia:52.72097f,separated:separated);
        return(f,wheel.Prepared);
    }
    public static LocalState140 Initial(ContactState139 s)=>new(s.Input.Rider with{Drive=1},s.Input.Omega,s.Input.Steering,s.Input.Time);
    public static double StateError(LocalState140 a,LocalState140 b)=>Math.Sqrt(Vector2.DistanceSquared(a.Rider.Velocity,b.Rider.Velocity)
        +Math.Pow(.33*(a.Omega-b.Omega),2)+Math.Pow(1.3*(a.Rider.AngularVelocity-b.Rider.AngularVelocity),2)
        +Math.Pow(BikeAngles.Wrap(a.Rider.Heading-b.Rider.Heading),2));
    public static LocalRun140 LocalRun(ContactState139 anchor,LocalState140 initial,bool separated,double dt,double duration=.2){
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var state=initial;double residual=0,gain=0,power=double.NegativeInfinity;int iterations=0;
        var count=(int)Math.Round(duration/dt);
        for(var j=0;j<count;j++){
            var q=Query(anchor,state,separated,dt);var f=q.Force;var w=q.Wheel;var v=state.Rider;var h=(float)dt;
            var a=f.NetForce/147;var alpha=f.NetTorque/52.72097f;var nextVelocity=v.Velocity+a*h;
            var nextSpin=v.AngularVelocity+alpha*h;var coefficient=(FreeLearningExperiment.Parameters().SolverSubsteps+1f)/(2*FreeLearningExperiment.Parameters().SolverSubsteps);
            var next=v with{Velocity=nextVelocity,Position=v.Position+v.Velocity*h+a*(coefficient*h*h),AngularVelocity=nextSpin,
                Heading=(float)BikeAngles.Wrap(v.Heading+v.AngularVelocity*h+coefficient*alpha*h*h)};
            var delta=TwoContactBikeAudit.BodyKinetic(next,147,52.72097f)-TwoContactBikeAudit.BodyKinetic(v,147,52.72097f)
                +.45*(w.AfterOmega*w.AfterOmega-w.BeforeOmega*w.BeforeOmega);
            gain=Math.Max(gain,delta-w.EngineWorkJoules);power=Math.Max(power,Vector2.Dot(f.FrontForce,f.FrontVelocity));
            residual=Math.Max(residual,Math.Abs(w.RootResidualMetersPerSecond));iterations=Math.Max(iterations,w.RootIterations);
            state=new(next,w.AfterOmega,state.Steering,initial.Time+(j+1)*dt);
            if(!double.IsFinite(state.Omega)||!float.IsFinite(next.Speed))throw new InvalidOperationException("Non-finite local evolution.");
        }
        var end=Query(anchor,state,separated,0);var u=Vector2.Dot(end.Force.RearVelocity,end.Force.BodyForward);
        return new(dt,separated,state,end.Force,RearWheelDynamics.SlipRatio(u,state.Omega,RearWheelParameters.Default),residual,iterations,gain,power,
            (Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,count+1);
    }
    public static object InertiaTest(){
        var s=States().Single(x=>x.Name=="D0-failed");var angle=s.Input.Steering*180/Math.PI;var rows=new List<object>();var forces=new List<TyreForce140>();
        foreach(var iw in new[]{.45,.9,1.8}){
            var old=PhysicsAudit139.Query(s,angle,gas:1,wheelInertia:iw);var f=old.Force;var u=Vector2.Dot(f.RearVelocity,f.BodyForward);var side=Vector2.Dot(f.RearVelocity,f.BodyLeft);
            var material=TyreLaw140.Evaluate(.33*s.Input.Omega-u,side,u,f.TyreLoads!.RearNormalNewtons,f.TyreLoads.EffectiveFrictionCoefficient);forces.Add(material);
            var step=WheelSolver140.Evaluate(u,side,s.Input.Omega,f.Budget,147,old.Wheel.RequestedTorqueNewtonMeters,
                old.Wheel.AvailableTorqueNewtonMeters,old.Wheel.DriveTorqueNewtonMeters,Vector2.Dot(f.Resistance+f.FrontForce,f.BodyForward),.01f,
                RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw},f.TyreLoads.RearNormalNewtons,f.TyreLoads.EffectiveFrictionCoefficient);
            rows.Add(new{IwKgMetersSquared=iw,ContactForwardMetersPerSecond=u,ContactLateralMetersPerSecond=side,OmegaRadPerSecond=s.Input.Omega,
                LongitudinalSlipMetersPerSecond=.33*s.Input.Omega-u,NormalNewtons=f.TyreLoads.RearNormalNewtons,f.TyreLoads.EffectiveFrictionCoefficient,
                OldFxNewtons=f.RearLongitudinal,OldFyNewtons=f.RearLateral,NewPure=material,NewStep=step});
        }
        var error=forces.Max(x=>Math.Abs(x.FxNewtons-forces[0].FxNewtons))+forces.Max(x=>Math.Abs(x.FyNewtons-forces[0].FyNewtons));
        return new{Anchor=s.Name,AnchorIsLegal=false,DtPureSeconds=0,MaximumInertiaForceDifferenceNewtons=error,Rows=rows,
            Passed=error==0,Meaning="Same instantaneous material input, different physical Iw in the finite-step solver. No claim of identical future trajectories."};
    }
    public static object PhysicsGrid(){
        double maximumResidual=0,maximumEnergyError=0,maximumRelativeError=0,maximumUse=0,minimumLoss=double.PositiveInfinity;int maximumIterations=0,count=0;
        foreach(var u in new[]{0d,.001,10,30})foreach(var side in new[]{0d,1,10})foreach(var spin in new[]{.2,1d,20})
        foreach(var iw in new[]{.45,.9,1.8})foreach(var dt in Steps)foreach(var torque in new[]{0d,200})foreach(var normal in new[]{0d,700,1000}){
            var wp=RearWheelParameters.Default with{WheelInertiaKgMetersSquared=iw};var omega=u/.33*spin+10*spin;const double mass=147,mu=1.2845000267028808,external=-40;
            var w=WheelSolver140.Evaluate(u,side,omega,(float)(mu*normal+1000),147,torque,torque,torque,external,(float)dt,wp,normal,mu);
            var h=w.Seconds;var uAfter=u+h*(w.LongitudinalNewtons+external)/mass;var uMid=(u+uAfter)/2;
            var delta=.5*mass*(uAfter*uAfter-u*u)+.5*iw*(w.AfterOmega*w.AfterOmega-omega*omega);
            var residual=delta-w.EngineWorkJoules-external*uMid*h+w.LongitudinalLossJoules+w.BearingLossJoules;
            var relative=Math.Abs(residual)/(1+.5*mass*u*u+.5*iw*omega*omega+Math.Abs(w.EngineWorkJoules));
            maximumEnergyError=Math.Max(maximumEnergyError,Math.Abs(residual));maximumRelativeError=Math.Max(maximumRelativeError,relative);
            maximumResidual=Math.Max(maximumResidual,Math.Abs(w.RootResidualMetersPerSecond));maximumUse=Math.Max(maximumUse,w.RearUtilization);
            minimumLoss=Math.Min(minimumLoss,Math.Min(w.LongitudinalLossJoules,w.BearingLossJoules));maximumIterations=Math.Max(maximumIterations,w.RootIterations);count++;
            if(!double.IsFinite(relative)||!double.IsFinite(w.AfterOmega)||w.FrontLateralNewtons!=0)throw new InvalidOperationException("Grid invariant failed.");
        }
        return new{Queries=count,MaximumSolverResidualMetersPerSecond=maximumResidual,MaximumEnergyResidualJoules=maximumEnergyError,
            MaximumNormalizedEnergyResidual=maximumRelativeError,MaximumRearUtilization=maximumUse,MinimumDissipationJoules=minimumLoss,MaximumIterations=maximumIterations,
            Passed=maximumResidual<=2e-5&&maximumRelativeError<=1e-10&&maximumUse<=1.000001&&minimumLoss>=-1e-6,
            EnergyMeaning="Exact wheel + longitudinal body midpoint ledger with external work. Lateral motion/Box2D assessed separately, not silently included here."};
    }
    public static object FrontTest(){
        var s=States()[0];double drive=0,dtDifference=0,power=double.NegativeInfinity,sign=0;int count=0;
        foreach(var beta in new[]{-40d,-10,0,10,40})foreach(var angle in new[]{-35d,0,35}){
            var v=s.Input.Rider;var heading=v.Heading;var chi=heading-beta*Math.PI/180;v=v with{Velocity=new((float)(20*Math.Cos(chi)),(float)(20*Math.Sin(chi))),Drive=1};
            var current=new LocalState140(v,s.Input.Omega,(float)(angle*Math.PI/180),s.Input.Time);var zero=Query(s,current,true,0).Force;
            foreach(var h in Steps){var f=Query(s,current,true,h).Force;count++;
                var forward=new Vector2(f.FrontLeft.Y,-f.FrontLeft.X);drive=Math.Max(drive,Math.Abs(Vector2.Dot(f.FrontForce,forward)));
                power=Math.Max(power,Vector2.Dot(f.FrontForce,f.FrontVelocity));dtDifference=Math.Max(dtDifference,Vector2.Distance(f.FrontForce,zero.FrontForce));
                sign=Math.Max(sign,f.FrontLateral*Vector2.Dot(f.FrontVelocity,f.FrontLeft));
            }
        }
        return new{Queries=count,MaximumLocalDriveNewtons=drive,MaximumFrontPowerWatts=power,MaximumDtForceDifferenceNewtons=dtDifference,
            MaximumLateralSlipForceProduct=sign,Passed=drive<=.001&&power<=.001&&dtDifference==0&&sign<=.001,
            Meaning="Held actual steering equals request. A moving steering actuator can physically change the within-step axes with dt. Front original mass/capacity slope floor remains phenomenological; no temporal force memory or Iw."};
    }
    public static object Convergence(){
        var anchors=States();var configs=new List<(string Name,ContactState139 Anchor,LocalState140 State)>();
        foreach(var s in anchors.Take(3))configs.Add((s.Name,s,Initial(s)));
        var low=Initial(anchors[0]);configs.Add(("near-zero",anchors[0],low with{Rider=low.Rider with{Velocity=new(.01f,.02f),AngularVelocity=.01f,Drive=0},Omega=.03}));
        configs.Add(("large-wheelspin",anchors[0],low with{Omega=1000}));configs.Add(("negative-long-slip",anchors[0],low with{Omega=10}));
        var rows=new List<object>();bool passed=true;
        foreach(var config in configs){
            var current=Steps.Select(h=>LocalRun(config.Anchor,config.State,true,h)).ToArray();
            var old=Steps.Select(h=>LocalRun(config.Anchor,config.State,false,h)).ToArray();
            var errors=current.Zip(current.Skip(1),(a,b)=>StateError(a.End,b.End)).ToArray();
            var converging=errors[^1]<errors[0]||errors[0]<1e-5;passed&=converging;
            rows.Add(new{Scenario=config.Name,Initial=config.State,Surface="Homogeneous frozen #139 patch; no boundaries, no native world, no reachability claim",
                PhysicalTimesSeconds=new[]{.2},New=current,Old=old,AdjacentStateErrorsMetersPerSecondEquivalent=errors,
                ObservedOrders=errors.Zip(errors.Skip(1),(a,b)=>Math.Log2(a/Math.Max(b,1e-30))).ToArray(),Converging=converging});
        }
        return new{Passed=passed,Rows=rows,Meaning="Same 0.2s endpoint, same parameters for all h. Expected first-order planar held-force coupling; finest h is not an exact solution. Reports all force, omega, slip, torque and energy diagnostics."};
    }
    public static object Perturbation(){
        var s=States().Single(x=>x.Name=="D0-failed");var initial=Initial(s);var variants=new List<object>();
        foreach(var delta in new[]{-.25,0,.25}){
            var v=initial.Rider;var magnitude=v.Speed;var chi=v.Heading-v.SlideRadians-delta*Math.PI/180;
            var perturbed=initial with{Rider=v with{Velocity=new((float)(magnitude*Math.Cos(chi)),(float)(magnitude*Math.Sin(chi)))}};
            var run=LocalRun(s,perturbed,true,.0025);var f=run.Endpoint;
            var chiRate=TwoContactForces.Cross(run.End.Rider.Velocity,f.NetForce)/(147*run.End.Rider.Speed*run.End.Rider.Speed);
            variants.Add(new{DeltaBetaDegrees=delta,InitialBetaDegrees=perturbed.Rider.SlideRadians*180/Math.PI,
                FinalBetaDegrees=run.End.Rider.SlideRadians*180/Math.PI,FinalYawRate=run.End.Rider.AngularVelocity,FinalChiRate=chiRate,
                FinalBetaRate=run.End.Rider.AngularVelocity-chiRate,YawAcceleration=f.NetTorque/52.72097,FrontForce=f.FrontForce,RearForce=f.RearForce,run.EndKappa});
        }
        return new{Anchor=s.Name,AnchorLegal=false,DtSeconds=.0025,DurationSeconds=.2,Rows=variants,
            Meaning="Local perturbed ODE diagnostic from an already failed historical state, not a legal native reset or proof of a drift equilibrium. Native perturbations start before both bends."};
    }
    public static object Run(){
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;var inertia=InertiaTest();var grid=PhysicsGrid();var front=FrontTest();var convergence=Convergence();var perturbation=Perturbation();
        var json=FreeLearningExperiment.Json;bool Passed(object o)=>JsonSerializer.SerializeToElement(o,json).GetProperty("Passed").GetBoolean();
        return new{Passed=Passed(inertia)&&Passed(grid)&&Passed(front)&&Passed(convergence),Inertia=inertia,PhysicsGrid=grid,Front=front,Convergence=convergence,
            Perturbation=perturbation,CpuSeconds=(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,NativeWorlds=0};
    }
}
