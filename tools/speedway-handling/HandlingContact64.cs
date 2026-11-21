using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record ContactPrecision64(string Status, double AppliedResidual,
    double ForceRoundingNewtons, double MomentRoundingNewtonMeters, double AppliedRearLongitudinalNewtons,
    double WheelImpulseResidual, double ClutchResidualBeforeRounding, long ForceTicks, long SolverTicks);

internal static partial class HandlingContact
{
    internal readonly record struct Vec64(double X, double Y)
    {
        internal Vec64(Vector2 v) : this(v.X,v.Y) { }
        internal Vector2 ToBox() => new((float)X,(float)Y);
        internal Vec64 Left => new(-Y,X);
        internal double Norm => Math.Sqrt(Dot(this,this));
        internal static double Dot(Vec64 a, Vec64 b) => a.X*b.X+a.Y*b.Y;
        internal static double Cross(Vec64 a, Vec64 b) => a.X*b.Y-a.Y*b.X;
        public static Vec64 operator +(Vec64 a,Vec64 b) => new(a.X+b.X,a.Y+b.Y);
        public static Vec64 operator -(Vec64 a,Vec64 b) => new(a.X-b.X,a.Y-b.Y);
        public static Vec64 operator *(Vec64 a,double v) => new(a.X*v,a.Y*v);
        internal static Vec64 Axis(double angle) => new(Math.Cos(angle),Math.Sin(angle));
    }

    internal readonly record struct Trial64(LoadBasedTyreBudget Loads, Vec64 FrontForce, Vec64 RearForce,
        double FrontFy, double RearFy, double TargetSteering, double AfterSteering, double MidSteering, Vec64 FrontLeft,
        ClutchTrial64 Power);

    internal sealed class ContactQuery64
    {
        private readonly Vec64 position, forward, left, frontArm, rearArm, frontVelocity, rearVelocity, resistance;
        private readonly LoadBasedTyreBudget original;
        private readonly RearWheelParameters wheelParameters;
        private readonly CrankClutch144 shaft;
        private readonly double mass, height, frontAxle, rearAxle, normal, requestedYawMoment, leanRequest, leanAfter,
            steeringBefore, steeringRequest, maxSteering, steeringTravel, heading, frontSlipRatio, time, dt,
            total, rearForward, rearNormalizedSide, rearShare, relaxationSeconds, ratio, efficiency, throttle;
        private readonly bool released;
        private readonly double tolerance;
        private long forceTicks;
        internal int Evaluations { get; private set; }

