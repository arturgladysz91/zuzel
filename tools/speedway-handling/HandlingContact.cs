using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record HandlingSettings(double GripScale = 1.25, double YawResponseSeconds = .15,
    double LeanResponseSeconds = .18, double LeanRateRadiansPerSecond = 1.75,
    double MaximumLeanRadians = 1.02)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public H26Configuration? H26 {get;init;}
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RiderPostureH27? PostureH27 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool DoubleContactSolve { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool CoupledRearTyre { get; init; }
}

internal sealed record LeanSample(double Seconds, double RequestRadians, double ActualRadians);
internal sealed record LoadStepH26(double Seconds,double Acceleration,double Residual,int Iterations,
    double FrontNormalNewtons,double RearNormalNewtons)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public ContactPrecision64? Precision { get; init; }
}
internal sealed record HandlingStep(TwoContactForces Forces, double LeanAfter, double LeanRequest)
{
    public LoadStepH26? LoadH26 {get;init;}
    public PostureStepH27? PostureH27 { get; init; }
}

/// <summary>Gameplay surrogate. Roll changes lateral compliance; yaw comes from the two contact arms.</summary>
internal sealed class HandlingRig(HandlingSettings settings)
{
    internal EngineLossH26? EngineLoss=>settings.H26?.EngineLoss;
    internal H26Configuration? Configuration=>settings.H26;
    public List<LoadStepH26> LoadSamples {get;}=[];
    public List<LeanSample> LeanSamples { get; } = [new(0, 0, 0)];
    internal double Lean { get; private set; }
    internal double RiderHeightOffsetMeters { get; private set; }
    public List<TimedPostureH27> PostureSamples { get; } = [];
    private HandlingStep? prepared;

    internal TwoContactForces Prepare(RiderView body, RiderSpec rider, TrackFrame frame, TrackStateSnapshot surface,
        WorldParameters parameters, double time, float mass, float inertia, RearWheelState wheel,
        TwoContactBikeState steering, float dt, float request, CrankClutch144 shaft, double release)
    {
        var road = body.Road;
        var usable = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type, frame.Track.Geometry);
        var grip = surface.SampleSurface(road.Segment, Math.Clamp((float)(road.OffsetMeters / usable) * 4, 0, 4));
        prepared = HandlingContact.Prepare(settings, grip, body, rider, parameters, time, mass, inertia,
            wheel, steering.SteeringRadians, Lean, dt, request, shaft, release, RiderHeightOffsetMeters);
        return prepared.Forces;
    }

    internal void Commit(double seconds)
    {
        var step = prepared ?? throw new InvalidOperationException("Handling step was not prepared.");
        if(step.LoadH26 is {} loads)LoadSamples.Add(loads with {Seconds=seconds});
        if (step.PostureH27 is { } posture)
        {
            RiderHeightOffsetMeters = posture.RiderHeightOffsetAfterMeters;
            PostureSamples.Add(new(seconds, posture));
        }
        Lean = step.LeanAfter;
        LeanSamples.Add(new(seconds, step.LeanRequest, Lean));
    }
}

internal static partial class HandlingContact
{
    private const double FrontSlipRatio = .115;
    private const double RearSlipRatio = .104;
    private const double LateralRelaxationSeconds = .03;
    private const double FrontForceTargetFraction = .98;
    private const double BetaDampingThresholdRadians = .24;
    // Keeps ~0.06 N numerical reserve at the nominal height when bracketing.
    // Reaching the boundary is an explicit unloading diagnostic, never a load clamp.
    private const double ContactDomainAccelerationReserveMetersPerSecondSquared = .001;

