using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
namespace SharedWorldSpeedway;
public static class Telemetry143
{
    static readonly JsonSerializerOptions Json=FreeLearningExperiment.Json;
    static JsonNode Node(object x)=>JsonSerializer.SerializeToNode(x,Json)!;
    static double P(double[] x,double fraction)=>x.Order().ElementAt(Math.Min(x.Length-1,(int)Math.Floor((x.Length-1)*fraction)));
    public static JsonNode Summary(RobustEpisode run,double stopReference){
        var s=run.Replay.Samples;var t=run.Telemetry;var f=TrackAwarenessExperiment134.Frame(run.GeometryName!);var active=new double[3];var requested=new double[3];
        int Bin(double gas)=>gas<=1e-8?0:gas>=.99?2:1;
        foreach(var (a,b) in s.Zip(s.Skip(1))){var dt=b.Input.Time-a.Input.Time;active[Bin(a.Input.Active.Drive)]+=dt;requested[Bin(a.RequestedDrive)]+=dt;}
        var duration=s[^1].Input.Time-s[0].Input.Time;
        object? At(double reference){
            if(reference<s[0].Input.Rider.TotalReferenceMeters-1e-8)return null;
            var n=Array.FindIndex(s,q=>q.Input.Rider.TotalReferenceMeters>=reference);if(n<0)return null;var b=s[n];var a=s[Math.Max(0,n-1)];
            var ds=b.Input.Rider.TotalReferenceMeters-a.Input.Rider.TotalReferenceMeters;var u=ds>0?Math.Clamp((reference-a.Input.Rider.TotalReferenceMeters)/ds,0,1):1;
            return new{Reference=reference,Time=a.Input.Time+(b.Input.Time-a.Input.Time)*u,SpeedKph=(a.Input.Rider.Speed+(b.Input.Rider.Speed-a.Input.Rider.Speed)*u)*3.6,
                PhysicalDistance=a.Input.Rider.DistanceMeters+(b.Input.Rider.DistanceMeters-a.Input.Rider.DistanceMeters)*u,
                ActiveGas=b.Input.Active.Drive,RequestedGas=b.RequestedDrive,SteeringDegrees=b.Input.Steering*180/Math.PI,BetaDegrees=b.Input.Rider.SlideRadians*180/Math.PI,YawRate=b.Input.Rider.AngularVelocity,
                BeforeTime=a.Input.Time,AfterTime=b.Input.Time,Basis="Scalar time/speed/distance interpolation between adjacent native states at identical reference; gas/angles from first accepted state after crossing. No simulated or planner state is interpolated."};
        }
        var corners=new List<object>();var preparation=new List<object>();
        for(int corner=1;corner<=3;corner++){
            var lap=(corner-1)/2*f.LapMeters;var start=lap+f.SegmentStartMeters(corner%2==1?1:5);var end=lap+f.SegmentStartMeters(corner%2==1?4:8);if(start>stopReference)break;
            corners.Add(new{Corner=corner,Entry=At(start),Exit=At(end),Before20=At(end-20),Before10=At(end-10),Before5=At(end-5),Plus10=At(end+10),Plus20=At(end+20),Plus31=At(end+31),Plus40=At(end+40),Plus62=At(end+62)});
            var nextStart=end+62;
            foreach(var decision in run.Replay.Decisions){var native=s.MinBy(q=>Math.Abs(q.Input.Time-decision.Time))!;var distance=nextStart-native.Input.Rider.TotalReferenceMeters;
                if(distance<0||distance>62)continue;
                var index=Array.IndexOf(run.Replay.Decisions,decision);var previous=index>0?run.Replay.Decisions[index-1].Action:run.Replay.Samples[0].Input.Active;
                if(decision.Action.Drive>=previous.Drive-1e-7&&Math.Abs(decision.Action.SteeringRadians-previous.SteeringRadians)<1e-7)continue;
                var activation=run.Replay.Activations.FirstOrDefault(a=>Math.Abs(a.ObservedAt-decision.Time)<1e-8&&a.Action==decision.Action);
                var sample=activation is null?null:s.MinBy(q=>Math.Abs(q.Input.Time-activation.ActivationAt));
                preparation.Add(new{AfterCorner=corner,ObservationTime=decision.Time,Request=decision.Action,MetersBeforeNextBend=distance,
                    Activation=activation?.ActivationAt,ActivationMetersBeforeNextBend=sample is null?(double?)null:nextStart-sample.Input.Rider.TotalReferenceMeters,
                    Meaning="Observed gas reduction or steering change on actual straight, not an inferred AI intention or fixed corner script."});
            }
        }
        return Node(new{ActiveGasSeconds=active,RequestedGasSeconds=requested,ActiveGasFractions=active.Select(x=>x/duration),RequestedGasFractions=requested.Select(x=>x/duration),
            GasOrder="zero,partial,full; time integrated over complete native recording, no N/D coerced to0",Corners=corners,Preparation=preparation,
            Speed=new{MaximumKph=s.Max(q=>q.Input.Rider.Speed)*3.6,MedianKph=P(s.Select(q=>(double)q.Input.Rider.Speed*3.6).ToArray(),.5)},
            Beta=new{MedianAbsDegrees=P(s.Select(q=>Math.Abs(q.Input.Rider.SlideRadians)*180/Math.PI).ToArray(),.5),P95AbsDegrees=P(s.Select(q=>Math.Abs(q.Input.Rider.SlideRadians)*180/Math.PI).ToArray(),.95),MaximumAbsDegrees=s.Max(q=>Math.Abs(q.Input.Rider.SlideRadians))*180/Math.PI},
            Yaw=new{MedianAbs=P(s.Select(q=>(double)Math.Abs(q.Input.Rider.AngularVelocity)).ToArray(),.5),P95Abs=P(s.Select(q=>(double)Math.Abs(q.Input.Rider.AngularVelocity)).ToArray(),.95),MaximumAbs=s.Max(q=>Math.Abs(q.Input.Rider.AngularVelocity))},
            Wheel=new{MedianAbsSlip=P(t.Select(q=>Math.Abs(q.Wheel.InstantSlipRatio)).ToArray(),.5),P95AbsSlip=P(t.Select(q=>Math.Abs(q.Wheel.InstantSlipRatio)).ToArray(),.95),MaximumAbsSlip=t.Max(q=>Math.Abs(q.Wheel.InstantSlipRatio)),MaximumOmega=s.Max(q=>q.Input.Omega)},
            Forces=new{FrontFxMean=t.Average(q=>Vector2.Dot(q.Forces.FrontForce,q.Forces.BodyForward)),FrontFyMean=t.Average(q=>q.Forces.FrontLateral),RearFxMean=t.Average(q=>q.Forces.RearLongitudinal),RearFyMean=t.Average(q=>q.Forces.RearLateral),
                RearUtilizationMean=t.Average(q=>q.Wheel.RearUtilization),RearUtilizationP95=P(t.Select(q=>q.Wheel.RearUtilization).ToArray(),.95),FrontUtilizationMean=t.Average(q=>q.Forces.FrontForce.Length()/q.Forces.TyreLoads!.FrontCapacityNewtons)},
            MinimumPhysicalInner=s.Min(q=>q.Inner),MinimumPhysicalOuter=s.Min(q=>q.Outer),MinimumAcceptedCertificate=t.Min(q=>q.AcceptedCertificate),
            PhysicalDistance=s[^1].Input.Rider.DistanceMeters-s[0].Input.Rider.DistanceMeters,SteeringMaximumAbsDegrees=s.Max(q=>Math.Abs(q.Input.Steering))*180/Math.PI});
    }
    public static void Write(string path,RobustEpisode run){
        using var z=new GZipStream(File.Create(path),CompressionLevel.Optimal);using var w=new StreamWriter(z);var telemetry=run.Telemetry.ToDictionary(t=>Math.Round(t.Time,8));
        w.WriteLine("time,reference_m,distance_m,x_m,y_m,vx_mps,vy_mps,speed_kph,psi_rad,chi_rad,beta_rad,yaw_rads,omega_rads,active_gas,requested_gas,pending_gas,steering_rad,requested_steering_rad,inner_m,outer_m,certificate_m,front_Fx_N,front_Fy_N,rear_Fx_N,rear_Fy_N,front_yaw_Nm,rear_yaw_Nm,front_util,rear_util,longitudinal_slip,rear_lateral_slip_mps,engine_rpm,available_wheel_torque_Nm,applied_wheel_torque_Nm,body_energy_delta_J,energy_residual_J,interval_seconds");
        string S(object? o)=>o switch{null=>"",double d=>d.ToString("R",CultureInfo.InvariantCulture),float f=>f.ToString("R",CultureInfo.InvariantCulture),_=>Convert.ToString(o,CultureInfo.InvariantCulture)??""};
        foreach(var s in run.Replay.Samples){var i=s.Input;var v=i.Rider;telemetry.TryGetValue(Math.Round(i.Time,8),out var t);
            var values=new object?[]{i.Time,v.TotalReferenceMeters,v.DistanceMeters,v.Position.X,v.Position.Y,v.Velocity.X,v.Velocity.Y,v.Speed*3.6,v.Heading,Math.Atan2(v.Velocity.Y,v.Velocity.X),v.SlideRadians,v.AngularVelocity,i.Omega,
                i.Active.Drive,s.RequestedDrive,i.Pending.FirstOrDefault()?.Action.Drive,i.Steering,s.RequestedSteering,s.Inner,s.Outer,t?.AcceptedCertificate,
                t is null?null:Vector2.Dot(t.Forces.FrontForce,t.Forces.BodyForward),t?.Forces.FrontLateral,t?.Forces.RearLongitudinal,t?.Forces.RearLateral,t?.Forces.FrontTorque,t?.Forces.RearTorque,
                t is null?null:t.Forces.FrontForce.Length()/t.Forces.TyreLoads!.FrontCapacityNewtons,t?.Wheel.RearUtilization,t?.Wheel.InstantSlipRatio,
                t is null?null:Vector2.Dot(t.Forces.RearVelocity,t.Forces.BodyLeft),t?.Forces.Engine?.EngineRpm,t?.Forces.Engine?.AvailableWheelTorqueNewtonMeters,t?.Wheel.DriveTorqueNewtonMeters,
                t?.BodyEnergyDelta,t?.EnergyResidual,t?.Seconds};w.WriteLine(string.Join(',',values.Select(S)));
        }
    }
}