        internal ContactQuery64(HandlingSettings settings, RiderView body, RiderSpec rider,
            TwoContactBikeParameters bike, LoadBasedTyreBudget original, double mass, double inertia, double height,
            double time, double dt, double steering, double request, double lean, RearWheelState wheel,
            CrankClutch144 shaft, double release)
        {
            this.original=original with {EffectiveFrictionCoefficient=original.EffectiveFrictionCoefficient*settings.GripScale};
            this.mass=mass;
            this.height=height;
            this.time=time;
            this.dt=dt;
            this.shaft=shaft;
            tolerance=settings.H26!.AccelerationToleranceMetersPerSecondSquared;
            WheelOmega=wheel.AngularVelocityRadPerSecond;
            wheelParameters=wheel.Parameters;
            frontAxle=bike.FrontAxleFromComMeters;
            rearAxle=bike.RearAxleFromComMeters;
            normal=mass*LoadBasedTyreGrip.GravityMetersPerSecondSquared;
            total=this.original.EffectiveFrictionCoefficient*normal;
            steeringBefore=steering;
            steeringRequest=request;
            maxSteering=bike.MaximumSteeringRadians;
            steeringTravel=bike.SteeringRateRadiansPerSecond*dt;
            heading=body.Heading;
            position=new(body.Position);
            var velocity=new Vec64(body.Velocity);
            double speed=velocity.Norm;
            double yawRequest=speed*Math.Tan(request)/(frontAxle+rearAxle);
            double yawLimit=1.27*total/mass/Math.Max(4,speed);
            double beta=speed<.01f?0:BikeAngles.Wrap(heading-Math.Atan2(velocity.Y,velocity.X));
            yawRequest=Math.Clamp(yawRequest,-yawLimit,yawLimit)-2.8*Math.CopySign(
                Math.Max(0,Math.Abs(beta)-BetaDampingThresholdRadians),beta);
            YawDemand=speed*yawRequest;
            requestedYawMoment=inertia*(yawRequest-body.AngularVelocity)/settings.YawResponseSeconds;
            leanRequest=Math.Clamp(Math.Atan2(YawDemand,LoadBasedTyreGrip.GravityMetersPerSecondSquared),
                -settings.MaximumLeanRadians,settings.MaximumLeanRadians);
            leanAfter=lean+Math.Clamp((leanRequest-lean)*dt/settings.LeanResponseSeconds,
                -settings.LeanRateRadiansPerSecond*dt,settings.LeanRateRadiansPerSecond*dt);
            double leanFraction=Math.Abs((leanAfter+lean)/2)/settings.MaximumLeanRadians;
            frontSlipRatio=FrontSlipRatio*(1-.13*leanFraction);
            forward=Vec64.Axis(heading);
            left=forward.Left;
            frontArm=forward*frontAxle;
            rearArm=forward*(-rearAxle);
            frontVelocity=velocity+frontArm.Left*body.AngularVelocity;
            rearVelocity=velocity+rearArm.Left*body.AngularVelocity;
            rearForward=Vec64.Dot(rearVelocity,forward);
            rearNormalizedSide=Vec64.Dot(rearVelocity,left)/Scale(rearForward,RearSlipRatio*(1-.25*leanFraction),total/mass);
            rearShare=1/Math.Sqrt(1+rearNormalizedSide*rearNormalizedSide);
            relaxationSeconds=wheelParameters.MinimumContactRelaxationSeconds/rearShare;
            resistance=velocity*(-(LongitudinalDynamics.ProvisionalBaseResistanceForceNewtons+
                LongitudinalDynamics.ProvisionalQuadraticResistanceCoefficient*speed*speed)/Math.Sqrt(speed*speed+.25));
            var engine=Engine500Parameters.Default;
            ratio=engine.ShortOverallRatio+(engine.LongOverallRatio-engine.ShortOverallRatio)*rider.Setup.Gearing;
            efficiency=engine.Efficiency;
            throttle=(double)body.Drive*(1d-body.Impairment);
            released=time>=release-1e-10;
            if (!double.IsFinite(speed) || !double.IsFinite(normal) || normal<=0 ||
                !double.IsFinite(total) || total<=0 || !double.IsFinite(steering) || !double.IsFinite(request) ||
                !double.IsFinite(height) || height<=0 || !double.IsFinite(dt) || dt is <0 or >.020001 ||
                !double.IsFinite(requestedYawMoment) || !double.IsFinite(throttle) || throttle is <0 or >1)
                throw new ContactRootException(ContactRootStatus.InvalidPhysicalState,"nonfinite or out-of-range contact input");
        }

        private double WheelOmega { get; }
        private double YawDemand { get; }

        private double RearNormal(double ax) =>
            normal*(frontAxle/(frontAxle+rearAxle)+ax*height/(LoadBasedTyreGrip.GravityMetersPerSecondSquared*(frontAxle+rearAxle)));

        private ClutchTrial64 Power(double rearBudget, double external) =>
            shaft.Evaluate64(throttle,ratio,efficiency,dt,released, torque => RearWheelDynamics.EvaluateLongitudinal64(
                rearForward,WheelOmega,mass,rearBudget,torque,external,dt,wheelParameters,relaxationSeconds));

