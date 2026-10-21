using System.Globalization;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>#113: recorded-state queries first; no body integration in diagnosis.</summary>
public static class PhysicalSteeringAudit
{
    public const string BaseHead="c5cd35e37e7f44d877067671577e4e4cddc56ccf";
    public const string Previous="docs/experiments/two-contact-bike-dynamics";
    public static WorldParameters Parameters()=>TwoContactBikeAudit.Parameters() with{EnablePhysicalSteeringControl=true};
    public static string PreviousPath(string name)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(Path.Combine(root.FullName,"SpeedwayManager.sln")))root=root.Parent;
        return Path.Combine(root?.FullName??throw new DirectoryNotFoundException("Repository root required."),Previous,name);
    }
    public static Dictionary<string,double>[] ReadPrevious()
    {
        var lines=File.ReadAllLines(PreviousPath("track-short.csv"));var keys=lines[0].Split(',');
        return lines.Skip(1).Select(line=>line.Split(',').Select((value,i)=>(keys[i],double.Parse(value,CultureInfo.InvariantCulture))).ToDictionary(x=>x.Item1,x=>x.Item2)).ToArray();
    }
    public static RiderView View(Dictionary<string,double> r,TrackFrame frame)
    {
        var position=new Vector2((float)r["x"],(float)r["y"]);
        return new(1,position,new((float)r["vx"],(float)r["vy"]),(float)r["heading"],(float)r["body_omega"],frame.Project(position),r["s"],0,RiderActivity.Racing,(float)r["drive"],1,0);
    }
    public static void Diagnose(string dir)
    {
        var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var p=TwoContactBikeAudit.Parameters();var geometry=TwoContactBikeParameters.Default;
        var rows=ReadPrevious();var states=new List<object>();double maxForceReplay=0,maxSteerReplay=0;
        using var writer=InverseFeasibilityAudit.Csv(dir,"previous-path.csv","t,path_x,path_y,path_heading,path_curvature,preview_curvature,demand,actual_motion_curvature,actual_motion_yaw,body_yaw,normal_error,heading_error,beta");
        foreach(var r in rows){
            var v=View(r,frame);var q=path.At(r["s"]);var guidance=path.Guidance(v,p,true);
            TwoContactForces Query(float? angle=null,bool held=false){
                var wheel=new RearWheelState(r["omega_before"]*RearWheelParameters.Default.WheelRadiusMeters);
                return TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,r["t"],147,wheel,new(held?angle!.Value:(float)r["steer_before"]),(float)r["dt"],guidance,angle);
            }
            var f=Query();var motion=TwoContactForces.Cross(v.Velocity,f.NetForce)/(147*v.Speed*v.Speed);
            maxForceReplay=Math.Max(maxForceReplay,Math.Max(Math.Abs(f.FrontForce.X-r["front_fx"]),Math.Max(Math.Abs(f.FrontForce.Y-r["front_fy"]),Math.Max(Math.Abs(f.RearForce.X-r["rear_fx"]),Math.Abs(f.RearForce.Y-r["rear_fy"])))));
            maxSteerReplay=Math.Max(maxSteerReplay,Math.Abs(f.SteeringUsed-r["steer_used"]));
            var normal=CoreSim.PhysicalSpace.MeterPoint.Dot(new(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y),q.Outward);
            InverseFeasibilityAudit.Row(writer,r["t"],q.Position.X,q.Position.Y,q.Heading,q.Curvature,guidance.FeedForwardCurvature,f.Demand,motion/v.Speed,motion,v.AngularVelocity,normal,BikeAngles.Wrap(q.Heading-v.Heading),v.SlideRadians);
            if(!new[]{0d,.2,.5,.94,2,4,5.63}.Any(t=>Math.Abs(t-r["t"])<1e-7))continue;
            object Result(TwoContactForces value)=>new{value.SteeringRequest,value.SteeringUsed,value.FrontLateral,value.RearLateral,value.RearLongitudinal,value.FrontTorque,value.RearTorque,value.NetTorque,
                MotionYaw=TwoContactForces.Cross(v.Velocity,value.NetForce)/(147*v.Speed*v.Speed),FrontUtilization=Math.Abs(value.FrontLateral)/(.4*value.Budget)};
            var envelope=Enumerable.Range(-35,71).Select(deg=>new{Degrees=deg,Response=Result(Query(deg*MathF.PI/180,true))}).ToArray();
            states.Add(new{Time=r["t"],View=v,Path=q,Guidance=guidance,RequiredMotionYaw=f.Demand/v.Speed,PathMotionYaw=v.Speed*q.Curvature,
                Old=Result(f),RateLow=Result(Query((float)r["steer_before"]-geometry.SteeringRateRadiansPerSecond*(float)r["dt"])),
                RateHigh=Result(Query((float)r["steer_before"]+geometry.SteeringRateRadiansPerSecond*(float)r["dt"])),Envelope=envelope});
        }
        InverseFeasibilityAudit.Save(dir,"diagnosis.json",new{BaseHead,BodySteps=0,WheelCommits=0,MaximumForceReplayDifferenceNewtons=maxForceReplay,MaximumSteeringReplayDifferenceRadians=maxSteerReplay,
            Rule="Original saved #112 states, original contact equations including front load in rear midpoint. Envelope holds steering statically; RateLow/High enforce one accepted interval. Required motion yaw=demand/V is an inferred kinematic request, not an explicit old body-yaw setpoint.",States=states});
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="diagnose"){Diagnose(dir);return;}
        if(stage.StartsWith("gate=",StringComparison.Ordinal)){
            var name=stage[5..];var frame=AuthoredRacingPath.Crossover(false).Frame;
            var reference=name switch{"first-bend"=>frame.SegmentStartMeters(4)+1.05,"second-bend"=>frame.SegmentStartMeters(8)+1.05,"eight-laps"=>32+8*frame.LapMeters,_=>throw new ArgumentException("Unknown trial.")};
            using var saved=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,name+".json")));SaveGate(dir,name,saved.RootElement,reference);return;
        }
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"freeze.json",new{BaseHead,Parameters=Parameters(),Bike=TwoContactBikeParameters.Default,Wheel=RearWheelParameters.Default,
                PhysicalSteeringControl.ResponseIntervals,PhysicalSteeringControl.RootIterations,PhysicalSteeringControl.TorqueRootToleranceNewtonMeters,
                Rule="Same #112 initial body/wheel/steering, C3 INNER/progressive-b, Drive1 baseline engine. Existing preview and response times; yaw target=demand/V, torque=I*(target-bodyOmega)/SlipYawResponseSeconds. No body-heading target, beta target or integrator. Steering alone inverts unchanged local contacts. First bend, then second only if safe; eight laps only after both. Stop on actual footprint crossing or energy excess. No parameter/trajectory fitting."});return;
        }
        using var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
        if(freeze.RootElement.GetProperty("BaseHead").GetString()!=BaseHead||freeze.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json)
            ||freeze.RootElement.GetProperty("Bike").GetRawText()!=JsonSerializer.Serialize(TwoContactBikeParameters.Default,InverseFeasibilityAudit.Json)
            ||freeze.RootElement.GetProperty("Wheel").GetRawText()!=JsonSerializer.Serialize(RearWheelParameters.Default,InverseFeasibilityAudit.Json)
            ||freeze.RootElement.GetProperty("ResponseIntervals").GetInt32()!=PhysicalSteeringControl.ResponseIntervals||freeze.RootElement.GetProperty("RootIterations").GetInt32()!=PhysicalSteeringControl.RootIterations
            ||freeze.RootElement.GetProperty("TorqueRootToleranceNewtonMeters").GetDouble()!=PhysicalSteeringControl.TorqueRootToleranceNewtonMeters)throw new InvalidOperationException("Frozen steering inputs changed.");
        if(stage=="responses"){
            var results=new List<object>();var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);
            var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();
            foreach(var r in ReadPrevious().Where(r=>new[]{0d,.2,.94,4,5.63}.Any(t=>Math.Abs(t-r["t"])<1e-7))){
                var v=View(r,frame);var wheel=new RearWheelState(r["omega_before"]*RearWheelParameters.Default.WheelRadiusMeters);var state=new TwoContactBikeState((float)r["steer_before"]);
                var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,Parameters(),r["t"],147,wheel,state,(float)r["dt"],path.Guidance(v,Parameters(),true),inertia:52.72097f);
                if(state.SteeringRadians!=(float)r["steer_before"]||Math.Abs(wheel.AngularVelocityRadPerSecond-r["omega_before"])>1e-12
                    ||Math.Abs(f.SteeringAfter-f.SteeringBefore)>TwoContactBikeParameters.Default.SteeringRateRadiansPerSecond*r["dt"]+1e-6)throw new InvalidOperationException("Query mutated accepted state or violated steering rate.");
                results.Add(new{Time=r["t"],Forces=f});
            }
            var native=new[]{new TwoContactBikeAudit.Case("response-left",Steer:.04f,Seconds:.3),new("response-right",Steer:-.04f,Seconds:.3)};
            foreach(var item in native){var capture=TwoContactBikeAudit.Execute(item,parameters:Parameters());TwoContactBikeAudit.Export(dir,item.Name,capture);
                if(capture.StopReason is not null||capture.LegacyYawCalls!=0||capture.Ticks[^1].After.AngularVelocity*item.Steer<=0)throw new InvalidOperationException("Physical steering response failed.");}
            InverseFeasibilityAudit.Save(dir,"responses.json",new{Passed=true,BodyStepsInQueries=0,WheelCommitsInQueries=0,Results=results,NativeCases=native});return;
        }
        if(stage=="torque-bounds"){
            var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var results=new List<object>();
            foreach(var r in ReadPrevious().Where(r=>new[]{.2,.94,2,4,5.63}.Any(t=>Math.Abs(t-r["t"])<1e-7))){
                var v=View(r,frame);var wheel=new RearWheelState(r["omega_before"]*.33);var p=TwoContactBikeAudit.Parameters();
                var f=TwoContactBikeDynamics.Forces(v,spec,frame,surface,p,r["t"],147,wheel,new((float)r["steer_before"]),(float)r["dt"],path.Guidance(v,p,true));
                var w=wheel.Prepared;var ur=Vector2.Dot(f.RearVelocity,f.BodyForward);var wr=Vector2.Dot(f.RearVelocity,f.BodyLeft);var frontBudget=(1-wheel.Parameters.RearBudgetFraction)*f.Budget;var rearBudget=wheel.Parameters.RearBudgetFraction*f.Budget;
                // Any possible steered front force has body-forward component in
                // [-Bfront,+Bfront]. The unique scalar wheel root is monotone in
                // free slip; |FyRear| is smallest at one of these load endpoints.
                RearWheelStep Rear(double load)=>RearWheelDynamics.Evaluate(ur,wr,wheel.AngularVelocityRadPerSecond,f.Budget,147,SlipCoupledDynamics.VelocityScaleSquared(ur,f.Budget/147),
                    w.RequestedTorqueNewtonMeters,w.AvailableTorqueNewtonMeters,w.DriveTorqueNewtonMeters,load,(float)r["dt"],wheel.Parameters);
                var resistance=Vector2.Dot(f.Resistance,f.BodyForward);var a=Rear(resistance-frontBudget);var b=Rear(resistance+frontBudget);
                var armFy=TwoContactForces.Cross(f.RearArm,f.BodyLeft);var armFx=TwoContactForces.Cross(f.RearArm,f.BodyForward);
                if(wr>0||armFy>=0)throw new InvalidOperationException("This one-sided proof requires positive rear Fy and negative rear moment arm.");
                var minimumRearFy=Math.Min(a.RearLateralNewtons,b.RearLateralNewtons);
                var upper=f.FrontArm.Length()*frontBudget+armFy*minimumRearFy+Math.Abs(armFx)*rearBudget;
                var needed=52.72097*(f.Demand/v.Speed-v.AngularVelocity)/p.SlipYawResponseSeconds;
                results.Add(new{Time=r["t"],FrontBudget=frontBudget,RearBudget=rearBudget,MinimumRearLateralNewtons=minimumRearFy,ConservativeMaximumNetTorqueNm=upper,
                    TorqueToReachPdRateInPointOneSecondsNm=needed,RequestedRecoveryProvablyUnavailable=needed>upper});
            }
            InverseFeasibilityAudit.Save(dir,"torque-upper-bounds.json",new{BodySteps=0,Rule="Conservative upper bound for ALL front steering angles, even beyond mechanical range: |tauFront|<=|armFront|*Bfront; rear load range covers every possible front longitudinal projection. Original monotone wheel root bounds rear Fy, plus worst-case roundoff arm cross Fx. This proves only local instantaneous torque unavailability, never global trajectory infeasibility.",Results=results});return;
        }
        if(stage=="first-bend"){
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"responses.json")));if(!gate.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Response tests failed.");
            var capture=TwoContactBikeAudit.Execute(new(stage,Seconds:12,Drive:true),track:true,stopUnsafe:true,parameters:Parameters(),stopReference:AuthoredRacingPath.Crossover(false).Frame.SegmentStartMeters(4)+1.05);
            TwoContactBikeAudit.Export(dir,stage,capture);ExportControl(dir,stage,capture);SaveGate(dir,stage,capture,AuthoredRacingPath.Crossover(false).Frame.SegmentStartMeters(4)+1.05);return;
        }
        if(stage is "second-bend" or "eight-laps"){
            var previous=stage=="second-bend"?"first-bend":"second-bend";
            using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,previous+"-gate.json")));
            if(!gate.RootElement.GetProperty("Passed").GetBoolean())throw new InvalidOperationException("Previous short safety/energy gate failed; longer trial prohibited.");
            var frame=AuthoredRacingPath.Crossover(false).Frame;var reference=stage=="second-bend"?frame.SegmentStartMeters(8)+1.05:32+8*frame.LapMeters;
            var capture=TwoContactBikeAudit.Execute(new(stage,Seconds:stage=="second-bend"?25:200,Drive:true),track:true,laps:stage=="second-bend"?0:8,stopUnsafe:true,parameters:Parameters(),stopReference:stage=="second-bend"?reference:null);
            TwoContactBikeAudit.Export(dir,stage,capture);ExportControl(dir,stage,capture);SaveGate(dir,stage,capture,reference);return;
        }
        throw new ArgumentException("Use diagnose, freeze, torque-bounds, responses, first-bend, second-bend, eight-laps, or gate=<trial>.");
    }
    private static void SaveGate(string dir,string name,TwoContactBikeAudit.Capture c,double reference)
    {
        SaveGate(dir,name,JsonSerializer.SerializeToElement(TwoContactBikeAudit.Summary(c),InverseFeasibilityAudit.Json),reference);
    }
    private static void SaveGate(string dir,string name,JsonElement s,double reference)
    {
        var reached=s.GetProperty("Result").GetProperty("Riders")[0].GetProperty("TotalReferenceMeters").GetDouble()>=reference-1e-6;
        var observed=s.GetProperty("MinimumInner").GetDouble()>=0&&s.GetProperty("MinimumOuter").GetDouble()>=0;
        var certified=s.GetProperty("ConservativeInner").GetDouble()>0&&s.GetProperty("ConservativeOuter").GetDouble()>=.5;
        var margins=s.GetProperty("MinimumInner").GetDouble()>=.05&&s.GetProperty("MinimumOuter").GetDouble()>=.5;
        var energy=s.GetProperty("Energy");var valid=s.GetProperty("StopReason").ValueKind==JsonValueKind.Null&&s.GetProperty("LegacyYawCalls").GetInt32()==0&&energy.GetProperty("MaximumExcessBeyondEngineJoules").GetDouble()==0
            &&energy.GetProperty("MaximumLinearImpulseResidualNs").GetDouble()<.001&&energy.GetProperty("MaximumAngularImpulseResidualNms").GetDouble()<.00003;
        InverseFeasibilityAudit.Save(dir,name+"-gate.json",new{Passed=reached&&observed&&certified&&margins&&valid,Reached= reached,Reference=reference,NoObservedCrossing=observed,RequiredSafetyMargins=margins,CertifiedBetweenSamples=certified,EnergyAndContactYaw=valid,
            Rule="No per-step excess beyond engine/float tolerance; existing #112 linear/angular impulse bounds. Independently account cumulative energy residual by native impulse-error work in Python; an absolute 1 J cutoff does not scale with run length."});
    }
    private static void ExportControl(string dir,string name,TwoContactBikeAudit.Capture capture)
    {
        using var writer=InverseFeasibilityAudit.Csv(dir,name+"-control.csv","t,path_x,path_y,path_heading,path_curvature,preview_curvature,normal_error,demand,desired_yaw,actual_motion_yaw,body_yaw,requested_torque,predicted_torque,min_torque,max_torque,outside_response,rate_limited,queries");
        var path=AuthoredRacingPath.Crossover(false);
        foreach(var t in capture.Ticks){var v=t.Before;var q=path.At(v.TotalReferenceMeters);var control=t.Forces.SteeringControl!;
            var error=MeterPoint.Dot(new(v.Position.X-q.Position.X,v.Position.Y-q.Position.Y),q.Outward);
            InverseFeasibilityAudit.Row(writer,t.Time,q.Position.X,q.Position.Y,q.Heading,q.Curvature,path.Guidance(v,Parameters(),true).FeedForwardCurvature,error,t.Forces.Demand,
                control.DesiredDirectionRate,TwoContactForces.Cross(v.Velocity,t.Forces.NetForce)/(capture.Mass*v.Speed*v.Speed),v.AngularVelocity,control.RequestedTorque,control.PredictedTorque,
                control.SampledMinimumTorque,control.SampledMaximumTorque,control.OutsideSampledResponse?1:0,control.RateLimited?1:0,control.ContactQueries);
        }
    }
}
