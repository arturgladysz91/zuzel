using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Speed148;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record RiderTuning(double CornerUse = .94, double RollOffMetersPerSecondSquared = 1.20,
    double HeadingGainPerSecond = 3.0, double LookaheadSeconds = .19);

internal sealed record HandlingDecision(double Time, double Reference, FreeRacingAction Action, int Forecasts, int Queries,
    double TargetSpeed, double TargetOffset, double LeanRadians, string Line);

internal sealed class HandlingController
{
    private readonly TrackFrame frame;
    private readonly BendMap map;
    private readonly RiderTuning tuning;
    private readonly HandlingSettings settings;
    private readonly TrackSurfaceState surface;
    private readonly RiderSpec spec;
    private readonly WorldParameters physics;
    private readonly float mass, inertia;
    private readonly double lateralBudget;
    private readonly HandlingRoute inside, outside;
    private HandlingRoute active;
    private BendVisit? selectedBend;

    internal HandlingController(TrackFrame frame, TrackStateSnapshot track, WorldParameters physics, RiderSpec spec,
        float mass, float inertia, HandlingSettings settings, RiderTuning tuning)
    {
        this.frame = frame;
        map = new(frame);
        this.settings = settings;
        this.tuning = tuning;
        this.spec = spec;
        this.physics = physics;
        this.mass = mass;
        this.inertia = inertia;
        surface = track.SampleSurface(0, 2);
        double grip = LoadBasedTyreGrip.Budget(mass, surface, TwoContactBikeParameters.Default, physics.LoadBasedTyres)
            .TotalCapacityNewtons * settings.GripScale / mass;
        lateralBudget = grip;
        inside = new(frame, map, spec, grip, tuning, 4.9);
        outside = new(frame, map, spec, grip, tuning, 6.3);
        active = outside;
    }

    internal HandlingDecision Choose(FreeRacingInput input, CrankClutch144 shaft, double lean)
    {
        double delay = RiderController.ReactionSeconds(spec);
        var state = new HandlingForecast(input.Rider, input.Time, input.Omega, input.Steering, lean, shaft.Omega);
        state = HandlingPreview.Advance(state, delay, input.Active, input.Pending, surface, settings,
            spec, physics, frame, mass, inertia, shaft.Parameters);
        var body = state.Rider;
        double s = body.TotalReferenceMeters;
        double speed = body.Speed;
        var where = map.Locate(s);
        var bend = where.Current ?? where.Next;
        if (selectedBend != bend && s >= bend.Entry - 28 && s <= bend.Entry - 12)
        {
            // The lines share their geometry at the choice point. Faster or
            // already outward arrivals retain the extra corner radius.
            active = speed > inside.At(bend.Entry + 8).Speed + .35 ||
                body.Road.OffsetMeters > inside.At(s).Offset + 1 ? outside : inside;
            selectedBend = bend;
        }
        var current = active.At(s);
        var ahead = active.At(s + speed * tuning.LookaheadSeconds);
        double offsetError = body.Road.OffsetMeters - current.Offset + .28 * Vector2.Dot(body.Velocity, body.Road.Outward);
        double aim = current.Course + Math.Atan(offsetError / (6 + .25 * speed));
        double usage = Math.Clamp(ahead.Curvature * speed * speed / lateralBudget,-.91,.91);
        // Rear slip required to carry its share of the steady lateral load,
        // including the velocity of the rear axle from body yaw.
        double desiredBeta = Math.Clamp(.081 * usage / Math.Sqrt(1-usage*usage) -
            .6 * ahead.Curvature,-.21,.21);
        double error = BikeAngles.Wrap(aim + desiredBeta - body.Heading);
        double curvature = ahead.Curvature + tuning.HeadingGainPerSecond * error / Math.Max(6, speed);
        double wheelbase = TwoContactBikeParameters.Default.FrontAxleFromComMeters +
            TwoContactBikeParameters.Default.RearAxleFromComMeters;
        double steer = Math.Clamp(Math.Atan(wheelbase * curvature), -.31, .31);
        double targetSpeed = active.At(s + speed * .19).Speed;
        double scrub = Math.Clamp(Math.Abs(ahead.Curvature) * speed * speed / 16, 0, 1);
        double gas = .12 + .22 * scrub + .32 * (targetSpeed - speed);
        double slip = (state.Wheel * .33 - speed * Math.Cos(body.SlideRadians)) / Math.Sqrt(speed * speed + .25);
        if (speed > 7) gas = Math.Min(gas, 1 - .72 * Math.Max(0, slip - (.58 - .31 * scrub)));
        double excess = Math.Max(0, Math.Abs(body.SlideRadians) - .22);
        gas = Math.Min(gas, 1 - 4 * excess);
        var action = new FreeRacingAction((float)steer, (float)Math.Clamp(gas, 0, 1));
        return new(input.Time, input.Rider.TotalReferenceMeters, action, 1, 0, targetSpeed, current.Offset, lean,
            ReferenceEquals(active, outside) ? "outside" : "inside");
    }
}

