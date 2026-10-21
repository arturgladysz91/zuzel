using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL axle geometry and massless steering, not surveyed motorcycle dimensions.</summary>
public sealed record TwoContactBikeParameters
{
    public static TwoContactBikeParameters Default {get;}=new();
    public float FrontAxleFromComMeters {get;init;}=.70f;
    public float RearAxleFromComMeters {get;init;}=.60f;
    public float MaximumSteeringRadians {get;init;}=35*MathF.PI/180;
    public float SteeringRateRadiansPerSecond {get;init;}=90*MathF.PI/180;
    public void Validate(RearWheelParameters wheel)
    {
        if(new[]{FrontAxleFromComMeters,RearAxleFromComMeters,MaximumSteeringRadians,SteeringRateRadiansPerSecond}.Any(v=>!float.IsFinite(v))
            ||FrontAxleFromComMeters<=0||RearAxleFromComMeters<=0||MaximumSteeringRadians<=0||MaximumSteeringRadians>=MathF.PI/2||SteeringRateRadiansPerSecond<=0
            ||Math.Max(FrontAxleFromComMeters,RearAxleFromComMeters)+wheel.WheelRadiusMeters>SpeedwayBikeDimensions.Reference.OverallMechanicalLengthMeters/2)
            throw new ArgumentException("Finite axle/steering inputs and wheel extents inside the unchanged mechanical length required.");
    }
}
public sealed record TwoContactInitialState(float BetaRadians=0,float BodyAngularVelocity=0,float SteeringRadians=0,double? RearWheelOmega=null)
{
    public void Validate(TwoContactBikeParameters p)
    {
        if(!float.IsFinite(BetaRadians)||!float.IsFinite(BodyAngularVelocity)||!float.IsFinite(SteeringRadians)||Math.Abs(SteeringRadians)>p.MaximumSteeringRadians
            ||RearWheelOmega is { } w&&(!double.IsFinite(w)||w<0))throw new ArgumentException("Invalid controlled initial state.");
    }
}
public sealed record TwoContactSteeringCommand(int RiderId,double AtSeconds,float AngleRadians);
public readonly record struct SteeringView(int RiderId,float AngleRadians);
public sealed class TwoContactBikeState(float initialSteering=0)
{
    public float SteeringRadians {get;private set;}=float.IsFinite(initialSteering)?initialSteering:throw new ArgumentException("Finite initial steering required.");
    public float PreparedSteeringRadians {get;internal set;}=initialSteering;
    public void Commit()=>SteeringRadians=PreparedSteeringRadians;
}
public readonly record struct TwoContactForces(Vector2 FrontPoint,Vector2 RearPoint,Vector2 FrontArm,Vector2 RearArm,
    Vector2 FrontVelocity,Vector2 RearVelocity,Vector2 FrontForce,Vector2 RearForce,Vector2 Resistance,
    Vector2 BodyForward,Vector2 FrontLeft,Vector2 BodyLeft,float FrontLateral,float RearLateral,float RearLongitudinal,
    float Budget,float SteeringRequest,float SteeringBefore,float SteeringUsed,float SteeringAfter,float Demand)
{
    public Vector2 NetForce=>FrontForce+RearForce+Resistance;
    public float FrontTorque=>Cross(FrontArm,FrontForce);
    public float RearTorque=>Cross(RearArm,RearForce);
    public float NetTorque=>FrontTorque+RearTorque;
    public static float Cross(Vector2 r,Vector2 f)=>r.X*f.Y-r.Y*f.X;
    public static Vector2 PointVelocity(Vector2 velocity,float omega,Vector2 arm)=>velocity+omega*new Vector2(-arm.Y,arm.X);
}
public readonly record struct TwoContactObservation(double TimeSeconds,int RiderId,TwoContactForces Forces);