        internal ContactResidual64<Trial64> Evaluate(double ax)
        {
            long start=Stopwatch.GetTimestamp();
            Evaluations++;
            double rearNormal=RearNormal(ax);
            if (rearNormal<=0 || rearNormal>=normal)
                throw new ContactRootException(ContactRootStatus.InvalidPhysicalState,"outside positive contact loads");
            var loads=original with {FrontNormalNewtons=normal-rearNormal,RearNormalNewtons=rearNormal};
            double rearFy=-loads.RearCapacityNewtons*rearNormalizedSide*rearShare;
            double target=(requestedYawMoment-Vec64.Cross(rearArm,left*rearFy))/Math.Max(.1,frontAxle);
            double steer=Inverse(loads.FrontCapacityNewtons,target);
            double after=steeringBefore+Math.Clamp(steer-steeringBefore,-steeringTravel,steeringTravel);
            double mid=(steeringBefore+after)/2;
            var frontForward=Vec64.Axis(heading+mid);
            var frontLeft=frontForward.Left;
            double scale=Scale(Vec64.Dot(frontVelocity,frontForward),frontSlipRatio,total/mass);
            double side=Vec64.Dot(frontVelocity,frontLeft);
            double frontFy=-loads.FrontCapacityNewtons*side/Math.Sqrt(side*side+scale*scale);
            var frontForce=frontLeft*frontFy;
            var power=Power(loads.RearCapacityNewtons*rearShare,Vec64.Dot(frontForce+resistance,forward));
            var rearForce=forward*power.Wheel.Force+left*rearFy;
            double residual=ax-Vec64.Dot(frontForce+rearForce+resistance,forward)/mass;
            forceTicks+=Stopwatch.GetTimestamp()-start;
            return new(ax,residual,new(loads,frontForce,rearForce,frontFy,rearFy,steer,after,mid,frontLeft,power));
        }

        private double Inverse(double capacity,double target)
        {
            double speed=frontVelocity.Norm;
            if (speed<.5) return Math.Clamp(steeringRequest,-maxSteering,maxSteering);
            double velocityAngle=Math.Atan2(Vec64.Dot(frontVelocity,left),Vec64.Dot(frontVelocity,forward));
            double scale=Scale(speed,frontSlipRatio,total/mass);
            double steer=steeringRequest;
            for (int i=0;i<3;i++)
            {
                double utilization=Math.Clamp(target/(capacity*Math.Cos(steer)),-FrontForceTargetFraction,FrontForceTargetFraction);
                double slip=scale*utilization/Math.Sqrt(1-utilization*utilization);
                steer=Math.Clamp(velocityAngle+Math.Asin(Math.Clamp(slip/speed,-.98,.98)),-maxSteering,maxSteering);
            }
            return steer;
        }

        // For fixed Fx the passive clutch has increasing input torque as Fx rises, while
        // the resulting mid-wheel speed decreases. Thus mid slip decreases with Fx and
        // external forward force, and the total longitudinal force increases with external
        // force. At fixed signed slip, |Fx| increases with the rear normal capacity even
        // with load-dependent relaxation. The sign is independent of capacity at fixed
        // external force (test the coupled solution at Fx=0). These facts bound each box
        // by its two capacity endpoints and the monotone external-force endpoints.
        private bool ExcludesRoots(double lo,double hi,int sign)
        {
            const int maximumBoxes=4096;
            var boxes=new Stack<(double Low,double High)>();
            boxes.Push((lo,hi));
            double maxMidSteer=Math.Min(Math.PI/2,Math.Max(Math.Abs(steeringBefore-steeringTravel/2),
                Math.Abs(steeringBefore+steeringTravel/2)));
            double extCenter=Vec64.Dot(resistance,forward);
            for (int i=0;i<maximumBoxes&&boxes.TryPop(out var box);i++)
            {
                double rearLo=RearNormal(box.Low), rearHi=RearNormal(box.High);
                double frontBound=original.EffectiveFrictionCoefficient*(normal-rearLo)*Math.Sin(maxMidSteer);
                double external=extCenter+sign*frontBound;
                long started=Stopwatch.GetTimestamp();
                double fLo=Power(rearLo*original.EffectiveFrictionCoefficient*rearShare,external).Wheel.Force;
                double fHi=Power(rearHi*original.EffectiveFrictionCoefficient*rearShare,external).Wheel.Force;
                forceTicks+=Stopwatch.GetTimestamp()-started;
                double bound=(external+(sign>0?Math.Max(fLo,fHi):Math.Min(fLo,fHi)))/mass;
                if (sign>0 ? box.Low-bound>tolerance : box.High-bound < -tolerance)
                    continue;
                double mid=box.Low+(box.High-box.Low)/2;
                if (box.High-box.Low < tolerance) return false;
                boxes.Push((box.Low,mid));
                boxes.Push((mid,box.High));
            }
            return boxes.Count==0;
        }