    internal static HandlingStep Prepare(HandlingSettings settings, TrackSurfaceState surface, RiderView body, RiderSpec rider,
        WorldParameters parameters, double time, float mass, float inertia, RearWheelState wheel,
        float steering, double lean, float dt, float request, CrankClutch144 shaft, double release,
        double riderHeightOffsetMeters = 0)
    {
        if(settings.H26 is {} explicitH26){
            explicitH26.Validate();
            if(explicitH26.EngineLoss!=shaft.LossH26)
                throw new InvalidOperationException("H26 contact/powertrain configuration mismatch.");
        }
        if (settings.PostureH27 is not null && settings.H26 is not { DynamicLoads: true })
            throw new InvalidOperationException("H27-P requires the explicit H26 dynamic-load branch.");
        if(settings.H26 is not {DynamicLoads:true} h26)
            return AtLoads(settings,surface,body,rider,parameters,time,mass,inertia,wheel,steering,lean,dt,request,shaft,release,null);
        h26.Validate();
        var posture = settings.PostureH27?.Prepare(mass, rider.Profile.Gameplay.Physical.MassKg,
            h26.ComHeightMeters, riderHeightOffsetMeters, body.Drive, dt);
        double height = posture?.CombinedMidpointHeightMeters ?? h26.ComHeightMeters;
        var bike=parameters.TwoContactBike??TwoContactBikeParameters.Default;
        var original=LoadBasedTyreGrip.Budget(mass,surface,bike,parameters.LoadBasedTyres);
        double normal=mass*LoadBasedTyreGrip.GravityMetersPerSecondSquared;
        var forward=new Vector2(MathF.Cos(body.Heading),MathF.Sin(body.Heading));
        int iterations=0;
        (double Residual,HandlingStep Step) At(double ax){
            var split=DynamicAxleLoad.AtAcceleration(ax,bike,height);
            double rear=normal*split.RearFraction;
            if(!split.WithinAssumptions || rear<=0 || rear>=normal)
                throw new InvalidOperationException("H26 wheel unloading: two-contact assumption invalid.");
            var loads=original with {FrontNormalNewtons=normal-rear,RearNormalNewtons=rear};
            var step=AtLoads(settings,surface,body,rider,parameters,time,mass,inertia,wheel,steering,lean,dt,request,shaft,release,loads);
            iterations++;
            return(ax-Vector2.Dot(step.Forces.NetForce,forward)/mass,step);
        }
        // Search stays inside the two-positive-contact domain. A root outside it is rejected, never clamped.
        double lo=-LoadBasedTyreGrip.GravityMetersPerSecondSquared*bike.FrontAxleFromComMeters/height+ContactDomainAccelerationReserveMetersPerSecondSquared;
        double hi= LoadBasedTyreGrip.GravityMetersPerSecondSquared*bike.RearAxleFromComMeters/height-ContactDomainAccelerationReserveMetersPerSecondSquared;
        if (settings.CoupledRearTyre && !settings.DoubleContactSolve)
            throw new InvalidOperationException("Combined rear tyre requires double-precision contact solving.");
        if (settings.DoubleContactSolve)
            return new ContactQuery64(settings, body, rider, bike, original, mass, inertia, height, time, dt,
                steering, request, lean, wheel, shaft, release).Solve(lo,hi,posture);
        var zero=At(0);
        double ax=0;
        var point=zero;
        if(Math.Abs(point.Residual)>h26.AccelerationToleranceMetersPerSecondSquared){
            var a=At(lo);var b=At(hi);
            if(a.Residual>0 || b.Residual<0)
                throw new InvalidOperationException($"H26 wheel unloading: no positive-load coupled root; ax domain [{lo:R},{hi:R}], residuals [{a.Residual:R},{b.Residual:R}].");
            int side=0;
            for(int i=0;i<36;i++){
                ax=(lo*b.Residual-hi*a.Residual)/(b.Residual-a.Residual);
                if(!double.IsFinite(ax)||ax<=lo||ax>=hi)ax=(lo+hi)/2;
                point=At(ax);
                if(Math.Abs(point.Residual)<=h26.AccelerationToleranceMetersPerSecondSquared)break;
                if(point.Residual<0){lo=ax;a=point;if(side<0)b=(b.Residual/2,b.Step);side=-1;}
                else {hi=ax;b=point;if(side>0)a=(a.Residual/2,a.Step);side=1;}
                if(i==35)throw new InvalidOperationException("H26 coupled load solve failed convergence.");
            }
        }
        // At(ax) last prepared the matching shaft/wheel state; only the accepted native step commits it.
        var accepted=point.Step;
        var final=accepted.Forces.TyreLoads!;
        return accepted with {LoadH26=new(time,ax,point.Residual,iterations,final.FrontNormalNewtons,final.RearNormalNewtons),
            PostureH27 = posture};
    }