/// <summary>Existing tyre laws at two local velocities; Box2D alone integrates the rigid body.</summary>
public static class TwoContactBikeDynamics
{
    public static TwoContactForces Forces(RiderView v,RiderSpec spec,TrackFrame frame,TrackStateSnapshot surface,WorldParameters p,
        double time,float mass,RearWheelState wheel,TwoContactBikeState state,float seconds,PathGuidanceSample? path=null,
        float? steeringOverride=null,Action<ForceObservation>? observe=null,Action<TwoContactObservation>? observeContacts=null)
    {
        if(!p.EnableTwoContactBikeDynamics||!p.EnableSlipCoupledDynamics||!p.EnableRearWheelDynamics||p.EnableCoupledSlipInverse||p.EnableFullContactYawFeedForward
            ||!float.IsFinite(seconds)||seconds is <0 or >.020001f||!float.IsFinite(mass)||mass<=0||!float.IsFinite(v.Speed)||!float.IsFinite(v.Heading)
            ||!float.IsFinite(v.AngularVelocity)||steeringOverride is { } input&&!float.IsFinite(input))throw new ArgumentException("Finite isolated two-contact variant with original rear wheel required.");
        var geometry=p.TwoContactBike??TwoContactBikeParameters.Default;
        var road=v.Road;var speed=v.Speed;
        var surfaceAtCom=surface.SampleSurface(road.Segment,Math.Clamp((float)road.OffsetMeters/LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type,frame.Track.Geometry)*4,0,4));
        var radius=frame.Track.Geometry.InnerRadiusMeters+Math.Max(0,(float)road.OffsetMeters);
        var safe=SegmentPhysics.MaxSafeTurnSpeedForRadius(radius,surfaceAtCom,spec.Profile.Skills,spec.Setup);
        var budget=mass*safe*safe/radius; // ONE inherited budget, statically split40/60.
        var ahead=Math.Max(.1,frame.ReferenceSpeed(road,speed)*2*p.YawResponseSeconds);
        var preview=frame.RoadAt(v.TotalReferenceMeters+ahead,v.TargetOffsetMeters);
        var angle=BikeAngles.Wrap(preview.Heading-road.Heading);
        var curvature=angle/(ahead+Math.Abs(angle)*Math.Max(0,road.OffsetMeters));
        var along=Vector2.Dot(v.Velocity,road.Tangent);
        var response=p.LateralResponseSeconds+SlipCoupledDynamics.OuterToYawResponseSeparation*p.SlipYawResponseSeconds;
        var demand=along*along*curvature+((float)road.OffsetMeters-v.TargetOffsetMeters)/(response*response)+2*Vector2.Dot(v.Velocity,road.Outward)/response;
        if(path is { } q){
            var delta=new MeterPoint(v.Position.X,v.Position.Y)-q.Position;var velocity=new MeterPoint(v.Velocity.X,v.Velocity.Y);
            var longitudinal=MeterPoint.Dot(velocity,q.Tangent);
            demand=longitudinal*longitudinal*q.FeedForwardCurvature+MeterPoint.Dot(delta,q.Outward)/(response*response)+2*MeterPoint.Dot(velocity,q.Outward)/response;
        }
        // A direction/curvature request moves ONLY the front wheel. No beta target,
        // heading write, yaw PD, counter-torque or ideal trajectory constraint.
        var headingError=BikeAngles.Wrap((path?.Heading??road.Heading)-v.Heading);
        var requested=steeringOverride??(float)BikeAngles.Wrap(headingError+Math.Atan((geometry.FrontAxleFromComMeters+geometry.RearAxleFromComMeters)*demand/Math.Max(speed*speed,.25f)));
        var bounded=Math.Clamp(requested,-geometry.MaximumSteeringRadians,geometry.MaximumSteeringRadians);
        var after=state.SteeringRadians+Math.Clamp(bounded-state.SteeringRadians,-geometry.SteeringRateRadiansPerSecond*seconds,geometry.SteeringRateRadiansPerSecond*seconds);
        state.PreparedSteeringRadians=after;var used=(state.SteeringRadians+after)/2;
        var forward=new Vector2(MathF.Cos(v.Heading),MathF.Sin(v.Heading));var left=new Vector2(-forward.Y,forward.X);
        var frontForward=new Vector2(MathF.Cos(v.Heading+used),MathF.Sin(v.Heading+used));var frontLeft=new Vector2(-frontForward.Y,frontForward.X);
        var frontPoint=v.Position+forward*geometry.FrontAxleFromComMeters;var rearPoint=v.Position-forward*geometry.RearAxleFromComMeters;
        // Use the SAME float world-point subtraction as Box2D, including its rounding.
        var frontArm=frontPoint-v.Position;var rearArm=rearPoint-v.Position;
        var vf=TwoContactForces.PointVelocity(v.Velocity,v.AngularVelocity,frontArm);var vr=TwoContactForces.PointVelocity(v.Velocity,v.AngularVelocity,rearArm);
        var uf=Vector2.Dot(vf,frontForward);var wf=Vector2.Dot(vf,frontLeft);var ur=Vector2.Dot(vr,forward);var wr=Vector2.Dot(vr,left);
        var rearBudget=wheel.Parameters.RearBudgetFraction*budget;var frontBudget=budget-rearBudget;
        var fyFront=RearWheelDynamics.FrontLateralForce(wf,frontBudget,SlipCoupledDynamics.VelocityScaleSquared(uf,budget/mass));
        var frontForce=frontLeft*fyFront;
        var resistance=-v.Velocity*(LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed)/MathF.Sqrt(speed*speed+.25f));
        var launch=spec.SpeedMetersPerSecond==0&&v.TotalReferenceMeters<frame.Embedding.Segments[0].StraightLengthMeters&&!road.IsTurn;
        var reference=launch?LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(spec.Profile.Skills,spec.Setup,surfaceAtCom)
            :road.IsTurn?LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(spec.Profile.Skills,spec.Setup,surfaceAtCom)
            :LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(spec.Profile.Skills,spec.Setup,surfaceAtCom);
        var wp=wheel.Parameters;var shaftSpeed=Math.Max(0,wp.WheelRadiusMeters*wheel.AngularVelocityRadPerSecond);
        var available=p.ExperimentalDrive is { } engine?engine.AvailableForce(reference,(float)shaftSpeed,spec.Setup)
            :LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(reference,(float)shaftSpeed,spec.Setup);
        var throttle=v.Activity is RiderActivity.Finished or RiderActivity.Crashed?0:v.Drive*(1-v.Impairment);
        var contact=RearWheelDynamics.Evaluate(ur,wr,wheel.AngularVelocityRadPerSecond,budget,mass,SlipCoupledDynamics.VelocityScaleSquared(ur,budget/mass),
            (p.ExperimentalDrive?.ReferenceForce(reference)??reference)*wp.WheelRadiusMeters*throttle,available*wp.WheelRadiusMeters,available*wp.WheelRadiusMeters*throttle,
            // Front force is an external longitudinal load in the original scalar
            // wheel/contact solve. Omitting it would break the midpoint energy identity.
            Vector2.Dot(resistance+frontForce,forward),seconds,wp);
        wheel.Prepared=contact with{FrontLateralNewtons=0}; // front now has its OWN axes/velocity, recorded separately.
        var rearForce=forward*contact.LongitudinalNewtons+left*contact.RearLateralNewtons;
        var result=new TwoContactForces(frontPoint,rearPoint,frontArm,rearArm,vf,vr,frontForce,rearForce,resistance,forward,frontLeft,left,
            fyFront,contact.RearLateralNewtons,contact.LongitudinalNewtons,budget,requested,state.SteeringRadians,used,after,(float)demand);
        observe?.Invoke(new(time,v,result.NetForce,result.NetTorque,(float)(mass*demand),Vector2.Dot(result.FrontForce+result.RearForce,new(-road.Tangent.Y,road.Tangent.X)),budget,
            Vector2.Dot(result.NetForce,road.Tangent),"two-contact"));
        observeContacts?.Invoke(new(time,v.Id,result));return result;
    }
}