        internal HandlingStep Solve(double lo,double hi,PostureStepH27? posture,bool bisect=false)
        {
            long started=Stopwatch.GetTimestamp();
            var point=ContactLoadSolver64.Solve(lo,hi,tolerance,Evaluate,sign=>ExcludesRoots(lo,hi,sign),bisect);
            if (Math.Abs(BoundaryResidual(point))>tolerance)
            {
                point=ContactLoadSolver64.Solve(lo,hi,tolerance/2,Evaluate,sign=>ExcludesRoots(lo,hi,sign),bisect);
                if (Math.Abs(BoundaryResidual(point))>tolerance)
                    throw new ContactRootException(ContactRootStatus.NumericalDiscontinuity,"Box2D boundary residual exceeds tolerance");
            }
            var result=Apply(point,posture);
            var loads=result.LoadH26!;
            return result with {LoadH26=loads with {Precision=loads.Precision! with
                {ForceTicks=forceTicks,SolverTicks=Stopwatch.GetTimestamp()-started}}};
        }

        private double BoundaryResidual(ContactResidual64<Trial64> point) => point.Ax-
            Vec64.Dot(new Vec64(point.Value.FrontForce.ToBox())+new Vec64(point.Value.RearForce.ToBox())+
                new Vec64(resistance.ToBox()),forward)/mass;

        private HandlingStep Apply(ContactResidual64<Trial64> point,PostureStepH27? posture)
        {
            var t=point.Value;
            Vector2 frontPoint=(position+frontArm).ToBox(), rearPoint=(position+rearArm).ToBox();
            Vector2 frontApplied=t.FrontForce.ToBox(), rearApplied=t.RearForce.ToBox(), dragApplied=resistance.ToBox();
            var net=new Vec64(frontApplied)+new Vec64(rearApplied)+new Vec64(dragApplied);
            double appliedAx=Vec64.Dot(net,forward)/mass;
            double appliedFx=Vec64.Dot(new(rearApplied),forward), appliedFy=Vec64.Dot(new(rearApplied),left);
            double external=Vec64.Dot(new Vec64(frontApplied)+new Vec64(dragApplied),forward);
            double torque=t.Power.Torque*ratio*(t.Power.Torque>=0?efficiency:1/efficiency);
            var wheel=RearWheelDynamics.AcceptLongitudinal64(rearForward,WheelOmega,mass,t.Loads.RearCapacityNewtons,
                torque,external,appliedFx,appliedFy,dt,wheelParameters,t.Power.Wheel);
            var engine=shaft.Accept64(throttle,ratio,efficiency,dt,t.Power,wheel,wheelParameters);
            var forces=new TwoContactForces(frontPoint,rearPoint,frontPoint-position.ToBox(),rearPoint-position.ToBox(),
                frontVelocity.ToBox(),rearVelocity.ToBox(),frontApplied,rearApplied,dragApplied,forward.ToBox(),
                t.FrontLeft.ToBox(),left.ToBox(),(float)t.FrontFy,(float)appliedFy,(float)appliedFx,(float)total,
                (float)t.TargetSteering,(float)steeringBefore,(float)t.MidSteering,(float)t.AfterSteering,(float)YawDemand)
                {TyreLoads=t.Loads,Engine=engine,SteeringTargetBeforeRounding=t.TargetSteering};
            double actualMoment=Vec64.Cross(new Vec64(frontPoint)-position,new(frontApplied))+
                Vec64.Cross(new Vec64(rearPoint)-position,new(rearApplied));
            double modeledMoment=Vec64.Cross(frontArm,t.FrontForce)+Vec64.Cross(rearArm,t.RearForce);
            double wheelResidual=wheelParameters.WheelInertiaKgMetersSquared*(wheel.AfterOmega-WheelOmega)-
                dt*(torque-wheelParameters.WheelRadiusMeters*appliedFx-
                    wheelParameters.BearingResistanceNewtonMeterSeconds*wheel.MidOmega);
            return new(forces,leanAfter,leanRequest)
            {
                PostureH27=posture,
                LoadH26=new(time,point.Ax,point.Residual,Evaluations,t.Loads.FrontNormalNewtons,t.Loads.RearNormalNewtons)
                {
                    Precision=new(nameof(ContactRootStatus.Converged),point.Ax-appliedAx,
                        (net-(t.FrontForce+t.RearForce+resistance)).Norm,actualMoment-modeledMoment,appliedFx,
                        wheelResidual,t.Power.Residual,0,0)
                }
            };
        }
    }
}
