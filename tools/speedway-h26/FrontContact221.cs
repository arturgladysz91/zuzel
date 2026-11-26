namespace SpeedwayHandling;

internal readonly record struct FrontResponse221(double RotorTorqueNm, double RelativeSpinRadiansPerSecond,
    double RollingSlipMetersPerSecond, double SteeringReactionWatts, double StrainRateMetersPerSecond,
    double ContactLossWatts, double RelaxationLossWatts);

/// <summary>Independent research variants. Zero parameters select the original contact.</summary>
internal readonly record struct FrontContact221
{
    internal FrontContact221(double PolarInertiaKgM2 = 0, double RelaxationMeters = 0, bool SteadyLateral = false)
    {
        if (!double.IsFinite(PolarInertiaKgM2) || PolarInertiaKgM2 < 0)
            throw new ArgumentOutOfRangeException(nameof(PolarInertiaKgM2));
        if (!double.IsFinite(RelaxationMeters) || RelaxationMeters < 0)
            throw new ArgumentOutOfRangeException(nameof(RelaxationMeters));
        if (PolarInertiaKgM2 > 0 && RelaxationMeters > 0)
            throw new ArgumentException("Validate the front rotor and front tread memory independently.");
        this.PolarInertiaKgM2 = PolarInertiaKgM2;
        if (SteadyLateral && RelaxationMeters <= 0)
            throw new ArgumentException("The steady comparison needs the paired relaxation parameter.");
        this.RelaxationMeters = RelaxationMeters;
        this.SteadyLateral = SteadyLateral;
    }

    public double PolarInertiaKgM2 { get; }
    public double RelaxationMeters { get; }
    public bool SteadyLateral { get; }
    internal bool HasLateralMemory => RelaxationMeters > 0 && !SteadyLateral;
    private const double LongitudinalRegularizationMetersPerSecond = .65;
    private const double LowSpeedTransportMetersPerSecond = .75;
    private const double ReferencePaceMetersPerSecond = 20;
    private const double ReferenceLateralSlopeNewtonSecondsPerMeter = 750;
    internal bool Enabled => PolarInertiaKgM2 > 0 || RelaxationMeters > 0;
    internal double ShearStiffness => ReferencePaceMetersPerSecond * ReferenceLateralSlopeNewtonSecondsPerMeter /
        RelaxationMeters;

    internal Tyre215 Evaluate(State215 state, Kinematics215 motion, Patch215 patch, double normal, double normalLoss,
        double capacity, double sideScale, double steerRate, out FrontResponse221 response)
    {
        double side = patch.Velocity.Dot(patch.Side);
        if (PolarInertiaKgM2 > 0)
        {
            double relativeSpin = state.FrontWheel / PolarInertiaKgM2 - motion.WorldRate.Dot(patch.Axle);
            Vec215 slip = patch.Velocity + patch.Axle.Cross(patch.Radial) * relativeSpin;
            double forward = slip.Dot(patch.Forward);
            double x = forward / LongitudinalRegularizationMetersPerSecond;
            double y = side / sideScale;
            double denominator = Math.Sqrt(1 + x * x + y * y);
            double longitudinal = -capacity * x / denominator, lateral = -capacity * y / denominator;
            var tyre = new Tyre215(patch, normal, longitudinal, lateral, capacity, normalLoss);
            double axialTorque = patch.Axle.Dot(patch.Radial.Cross(tyre.Force));
            // Angular state is total world L. The frame rate follows L minus axle*p,
            // including rotation of the axle. Prescribing steer incurs its generalized work.
            double steeringWorkRate = -state.FrontWheel *
                motion.WorldRate.Dot(state.Pose.Apply(Vec215.Up).Cross(patch.Axle)) * steerRate;
            response = new(axialTorque, relativeSpin, forward, steeringWorkRate, 0,
                -(longitudinal * forward + lateral * side), 0);
            return tyre;
        }

        double forwardSpeed = patch.Velocity.Dot(patch.Forward);
        double transport = Math.Sqrt(forwardSpeed * forwardSpeed +
            LowSpeedTransportMetersPerSecond * LowSpeedTransportMetersPerSecond) / RelaxationMeters;
        if (SteadyLateral)
        {
            // Eliminating q at equilibrium gives this exact same force curve,
            // including the saturated branch of the load-limited spring.
            double force = -Math.Clamp(ShearStiffness * side / transport, -capacity, capacity);
            response = new(0, 0, forwardSpeed, 0, 0, -force * side, 0);
            return new(patch, normal, 0, force, capacity, normalLoss);
        }
        double strain = state.FrontShearMeters;
        double spring = ShearStiffness * strain;
        double scale = capacity <= 0 ? 0 : Math.Min(1, capacity / Math.Max(1e-9, Math.Abs(spring)));
        // Coupling both force and deformation rate gives d(stored shear energy)/dt +
        // dissipation = (k*q*scale)*side velocity at any load, including airborne decay.
        response = new(0, 0, forwardSpeed, 0, scale * side - transport * strain,
            0, ShearStiffness * transport * strain * strain);
        return new(patch, normal, 0, -spring * scale, capacity, normalLoss);
    }
}