    internal static HandlingStep AtLoads(HandlingSettings settings, TrackSurfaceState surface, RiderView body, RiderSpec rider,
        WorldParameters parameters, double time, float mass, float inertia, RearWheelState wheel,
        float steering, double lean, float dt, float request, CrankClutch144 shaft, double release,
        LoadBasedTyreBudget? supplied, bool probeCombinedSlip = false)
    {
        var bike = parameters.TwoContactBike ?? TwoContactBikeParameters.Default;
        var baseLoads = supplied ?? LoadBasedTyreGrip.Budget(mass, surface, bike, parameters.LoadBasedTyres);
        var loads = baseLoads with { EffectiveFrictionCoefficient = baseLoads.EffectiveFrictionCoefficient * settings.GripScale };
        float budget = (float)loads.TotalCapacityNewtons;
        double wheelbase = bike.FrontAxleFromComMeters + bike.RearAxleFromComMeters;
        double speed = body.Speed;
        double curvatureRequest = Math.Tan(request) / wheelbase;
        double yawRequest = speed * curvatureRequest;
        double yawBound = 1.27 * budget / mass / Math.Max(4, speed);
        yawRequest = Math.Clamp(yawRequest, -yawBound, yawBound);
        var beta = body.SlideRadians;
        double excessBeta = Math.CopySign(Math.Max(0, Math.Abs(beta) - BetaDampingThresholdRadians), beta);
        yawRequest -= 2.8 * excessBeta;

        double leanRequest = Math.Clamp(Math.Atan2(speed * yawRequest, LoadBasedTyreGrip.GravityMetersPerSecondSquared),
            -settings.MaximumLeanRadians, settings.MaximumLeanRadians);
        double leanAfter = lean + Math.Clamp((leanRequest - lean) * dt / settings.LeanResponseSeconds,
            -settings.LeanRateRadiansPerSecond * dt, settings.LeanRateRadiansPerSecond * dt);
        double leanFraction = Math.Abs((leanAfter + lean) / 2) / settings.MaximumLeanRadians;
        var forward = new Vector2(MathF.Cos(body.Heading), MathF.Sin(body.Heading));
        var left = new Vector2(-forward.Y, forward.X);
        var frontPoint = body.Position + bike.FrontAxleFromComMeters * forward;
        var rearPoint = body.Position - bike.RearAxleFromComMeters * forward;
        var frontArm = frontPoint - body.Position;
        var rearArm = rearPoint - body.Position;
        var frontVelocity = TwoContactForces.PointVelocity(body.Velocity, body.AngularVelocity, frontArm);
        var rearVelocity = TwoContactForces.PointVelocity(body.Velocity, body.AngularVelocity, rearArm);
        float rearForward = Vector2.Dot(rearVelocity, forward);
        float rearSide = Vector2.Dot(rearVelocity, left);
        double rearScale = Scale(rearForward, RearSlipRatio * (1 - .25 * leanFraction), budget / mass);
        double normalizedSide = rearSide / rearScale;
        double share = 1 / Math.Sqrt(1 + normalizedSide * normalizedSide);
        float rearFy = (float)(-loads.RearCapacityNewtons * normalizedSide * share);
        double moment = inertia * (yawRequest - body.AngularVelocity) / settings.YawResponseSeconds;
        double frontYTarget = (moment - TwoContactForces.Cross(rearArm, rearFy * left)) /
            Math.Max(.1, bike.FrontAxleFromComMeters);
        float requestAngle = FrontInverse(frontVelocity, forward, left, loads.FrontCapacityNewtons, frontYTarget,
            FrontSlipRatio * (1 - .13 * leanFraction), budget / mass, request, bike.MaximumSteeringRadians);
        float steerAfter = steering + Math.Clamp(requestAngle - steering,
            -bike.SteeringRateRadiansPerSecond * dt, bike.SteeringRateRadiansPerSecond * dt);
        float steerUsed = (steering + steerAfter) / 2;
        var frontForward = new Vector2(MathF.Cos(body.Heading + steerUsed), MathF.Sin(body.Heading + steerUsed));
        var frontLeft = new Vector2(-frontForward.Y, frontForward.X);
        double frontScale = Scale(Vector2.Dot(frontVelocity, frontForward),
            FrontSlipRatio * (1 - .13 * leanFraction), budget / mass);
        float frontFy = RearWheelDynamics.FrontLateralForce(Vector2.Dot(frontVelocity, frontLeft),
            loads.FrontCapacityNewtons, frontScale * frontScale);
        var frontForce = frontFy * frontLeft;
        var resistance = -body.Velocity * (LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(body.Speed) /
            MathF.Sqrt(body.Speed * body.Speed + .25f));
        double external = Vector2.Dot(frontForce + resistance, forward);
        var engine = Engine500Parameters.Default;
        double ratio = engine.ShortOverallRatio + (engine.LongOverallRatio - engine.ShortOverallRatio) * rider.Setup.Gearing;
        // Reserve the lateral component first. The remaining circle sets Fx;
        // the relaxation product is preserved when the budget is reduced.
        var material = wheel.Parameters with
        {
            MinimumContactRelaxationSeconds = wheel.Parameters.MinimumContactRelaxationSeconds / share
        };
        var command = shaft.Prepare(body.Drive * (1 - body.Impairment), ratio, engine.Efficiency, dt,
            time >= release - 1e-10, torque =>
            {
                // A frozen-state diagnostic keeps the already prepared front control and changes
                // only the rear law. The predictor and native caller retain lateral-first.
                if (probeCombinedSlip)
                    return RearWheelDynamics.Evaluate(rearForward, rearSide, wheel.AngularVelocityRadPerSecond,
                        budget, mass, rearScale * rearScale, torque, Math.Abs(torque), torque, external, dt,
                        wheel.Parameters, rearBudgetNewtons: loads.RearCapacityNewtons);
                var result = RearWheelDynamics.Evaluate(rearForward, 0, wheel.AngularVelocityRadPerSecond, budget, mass,
                    rearScale * rearScale, torque, Math.Abs(torque), torque, external, dt, material,
                    rearBudgetNewtons: loads.RearCapacityNewtons * share);
                double use = Math.Sqrt(result.LongitudinalNewtons * (double)result.LongitudinalNewtons +
                    rearFy * (double)rearFy) / loads.RearCapacityNewtons;
                return result with { RearLateralNewtons = rearFy, RearUtilization = use };
            },wheel.Parameters);
        var rear = shaft.Prepared.Wheel;
        if (probeCombinedSlip)
            rearFy = rear.RearLateralNewtons;
        var rearForce = rear.LongitudinalNewtons * forward + rearFy * left;
        var forces = new TwoContactForces(frontPoint, rearPoint, frontArm, rearArm, frontVelocity, rearVelocity,
            frontForce, rearForce, resistance, forward, frontLeft, left, frontFy, rearFy, rear.LongitudinalNewtons,
            budget, requestAngle, steering, steerUsed, steerAfter, (float)(speed * yawRequest))
            { TyreLoads = loads, Engine = command };
        return new(forces, leanAfter, leanRequest);
    }

