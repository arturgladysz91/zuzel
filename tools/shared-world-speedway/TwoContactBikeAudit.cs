using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public static class TwoContactBikeAudit
{
    public const string BaseHead="a0a1e91dc208c7a33b56cdb4848509271d27f822";
    public sealed record Case(string Name,float Speed=18,float Beta=0,float Spin=0,float Steer=0,double Seconds=1,double Step=.01,bool Drive=false,bool Ramp=false);
    public sealed record Tick(double Time,float Seconds,RiderView Before,RiderView After,TwoContactForces Forces,RearWheelStep Wheel,double Inner,double Outer);
    public sealed record Capture(WorldSnapshot Initial,RunResult Result,Tick[] Ticks,float Mass,float Inertia,int LegacyYawCalls,string? StopReason);
    public static WorldParameters Parameters()=>SpeedGripCrossoverAudit.Parameters(null) with{EnableTwoContactBikeDynamics=true};
    public static Case[] IsolatedCases()=>new[]{
        new Case("A-straight-coast"),new Case("A-straight-drive",Drive:true),
        new Case("B-left",Steer:.04f),new Case("B-right",Steer:-.04f),
        new Case("C-spin-ramp",Beta:.15f,Seconds:1.2,Ramp:true),new Case("C-coast",Beta:.15f,Seconds:1.2),
        new Case("D-beta20",12,20*MathF.PI/180,Seconds:1.2),new Case("D-beta35",12,35*MathF.PI/180,Seconds:1.2),
        new Case("D-beta50",12,50*MathF.PI/180,Seconds:1.2),new Case("D-beta-minus35",12,-35*MathF.PI/180,Seconds:1.2),
        new Case("D-front-minus",12,35*MathF.PI/180,Steer:-.08f,Seconds:1.2),new Case("D-front-plus",12,35*MathF.PI/180,Steer:.08f,Seconds:1.2),
        new Case("E-rest",0,Steer:.04f),new Case("E-spin-only",0,Spin:2),new Case("E-low-speed",.05f,50*MathF.PI/180,Spin:.2f),
        new Case("E-left-h20",Steer:.04f,Step:.02),new Case("E-left-h5",Steer:.04f,Step:.005),new Case("E-left-repeat",Steer:.04f)};
    public static Capture Execute(Case c,bool track=false,int laps=0,bool stopUnsafe=false,WorldParameters? parameters=null,double? stopReference=null,Action<AdaptiveRiderDecision>? adaptiveObserver=null)
    {
        var path=track?AuthoredRacingPath.Crossover(false):null;var frame=RacingLineRootCauseAudit.Frame();
        var rider=RacingLineRootCauseAudit.Rider(path??AuthoredRacingPath.Crossover(false)) with{
            ReferenceMeters=track?32:4,OffsetMeters=track?1:5,TargetOffsetMeters=track?1:5,SpeedMetersPerSecond=c.Speed,TrafficEnabled=false};
        var commands=track?null:new[]{new ControlCommand(rider.Profile.Id,0,5,c.Drive?1:0)}
            .Concat(c.Ramp?new[]{new ControlCommand(rider.Profile.Id,.3,5,.5f),new ControlCommand(rider.Profile.Id,.6,5,1)}:Array.Empty<ControlCommand>()).ToArray();
        var ticks=new List<Tick>();TwoContactForces forces=default;RearWheelStep wheel=default;RiderView forceBefore=default;var yawCalls=0;string? stop=null;
        using var world=new WorldRunner(new[]{rider},parameters:parameters??Parameters(),trackShape:RacingLineRootCauseAudit.Variant(),
            frozenSurface:TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot(),authoredPath:parameters?.EnableAdaptiveRiderControl==true?null:path,
            diagnosticPathGuidance:track&&parameters?.EnableAdaptiveRiderControl!=true,diagnosticPathPreview:track&&parameters?.EnableAdaptiveRiderControl!=true,
            controls:commands,twoContactInitial:new(c.Beta,c.Spin),steeringCommands:track?null:new[]{new TwoContactSteeringCommand(rider.Profile.Id,0,c.Steer)},
            diagnostics:new(){Force=f=>forceBefore=f.Rider,TwoContact=o=>forces=o.Forces,RearWheel=w=>wheel=w.Step,YawLimit=_=>yawCalls++,AdaptiveRider=adaptiveObserver});
        var initial=world.Snapshot;var mechanical=world.MechanicalProperties(rider.Profile.Id);var geometry=new RacingLineGeometry(world.Frame);
        var previousTime=0d;
        var result=world.Run(c.Name,c.Step,c.Seconds,laps,snapshot=>{
            var after=snapshot.Riders.Single();var gap=geometry.Clearance(after);
            ticks.Add(new(previousTime,(float)wheel.Seconds,forceBefore,after,forces,wheel,gap.Inner,gap.Outer));
            previousTime=snapshot.TimeSeconds;
        },lapsFromInitialPosition:true,stopOnCrash:true,diagnosticStop:snapshot=>{
            var t=ticks[^1];var delta=BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared)-BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared)
                +.5*RearWheelParameters.Default.WheelInertiaKgMetersSquared*(t.Wheel.AfterOmega*t.Wheel.AfterOmega-t.Wheel.BeforeOmega*t.Wheel.BeforeOmega);
            // Diagnostic halt, NEVER a force correction. Allow float32 energy roundoff.
            var tolerance=.05+1e-6*Math.Max(BodyKinetic(t.Before,mechanical.MassKg,mechanical.InertiaKgMetersSquared),BodyKinetic(t.After,mechanical.MassKg,mechanical.InertiaKgMetersSquared));
            if(delta-t.Wheel.EngineWorkJoules>tolerance)stop="energy increase beyond engine work/float tolerance";
            if(stopUnsafe&&(t.Inner<0||t.Outer<0))stop=t.Inner<0?"observed inner footprint crossing":"observed outer footprint crossing";
            if(snapshot.AxleLoad is {WithinAssumptions:false})stop="axle load outside flat-contact assumptions";
            return stop is not null||stopReference is { } end&&t.After.TotalReferenceMeters>=end;
        });
        return new(initial,result,ticks.ToArray(),mechanical.MassKg,mechanical.InertiaKgMetersSquared,yawCalls,stop);
    }
    public static object ContactProbes()
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var rider=RacingLineRootCauseAudit.Rider(path);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var rows=new List<object>();
        foreach(var sign in new[]{1,-1})foreach(var multiplier in new[]{1d,1.3,1.8}){
            var beta=sign*.35f;var omega=sign*.3f;var road=frame.RoadAt(4,5);var point=frame.At(4,5).Position;
            var v=new RiderView(1,point,new(18,0),beta,omega,road,4,0,RiderActivity.Racing,1,5,0);
            var wheel=new RearWheelState(v.ForwardSpeed*multiplier);var f=TwoContactBikeDynamics.Forces(v,rider,frame,surface,Parameters(),0,147,wheel,new(),.01f,steeringOverride:0);
            rows.Add(new{Sign=sign,Multiplier=multiplier,BodySpeed=18,Beta=beta,BodyOmega=omega,RearWheelOmega=wheel.AngularVelocityRadPerSecond,Forces=f,Wheel=wheel.Prepared});
        }
        return new{BodySteps=0,WheelCommits=0,Rule="Six frozen-state original contact evaluations; vary only initial wheel omega at the same rigid-body state and zero steering. Opposite signs mirror beta/body yaw.",Rows=rows};
    }
    public static double BodyKinetic(RiderView v,float mass,float inertia)=>.5*mass*v.Velocity.LengthSquared()+.5*inertia*v.AngularVelocity*v.AngularVelocity;
    public static object Energy(Capture c)
    {
        double body=0,wheel=0,engine=0,frontLoss=0,rearLat=0,rearLong=0,bearing=0,resistance=0,frontWork=0,rearWork=0,frontYaw=0,rearYaw=0;
        double maxExcess=0,maxLinear=0,maxAngular=0,maxRoot=0,minFront=0,minRear=0;
        foreach(var t in c.Ticks){
            var f=t.Forces;var dt=t.Seconds;var mid=(t.Before.Velocity+t.After.Velocity)/2;var spin=(t.Before.AngularVelocity+t.After.AngularVelocity)/2;
            var vf=TwoContactForces.PointVelocity(mid,spin,f.FrontArm);var vr=TwoContactForces.PointVelocity(mid,spin,f.RearArm);
            var lf=-Vector2.Dot(f.FrontForce,vf)*dt;var lr=-f.RearLateral*Vector2.Dot(vr,f.BodyLeft)*dt;
            var ll=f.RearLongitudinal*(RearWheelParameters.Default.WheelRadiusMeters*t.Wheel.MidOmega-Vector2.Dot(vr,f.BodyForward))*dt;
            var db=BodyKinetic(t.After,c.Mass,c.Inertia)-BodyKinetic(t.Before,c.Mass,c.Inertia);
            var dw=.5*RearWheelParameters.Default.WheelInertiaKgMetersSquared*(t.Wheel.AfterOmega*t.Wheel.AfterOmega-t.Wheel.BeforeOmega*t.Wheel.BeforeOmega);
            body+=db;wheel+=dw;engine+=t.Wheel.EngineWorkJoules;bearing+=t.Wheel.BearingLossJoules;
            frontLoss+=lf;rearLat+=lr;rearLong+=ll;resistance+=Vector2.Dot(f.Resistance,mid)*dt;
            frontWork+=Vector2.Dot(f.FrontForce,vf)*dt;rearWork+=Vector2.Dot(f.RearForce,vr)*dt;
            frontYaw+=f.FrontTorque*spin*dt;rearYaw+=f.RearTorque*spin*dt;
            maxExcess=Math.Max(maxExcess,db+dw-t.Wheel.EngineWorkJoules);
            minFront=Math.Min(minFront,lf);minRear=Math.Min(minRear,lr+ll);
            maxLinear=Math.Max(maxLinear,((t.After.Velocity-t.Before.Velocity)*c.Mass-f.NetForce*dt).Length());
            maxAngular=Math.Max(maxAngular,Math.Abs(c.Inertia*(t.After.AngularVelocity-t.Before.AngularVelocity)-f.NetTorque*dt));
            maxRoot=Math.Max(maxRoot,Math.Abs(t.Wheel.RootResidualMetersPerSecond));
        }
        return new{BodyChangeJoules=body,WheelChangeJoules=wheel,EngineWorkJoules=engine,FrontLateralLossJoules=frontLoss,RearLateralLossJoules=rearLat,RearLongitudinalLossJoules=rearLong,
            BearingLossJoules=bearing,ResistanceWorkJoules=resistance,FrontBodyWorkJoules=frontWork,RearBodyWorkJoules=rearWork,
            FrontRotationalWorkJoules=frontYaw,RearRotationalWorkJoules=rearYaw,
            BodyWorkResidualJoules=body-frontWork-rearWork-resistance,
            BalanceResidualJoules=body+wheel-engine+frontLoss+rearLat+rearLong+bearing-resistance,
            MaximumExcessBeyondEngineJoules=maxExcess,MaximumLinearImpulseResidualNs=maxLinear,MaximumAngularImpulseResidualNms=maxAngular,MaximumRootResidual=maxRoot,
            MinimumFrontIntervalLossJoules=minFront,MinimumRearIntervalLossJoules=minRear};
    }
    public static object Summary(Capture c)=>new{c.Result,c.Initial,c.Mass,c.Inertia,c.LegacyYawCalls,c.StopReason,Energy=Energy(c),
        MaximumSlideDegrees=c.Ticks.Max(t=>Math.Abs(t.Before.SlideRadians)*180/Math.PI),FinalSlideDegrees=c.Ticks[^1].After.SlideRadians*180/Math.PI,
        MaximumSpin=c.Ticks.Max(t=>Math.Abs(t.Before.AngularVelocity)),MaximumWheelspin=c.Ticks.Max(t=>t.Wheel.InstantSlipRatio),
        MinimumInner=c.Ticks.Min(t=>t.Inner),MinimumOuter=c.Ticks.Min(t=>t.Outer),
        ConservativeInner=c.Ticks.Min(t=>t.Inner-(Math.Max(t.Before.Speed,t.After.Speed)+1.05*Math.Max(Math.Abs(t.Before.AngularVelocity),Math.Abs(t.After.AngularVelocity)))*t.Seconds/2-.012),
        ConservativeOuter=c.Ticks.Min(t=>t.Outer-(Math.Max(t.Before.Speed,t.After.Speed)+1.05*Math.Max(Math.Abs(t.Before.AngularVelocity),Math.Abs(t.After.AngularVelocity)))*t.Seconds/2-.012)};
    public static void Export(string dir,string name,Capture c)
    {
        InverseFeasibilityAudit.Save(dir,name+".json",Summary(c));
        using var writer=InverseFeasibilityAudit.Csv(dir,name+".csv","t,dt,s,x,y,vx,vy,speed,heading,velocity_heading,beta,body_omega,steer_request,steer_before,steer_used,steer_after,front_vx,front_vy,rear_vx,rear_vy,front_fx,front_fy,rear_fx,rear_fy,resistance_x,resistance_y,front_torque,rear_torque,budget,front_lateral,rear_lateral,rear_longitudinal,wheelspin,omega_before,omega_after,engine_work,bearing_loss,front_arm_x,front_arm_y,rear_arm_x,rear_arm_y,inner,outer,drive");
        foreach(var t in c.Ticks){var v=t.Before;var f=t.Forces;var w=t.Wheel;
            InverseFeasibilityAudit.Row(writer,t.Time,t.Seconds,v.TotalReferenceMeters,v.Position.X,v.Position.Y,v.Velocity.X,v.Velocity.Y,v.Speed,v.Heading,Math.Atan2(v.Velocity.Y,v.Velocity.X),v.SlideRadians,v.AngularVelocity,
                f.SteeringRequest,f.SteeringBefore,f.SteeringUsed,f.SteeringAfter,f.FrontVelocity.X,f.FrontVelocity.Y,f.RearVelocity.X,f.RearVelocity.Y,
                f.FrontForce.X,f.FrontForce.Y,f.RearForce.X,f.RearForce.Y,f.Resistance.X,f.Resistance.Y,f.FrontTorque,f.RearTorque,f.Budget,f.FrontLateral,f.RearLateral,f.RearLongitudinal,
                w.InstantSlipRatio,w.BeforeOmega,w.AfterOmega,w.EngineWorkJoules,w.BearingLossJoules,f.FrontArm.X,f.FrontArm.Y,f.RearArm.X,f.RearArm.Y,t.Inner,t.Outer,v.Drive);
        }
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"freeze.json",new{BaseHead,Parameters=Parameters(),Bike=TwoContactBikeParameters.Default,Wheel=RearWheelParameters.Default,Cases=IsolatedCases(),
                Geometry=RacingLineRootCauseAudit.Variant(),TrackRule="Baseline original engine, Grip1/0/.5, Drive1, existing C3 INNER, initial ref32/v18/rolling wheel/steer0. Short12s with actual footprint crossing or energy excess halt. Long3laps/max70s only after all isolated and short safety/stability gates pass. No line or parameter fitting.",
                EnergyRule="Original front and rear laws at local contact velocities, midpoint rear wheel including front/resistance external longitudinal force. Front/rear applied at world points; no ApplyTorque. Ledger uses native endpoint point-velocity work including rotational work exactly once.",
                Provisional="Axles+.70/-.60 from unchanged CoM, steering35deg/rate90deg/s, static40/60 budget independent of axle lever arms. No measured-real-bike claim."});return;
        }
        using var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
        if(freeze.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json)
            ||freeze.RootElement.GetProperty("Bike").GetRawText()!=JsonSerializer.Serialize(TwoContactBikeParameters.Default,InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen settings changed.");
        if(stage=="isolated"){
            var all=new List<object>();foreach(var item in IsolatedCases()){
                var c=Execute(item);Export(dir,item.Name,c);all.Add(new{Case=item,Summary=Summary(c)});Console.WriteLine(item.Name+": "+JsonSerializer.Serialize(Energy(c),InverseFeasibilityAudit.Json));
            }
            InverseFeasibilityAudit.Save(dir,"isolated-runs.json",all);return;
        }
        if(stage.StartsWith("case=",StringComparison.Ordinal)){
            var item=IsolatedCases().Single(c=>c.Name==stage[5..]);Export(dir,item.Name,Execute(item));return;
        }
        if(stage=="contact-probes"){InverseFeasibilityAudit.Save(dir,"contact-probes.json",ContactProbes());return;}
        if(stage=="verify-off"){
            using var original=JsonDocument.Parse(File.ReadAllText("docs/experiments/speed-grip-line-crossover/baseline-inner.json"));
            var repeat=SpeedGripCrossoverAudit.Execute(false).Body;
            var expected=original.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
            if(expected!=repeat.Result.Fingerprint)throw new InvalidOperationException("Default-OFF #106 baseline fingerprint changed.");
            InverseFeasibilityAudit.Save(dir,"off-verification.json",new{Source="#106 baseline INNER, also reused in #107",Expected=expected,Actual=repeat.Result.Fingerprint,
                Matches=true,Worlds=1,Rule="One full historical baseline repeat to prove OFF; no historical matrix. #111 Power3/4 native-prefix and geometry regression tests also retained."});return;
        }
        if(stage is "track-short" or "track-long"){
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,stage=="track-short"?"isolated-gate.json":"short-gate.json")));
            if(!gate.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Previous physical/safety gate failed.");
            var c=Execute(new(stage,Seconds:stage=="track-short"?12:70,Drive:true),track:true,laps:stage=="track-short"?0:3,stopUnsafe:true);Export(dir,stage,c);return;
        }
        throw new ArgumentException("Use freeze, isolated, track-short or track-long.");
    }
}