internal sealed record RoutePoint(double Offset, double Course, double Curvature, double Speed);

internal sealed class HandlingRoute
{
    private const double Step = .50;
    private const double StraightOffset = 7.7;
    private readonly double start;
    private readonly RoutePoint[] points;

    internal HandlingRoute(TrackFrame frame, BendMap map, RiderSpec spec, double capacityAcceleration,
        RiderTuning tuning, double apexOffset)
    {
        start = spec.ReferenceMeters - 2;
        int count = (int)Math.Ceiling((4 * frame.LapMeters - start) / Step);
        var offsets = new double[count];
        var xy = new Vector2[count];
        var course = new double[count];
        var curvature = new double[count];
        var speedSquared = new double[count];
        double ratio = 10 - 2 * spec.Setup.Gearing;
        double engineKinematicSpeed = Engine500.CutoffRpm * Math.PI / 30 * .33 / ratio;

        double NominalOffset(double s)
        {
            var where = map.Locate(s);
            var upcoming = where.Current ?? where.Next;
            if (s > upcoming.Exit || s < upcoming.Entry - 22)
                upcoming = upcoming.Index == 0 ? map.At(upcoming.Lap - 1, 1) : map.At(upcoming.Lap, 0);
            double middle = upcoming.Entry + .50 * upcoming.Length;
            double t1 = Smooth((s - (upcoming.Entry - 22)) / (middle - upcoming.Entry + 22));
            double t2 = Smooth((upcoming.Exit + 12 - s) / (upcoming.Exit + 12 - middle));
            return StraightOffset + (apexOffset - StraightOffset) * t1 * t2;
        }

        double joinOffset = spec.OffsetMeters - NominalOffset(spec.ReferenceMeters);
        for (int i = 0; i < count; i++)
        {
            double s = start + i * Step;
            offsets[i] = NominalOffset(s) + joinOffset * (1 - Smooth((s - spec.ReferenceMeters) / 39));
            xy[i] = frame.At(s, offsets[i]).Position;
        }

        for (int i = 0; i < count; i++)
        {
            var difference = xy[Math.Min(i+1,count-1)] - xy[Math.Max(i-1,0)];
            course[i] = Math.Atan2(difference.Y, difference.X);
            if (i > 0 && i+1 < count)
            {
                var before = xy[i]-xy[i-1];
                var after = xy[i+1]-xy[i];
                curvature[i] = 2d*(before.X*after.Y-before.Y*after.X)/(before.Length()*after.Length()*(before+after).Length());
            }
            speedSquared[i] = Math.Min(engineKinematicSpeed * engineKinematicSpeed,
                tuning.CornerUse * capacityAcceleration / Math.Max(.0001, Math.Abs(curvature[i])));
        }

        for (int i = count-2; i >= 0; i--)
            speedSquared[i] = Math.Min(speedSquared[i], speedSquared[i+1] + 2 *
                tuning.RollOffMetersPerSecondSquared * Vector2.Distance(xy[i],xy[i+1]));
        points = Enumerable.Range(0,count).Select(i => new RoutePoint(offsets[i],course[i],curvature[i],
            Math.Sqrt(speedSquared[i]))).ToArray();
    }