    internal static double Scale(double forward, double slipRatio, double budgetAcceleration) =>
        Math.Sqrt(slipRatio * slipRatio * (forward * forward + .25) +
            Math.Pow(budgetAcceleration * LateralRelaxationSeconds, 2));

    private static float FrontInverse(Vector2 velocity, Vector2 forward, Vector2 left, double capacity,
        double bodyTarget, double ratio, double budgetAcceleration, float initial, float limit)
    {
        double speed = velocity.Length();
        if (speed < .5) return Math.Clamp(initial, -limit, limit);
        double velocityAngle = Math.Atan2(Vector2.Dot(velocity, left), Vector2.Dot(velocity, forward));
        double steer = initial;
        for (int i = 0; i < 3; i++)
        {
            double utilization = Math.Clamp(bodyTarget / (capacity * Math.Cos(steer)),
                -FrontForceTargetFraction, FrontForceTargetFraction);
            double slip = Scale(speed, ratio, budgetAcceleration) * utilization / Math.Sqrt(1 - utilization * utilization);
            steer = Math.Clamp(velocityAngle + Math.Asin(Math.Clamp(slip / speed, -.98, .98)), -limit, limit);
        }
        return (float)steer;
    }
}

/// <summary>Quasi-static vertical rider motion for the flat contact diagnostic.</summary>
internal sealed record RiderPostureH27(double BikeHeightMeters = .45, double RiderTravelMeters = .14,
    double RiderSpeedMetersPerSecond = .50, double ResponseSeconds = .15)
{
    internal PostureStepH27 Prepare(double massKg, double riderKg, double neutralHeightMeters,
        double initialOffsetMeters, double activeDrive, double seconds)
    {
        if (!double.IsFinite(BikeHeightMeters) || BikeHeightMeters <= 0 ||
            !double.IsFinite(RiderTravelMeters) || RiderTravelMeters < 0 ||
            !double.IsFinite(RiderSpeedMetersPerSecond) || RiderSpeedMetersPerSecond <= 0 ||
            !double.IsFinite(ResponseSeconds) || ResponseSeconds <= 0 ||
            !double.IsFinite(massKg) || !double.IsFinite(riderKg) || riderKg <= 0 || massKg <= riderKg ||
            !double.IsFinite(initialOffsetMeters) || initialOffsetMeters < -RiderTravelMeters - 1e-12 ||
            initialOffsetMeters > 1e-12 || !double.IsFinite(activeDrive) || activeDrive is < 0 or > 1 ||
            !double.IsFinite(seconds) || seconds < 0)
            throw new ArgumentException("Finite bounded H27-P posture state required.");

        double bikeKg = massKg - riderKg;
        double riderNeutral = (massKg * neutralHeightMeters - bikeKg * BikeHeightMeters) / riderKg;
        if (riderNeutral <= RiderTravelMeters)
            throw new ArgumentException("Candidate rider COM must stay above ground.");
        double target = -RiderTravelMeters * activeDrive;
        double distance = Math.Abs(target - initialOffsetMeters);
        double linearSeconds = Math.Max(0, distance / RiderSpeedMetersPerSecond - ResponseSeconds);
        double At(double t)
        {
            double linear = Math.Min(t, linearSeconds);
            double remaining = (distance - RiderSpeedMetersPerSecond * linear) *
                Math.Exp(-(t - linear) / ResponseSeconds);
            return target - Math.CopySign(remaining, target - initialOffsetMeters);
        }
        double middle = At(seconds / 2);
        double after = At(seconds);
        return new(bikeKg, riderKg, riderNeutral, riderNeutral + target, riderNeutral + middle,
            riderNeutral + after, after, (bikeKg * BikeHeightMeters + riderKg * (riderNeutral + middle)) / massKg);
    }
}

internal sealed record PostureStepH27(double BikeKg, double RiderKg, double RiderNeutralHeightMeters,
    double RiderTargetHeightMeters, double RiderMidpointHeightMeters, double RiderHeightAfterMeters,
    double RiderHeightOffsetAfterMeters, double CombinedMidpointHeightMeters);

internal sealed record TimedPostureH27(double Seconds, PostureStepH27 Posture);
