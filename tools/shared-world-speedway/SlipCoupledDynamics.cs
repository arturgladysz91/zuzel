using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL effective planar tyre. No wheelspin state, speed correction or second integrator.</summary>
public static class SlipCoupledDynamics
{
    // Slope/regularization, not a preferred slide angle or an empirical calibration.
    public const float SlipStiffnessRatio=.12f;
    public const float LowSpeedMetersPerSecond=.5f;
    public const float MaximumSteeringDemandRatio=.95f;
    public const float YawLeverArmMeters=.5f;
    public const float MaximumForceHoldSeconds=.02f;
    public const float OuterToYawResponseSeparation=4;
    // Bound low-speed stiffness by m/T. T exceeds the largest existing 20 ms
    // force-hold interval, so lateral damping cannot overshoot a stopped side velocity.
    public const float MinimumLateralRelaxationSeconds=.03f;
    public readonly record struct TyreForces(float DriveNewtons,float LateralNewtons,float Utilization,float LateralLossWatts);
    public static TyreForces Tyre(RiderView v,float requestedDriveNewtons,float budgetNewtons,float massKg)
    {
        if(!float.IsFinite(budgetNewtons)||budgetNewtons<=0||!float.IsFinite(requestedDriveNewtons)||requestedDriveNewtons<0
            ||!float.IsFinite(massKg)||massKg<=0||!float.IsFinite(v.ForwardSpeed)||!float.IsFinite(v.SideSpeed))throw new ArgumentOutOfRangeException(nameof(budgetNewtons));
        return Tyre(v.SideSpeed,requestedDriveNewtons,budgetNewtons,VelocityScaleSquared(v.ForwardSpeed,budgetNewtons/massKg));
    }
    private static float VelocityScaleSquared(float u,float accelerationBudget)
        =>SlipStiffnessRatio*SlipStiffnessRatio*(u*u+LowSpeedMetersPerSecond*LowSpeedMetersPerSecond)
            +accelerationBudget*accelerationBudget*MinimumLateralRelaxationSeconds*MinimumLateralRelaxationSeconds;
    private static TyreForces Tyre(float w,float requestedDriveNewtons,float budgetNewtons,float scale)
    {
        var lateral=-budgetNewtons*w/MathF.Sqrt(w*w+scale);
        var longitudinal=Math.Min(Math.Max(0,requestedDriveNewtons),MathF.Sqrt(Math.Max(0,budgetNewtons*budgetNewtons-lateral*lateral)));
        return new(longitudinal,lateral,MathF.Sqrt(longitudinal*longitudinal+lateral*lateral)/budgetNewtons,-lateral*w);
    }
    public static (Vector2 Force,float Torque) Forces(RiderView v,RiderSpec spec,TrackFrame frame,
        TrackStateSnapshot surface,WorldParameters p,double time,float mass,float inertia,
        Action<ForceObservation>? observe=null,Action<SlipObservation>? observeSlip=null,
        Action<YawLimitObservation>? observeYaw=null)
    {
        if(spec.Passive || spec.SpeedMetersPerSecond==0 && v.TotalReferenceMeters<frame.Embedding.Segments[0].StraightLengthMeters
            && time+1e-12<RiderController.TapeReactionSeconds(spec))return(Vector2.Zero,0);
        var road=v.Road;var speed=v.Speed;var tangent=road.Tangent;
        if(v.Activity==RiderActivity.Crashed) {
            var travel=speed>.01f?v.Velocity/speed:tangent;
            return(-travel*Math.Min(mass*speed/.1f,LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed)),-inertia*v.AngularVelocity);
        }
        var grip=surface.SampleSurface(road.Segment,Math.Clamp((float)road.OffsetMeters/LaneModel.UsableRacingWidthMeters(
            frame.Track.Segments[road.Segment].Type,frame.Track.Geometry)*4,0,4));
        var radius=frame.Track.Geometry.InnerRadiusMeters+Math.Max(0,(float)road.OffsetMeters);
        var safe=SegmentPhysics.MaxSafeTurnSpeedForRadius(radius,grip,spec.Profile.Skills,spec.Setup);
        var budget=mass*safe*safe/radius; // reuse the existing capability ONCE, as a force budget
        var launch=spec.SpeedMetersPerSecond==0 && v.TotalReferenceMeters<frame.Embedding.Segments[0].StraightLengthMeters && !road.IsTurn;
        var reference=launch?LongitudinalDynamics.CalculateStandingStartAvailableDriveForceNewtons(spec.Profile.Skills,spec.Setup,grip)
            :road.IsTurn?LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(spec.Profile.Skills,spec.Setup,grip)
            :LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(spec.Profile.Skills,spec.Setup,grip);
        var u=v.ForwardSpeed;var w=v.SideSpeed;
        var available=LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(reference,Math.Max(0,u),spec.Setup);
        var scaleSquared=VelocityScaleSquared(u,budget/mass);
        var tyre=Tyre(w,available*v.Drive*(1-v.Impairment),budget,scaleSquared);
        var forward=new Vector2(MathF.Cos(v.Heading),MathF.Sin(v.Heading));var side=new Vector2(-forward.Y,forward.X);
        var tyreForce=forward*tyre.DriveNewtons+side*tyre.LateralNewtons;
        // External rolling/aerodynamic resistance is separate from the tyre circle.
        // Regularization is continuous at rest and cannot reverse a stopped body.
        var resistance=-v.Velocity*(LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed)
            /MathF.Sqrt(speed*speed+LowSpeedMetersPerSecond*LowSpeedMetersPerSecond));
        var force=tyreForce+resistance;
        // Static-geometry preview over two yaw response times; no hypothetical body or force solve.
        var ahead=Math.Max(.1,frame.ReferenceSpeed(road,speed)*2*p.YawResponseSeconds);
        var preview=frame.RoadAt(v.TotalReferenceMeters+ahead,v.TargetOffsetMeters);
        var angle=BikeAngles.Wrap(preview.Heading-road.Heading);
        var curvature=angle/(ahead+Math.Abs(angle)*Math.Max(0,road.OffsetMeters));
        var longitudinal=Vector2.Dot(v.Velocity,tangent);
        // A lateral force now has a yaw actuator in front of it. The outer loop
        // must be slower than that actuator; the old direct-force PD is unstable here.
        var response=p.LateralResponseSeconds+OuterToYawResponseSeparation*p.SlipYawResponseSeconds;
        var demand=longitudinal*longitudinal*curvature+((float)road.OffsetMeters-v.TargetOffsetMeters)
            /(response*response)+2*Vector2.Dot(v.Velocity,road.Outward)/response;
        var ratio=Math.Clamp(mass*demand/budget,-MaximumSteeringDemandRatio,MaximumSteeringDemandRatio);
        var targetSlip=(float)Math.Atan(Math.Sqrt(scaleSquared)
            /Math.Max(speed,LowSpeedMetersPerSecond)*ratio/Math.Sqrt(1-ratio*ratio));
        var travelHeading=speed>=.01f?MathF.Atan2(v.Velocity.Y,v.Velocity.X):(float)road.Heading;
        var error=(float)BikeAngles.Wrap(targetSlip-v.SlideRadians);
        if(speed<LowSpeedMetersPerSecond) {
            // RiderView deliberately stops defining travel slip below .01 m/s.
            // Fade that direction into the road heading with zero slope at the threshold.
            var blend=Math.Clamp((speed-.01f)/(LowSpeedMetersPerSecond-.01f),0,1);blend=blend*blend*(3-2*blend);
            var roadError=BikeAngles.Wrap(road.Heading-v.Heading);
            error=(float)BikeAngles.Wrap(roadError+blend*BikeAngles.Wrap(error-roadError));
        }
        // Actual lateral reaction, not a requested trajectory. Keeping the motor
        // out of this feed-forward keeps the yaw actuator independent of throttle.
        var lateralForce=side*tyre.LateralNewtons;
        var travelYaw=(v.Velocity.X*lateralForce.Y-v.Velocity.Y*lateralForce.X)/(mass*(speed*speed+LowSpeedMetersPerSecond*LowSpeedMetersPerSecond));
        var authority=(.75f+.25f*(spec.Profile.Gameplay.Abilities.Technique-1)/98f)*(1-v.Impairment);
        var yawAcceleration=error/(p.SlipYawResponseSeconds*p.SlipYawResponseSeconds)+2*(travelYaw-v.AngularVelocity)/p.SlipYawResponseSeconds;
        var torque=Math.Clamp(inertia*yawAcceleration,-budget*YawLeverArmMeters,budget*YawLeverArmMeters)*authority;
        var resistanceLoss=-Vector2.Dot(resistance,v.Velocity);
        // Bound WORK over the complete held-force interval, including the quadratic
        // change of kinetic energy. Instantaneous tau*omega alone lets a braking
        // moment overshoot zero spin and create rotational energy in a finite step.
        var passiveForce=lateralForce+resistance;
        var credit=Math.Max(0,(double)tyre.LateralLossWatts+resistanceLoss
            -passiveForce.LengthSquared()*MaximumForceHoldSeconds/(2*mass));
        var rate=(double)inertia/MaximumForceHoldSeconds;
        var root=Math.Sqrt((double)v.AngularVelocity*v.AngularVelocity+2*credit/rate);
        torque=Math.Clamp(torque,(float)(rate*(-v.AngularVelocity-root)),(float)(rate*(-v.AngularVelocity+root)));
        // Re-evaluate diagnostic-only stages inside the optional branch. Keep the
        // original force/torque arithmetic and disabled path unchanged.
        if(observeYaw is { } yaw) yaw(new(time,v.Id,inertia*yawAcceleration,
            Math.Clamp(inertia*yawAcceleration,-budget*YawLeverArmMeters,budget*YawLeverArmMeters)*authority,
            torque,error,v.AngularVelocity,inertia,credit));
        observe?.Invoke(new(time,v,force,torque,(float)(mass*demand),Vector2.Dot(tyreForce,new(-tangent.Y,tangent.X)),
            null,Vector2.Dot(force,tangent),"slip-racing"));
        observeSlip?.Invoke(new(time,v.Id,speed,v.SlideRadians*180/MathF.PI,targetSlip*180/MathF.PI,v.Heading,travelHeading,
            tyre.DriveNewtons,tyre.LateralNewtons,budget,tyre.Utilization,v.Drive,road.OffsetMeters,v.TargetOffsetMeters,torque,
            tyre.LateralLossWatts,resistanceLoss,Vector2.Dot(force,v.Velocity),torque*v.AngularVelocity));
        return(force,torque);
    }
}