    internal RoutePoint At(double s)
    {
        double x = Math.Clamp((s - start) / Step, 0, points.Length - 1);
        int i = Math.Min((int)x, points.Length-2);
        double u = x-i;
        var a = points[i];
        var b = points[i+1];
        double Mix(double y,double z) => y+u*(z-y);
        return new(Mix(a.Offset,b.Offset),a.Course+u*BikeAngles.Wrap(b.Course-a.Course),
            Mix(a.Curvature,b.Curvature),Mix(a.Speed,b.Speed));
    }

    private static double Smooth(double v)
    {
        double t = Math.Clamp(v, 0, 1);
        return t*t*t*(10+t*(-15+6*t));
    }
}

internal sealed record HandlingForecast(RiderView Rider, double Time, double Wheel, float Steer, double Lean, double Shaft);

internal static class HandlingPreview
{
    internal static HandlingForecast Advance(HandlingForecast state, double seconds, FreeRacingAction active,
        FreeRacingCommand[] pending, TrackSurfaceState surface, HandlingSettings settings, RiderSpec spec,
        WorldParameters physics, TrackFrame frame, float mass, float inertia, LaunchParameters144 launch)
    {
        var body = state.Rider;
        var shaft = new CrankClutch144(launch,state.Shaft);
        var wheel = ForecastFork148.Wheel(state.Wheel,physics.RearWheel);
        double time = state.Time, goal = time+seconds, lean = state.Lean;
        float steering = state.Steer;
        int at = 0;
        double reaction = RiderController.ReactionSeconds(spec);
        float positionStepFactor = (physics.SolverSubsteps + 1f) / (2f * physics.SolverSubsteps);
        while (time < goal-1e-9)
        {
            while (at < pending.Length && pending[at].ActivationAt <= time+1e-9) active = pending[at++].Action;
            double next = Math.Min(goal, (Math.Floor((time+1e-8)/.01)+1)*.01);
            if (at<pending.Length) next = Math.Min(next,pending[at].ActivationAt);
            float dt = (float)(next-time);
            body = body with { Drive=active.Drive };
            var step = HandlingContact.Prepare(settings,surface,body,spec,physics,time,mass,inertia,wheel,
                steering,lean,dt,active.SteeringRadians,shaft,reaction);
            var force = step.Forces;
            var acceleration = force.NetForce/mass;
            float angularAcceleration = force.NetTorque/inertia;
            var position = body.Position+body.Velocity*dt+positionStepFactor*dt*dt*acceleration;
            var road = frame.Project(position);
            double s = body.TotalReferenceMeters+frame.UnwrapDelta(body.Road.ReferenceMeters,road.ReferenceMeters);
            body = body with { Position=position, Velocity=body.Velocity+dt*acceleration,
                Heading=(float)BikeAngles.Wrap(body.Heading+body.AngularVelocity*dt+positionStepFactor*dt*dt*angularAcceleration),
                AngularVelocity=body.AngularVelocity+dt*angularAcceleration, Road=road, TotalReferenceMeters=s,
                DistanceMeters=body.DistanceMeters+Vector2.Distance(body.Position,position),Context=frame.Context(road,s) };
            wheel = ForecastFork148.Wheel(shaft.Prepared.Wheel.AfterOmega,wheel.Parameters);
            steering = force.SteeringAfter;
            lean = step.LeanAfter;
            shaft.Commit();
            time = next;
        }
        return new(body,time,wheel.AngularVelocityRadPerSecond,steering,lean,shaft.Omega);
    }
}
