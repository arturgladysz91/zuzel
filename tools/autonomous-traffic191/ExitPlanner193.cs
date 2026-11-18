using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record MotionObservation193(double Reference, double Course, double Curvature,
    double AheadCurvature, double X, double Y, double SpliceMeters);
internal sealed record ExitObservation193(FreeRacingAction? Action, string Choice, int Evaluations,
    int Feasible, double? StockCost, double? ChosenCost, double? MinimumSweep, double? MinimumPairGap,
    double? TargetX, double? TargetY, double? TargetCourse, double? PlannedCurvature);

/// <summary>Receding-horizon direct shooting over closed-loop steering and drive schedules.
/// Alternatives start from the same activation state. Only the first request is executable.
/// H25 is reused for predicted axle loads and actuator dynamics. Box2D alone certifies a race.</summary>
internal sealed class ExitPlanner193
{
    private const double LocalHorizonSeconds = 1.25;
    private const double BeginBeforeExitMeters = 29;
    private const double EndAfterExitMeters = 22;
    private const double GuardMeters = .62;
    private const double MinimumGainSeconds = .012;
    private const double GeometrySpeedMargin = .91;
    private const double RadiusMeters = .33;
    private readonly Track191 track;
    private readonly TrackFrame frame;
    private readonly BendMap bends;
    private readonly RacingLineGeometry boundary;
    private readonly TrackSurfaceState surface;
    private readonly WorldParameters physics;
    private readonly RiderSpec spec;
    private readonly HandlingSettings settings;
    private readonly Schedule188 timing;
    private readonly float mass, inertia;
    private readonly double lateralBudget, ratio, wheelbase;
    private const double CandidateDriveSlew = .20;
    private const double CandidateSteerSlew = .030;

    private sealed record Schedule(string Name, double Offset, double LookaheadSeconds, double DriveDelta);
    private sealed record Shot(FreeRacingAction First, double Cost, double Sweep, double Gap, HandlingForecast End,
        double Course, double Curvature);

    internal ExitPlanner193(Track191 track, TrackStateSnapshot trackSurface, WorldParameters physics,
        RiderSpec spec, HandlingSettings settings, Schedule188 timing, float mass, float inertia)
    {
        this.track = track;
        frame = track.Frame;
        bends = new(frame);
        boundary = new(frame);
        surface = trackSurface.SampleSurface(0, 2);
        this.physics = physics;
        this.spec = spec;
        this.settings = settings;
        this.timing = timing;
        this.mass = mass;
        this.inertia = inertia;
        var engine = Engine500Parameters.Default;
        var bike = TwoContactBikeParameters.Default;
        wheelbase = bike.FrontAxleFromComMeters + bike.RearAxleFromComMeters;
        ratio = engine.ShortOverallRatio + (engine.LongOverallRatio-engine.ShortOverallRatio)*spec.Setup.Gearing;
        lateralBudget = LoadBasedTyreGrip.Budget(mass,surface,bike,physics.LoadBasedTyres).TotalCapacityNewtons *
            settings.GripScale / mass;
    }

    internal ExitObservation193? Choose(FreeRacingInput input, HandlingForecast activation, FreeRacingAction stock,
        Motion191 reference, HandlingController controller, IReadOnlyList<Rival191> opponents, LaunchParameters144 launch)
    {
        var bike = activation.Rider;
        double s = bike.TotalReferenceMeters;
        var located = bends.Locate(s);
        var previousIndex = (located.Next.Index + bends.Count - 1) % bends.Count;
        var bend = located.Current ?? bends.At(located.Next.Lap-(located.Next.Index==0 ? 1 : 0), previousIndex);
        if (bike.Speed < 14 || s < bend.Exit-BeginBeforeExitMeters || s > bend.Exit+EndAfterExitMeters ||
            bike.Activity is RiderActivity.Disturbed or RiderActivity.Crashed)
            return null;

        // Predict rivals from the same immutable observation used by #191. Do not inspect their requests.
        var nearby = opponents.Where(p => Vector2.DistanceSquared(p.View.Position,bike.Position)<48*48).ToArray();
        var limits = track.Bounds(bend.Exit+8);
        double offset = Math.Clamp(bike.Road.OffsetMeters,limits.Minimum,limits.Maximum);
        double planned = Math.Clamp(reference.At(bend.Exit+18).Offset,limits.Minimum,limits.Maximum);
        double lateralMomentum = Vector2.Dot(bike.Velocity,bike.Road.Outward);
        double drift = Math.Clamp(offset+.55*lateralMomentum,limits.Minimum,limits.Maximum);
        var schedules = new Schedule[]
        {
            new("continue-drift", drift, .19, 0),
            new("prepare-earlier", offset, .30, 0),
            new("hold-corridor", offset, .19, 0),
            new("outward", Math.Min(limits.Maximum,offset+1.4), .23, .08),
            new("inward", Math.Max(limits.Minimum,offset-1.25), .23, 0),
            new("tactical-corridor", planned, .23, 0)
        };
        double step = timing.Period(input.Time);
        Shot? Evaluate(Schedule? schedule)
        {
            var current = activation;
            var plan = track.Frame.Shape!.At(s);
            double firstCurvature = plan.Curvature / (1+plan.Curvature*offset);
            double firstCourse = plan.Heading;
            var first = schedule is null ? stock :
                Request(current, input.Active, stock, schedule, bend, out firstCourse, out firstCurvature);
            var active = first;
            double sweep = 10, gap = 10, penalty = 0;
            var oldGap = boundary.Clearance(current.Rider);
            for (int k=0; k<(int)Math.Round(LocalHorizonSeconds/step); k++)
            {
                var nextStock = controller.ChooseAt(input,current,reference).Action;
                var next = schedule is null ? nextStock :
                    Request(current,active,nextStock,schedule,bend,out _,out _);
                var before = current;
                double maxRearUse = 0;
                current = HandlingPreview.Advance(current,step,active,[],surface,settings,spec,physics,frame,mass,inertia,launch,
                    (_,power) => maxRearUse=Math.Max(maxRearUse,power.Wheel.RearUtilization));
                var v = current.Rider;
                var newGap = boundary.Clearance(v);
                sweep = Math.Min(sweep,Clearance153.SweepLowerBound(before.Rider,v,step,oldGap,newGap));
                oldGap = newGap;
                if (sweep<GuardMeters || Math.Abs(v.SlideRadians)>.52 || maxRearUse>1.000001 ||
                    v.TotalReferenceMeters<before.Rider.TotalReferenceMeters)
                    return null;

                double elapsed = current.Time - input.Time;
                double risk = 0;
                if (nearby.Length > 0)
                {
                    var pose = ContactStep191.Pose(v);
                    foreach (var other in nearby)
                    {
                        var peer = other.At(elapsed);
                        if ((peer.Position-pose.Position).Length>4) continue;
                        double separation = MechanicalSeparation.Between(pose,peer).SignedMeters;
                        gap = Math.Min(gap,separation);
                        double guard = .16+.22*elapsed;
                        if (separation<guard && (elapsed<.55 || separation<.03))
                            return null;
                        risk += Math.Max(0,.9+guard-separation);
                    }
                }
                // Stabilization costs apply during straightening, not in the middle of the slide.
                double straight = Math.Clamp((v.TotalReferenceMeters-bend.Exit+5)/12,0,1);
                double courseError = BikeAngles.Wrap(v.Heading-v.SlideRadians-v.Road.Heading);
                penalty += step*(.35*risk + straight*(.34*Math.Abs(v.AngularVelocity)+
                    .45*Math.Abs(courseError)+.18*Math.Abs(v.SlideRadians)) +
                    .13*Math.Abs(next.SteeringRadians-active.SteeringRadians)/step+
                    .04*Math.Max(0,maxRearUse-.92));
                active = next;
            }
            double cost = RemainingSeconds(current,bend,reference) + penalty +
                .30*Math.Abs(CourseError(current.Rider));
            if (!double.IsFinite(cost))
                return null;
            return new(first,cost,sweep,gap,current,firstCourse,firstCurvature);
        }

        var incumbent = Evaluate(null);
        Shot? best = null;
        Schedule? winner = null;
        int feasible = incumbent is null ? 0 : 1;
        int evaluated = 1;
        void Try(Schedule schedule)
        {
            evaluated++;
            var shot = Evaluate(schedule);
            if (shot is null) return;
            feasible++;
            if (best is not null && shot.Cost>=best.Cost) return;
            best = shot;
            winner = schedule;
        }
        foreach (var schedule in schedules)
            Try(schedule);
        // One bounded coordinate-search sweep, initialized by the best feasible seed.
        // Endpoints vary with observed position and opponents instead of identifying a catalog line.
        if (winner is { } seed)
        {
            foreach (double direction in new[] { -1d,1d })
            {
                Try(seed with { Offset=Math.Clamp(seed.Offset+direction*.45,limits.Minimum,limits.Maximum) });
                Try(seed with { LookaheadSeconds=Math.Clamp(seed.LookaheadSeconds+direction*.055,.14,.36) });
                Try(seed with { DriveDelta=Math.Clamp(seed.DriveDelta+direction*.10,-.12,.18) });
            }
        }
        if (best is null || winner is null || incumbent is not null && best.Cost+MinimumGainSeconds>=incumbent.Cost)
            return new(null,"FAST50",evaluated,feasible,incumbent?.Cost,incumbent?.Cost,incumbent?.Sweep,
                incumbent?.Gap,null,null,null,null);
        var xy=best.End.Rider.Position;
        return new(best.First,winner.Name,evaluated,feasible,incumbent?.Cost,best.Cost,best.Sweep,best.Gap,
            xy.X,xy.Y,best.Course,best.Curvature);
    }

    private static double CourseError(RiderView v) =>
        BikeAngles.Wrap(v.Heading-v.SlideRadians-v.Road.Heading);

    private FreeRacingAction Request(HandlingForecast state,FreeRacingAction previous,FreeRacingAction stock,
        Schedule schedule,BendVisit bend,out double course,out double curvature)
    {
        var v = state.Rider;
        double speed = Math.Max(6,v.Speed);
        double s = v.TotalReferenceMeters;
        double forward = Math.Max(6,frame.ReferenceSpeed(v));
        double ahead = s+schedule.LookaheadSeconds*forward;
        double roadCurvature = frame.Shape!.At(ahead).Curvature;
        curvature = roadCurvature/(1+v.Road.OffsetMeters*roadCurvature);
        double offset = schedule.Offset-v.Road.OffsetMeters;
        double outward = Math.Clamp(offset/(1.30+.012*Math.Max(0,bend.Exit-s)),-1.60,1.60);
        course = BikeAngles.Wrap(v.Road.Heading-Math.Atan2(outward,speed));
        double chi = BikeAngles.Wrap(v.Heading-v.SlideRadians);
        double usage = Math.Clamp(curvature*speed*speed/lateralBudget,-.88,.88);
        double beta = Math.Clamp(.081*usage/Math.Sqrt(1-usage*usage)-.6*curvature,-.20,.20);
        double feedForward = speed*curvature;
        double yaw = feedForward+2.45*BikeAngles.Wrap(course-chi)
            +1.15*(beta-v.SlideRadians)-.19*(v.AngularVelocity-feedForward);
        double steer = Math.Clamp(Math.Atan(wheelbase*yaw/speed),-.31,.31);
        steer = Math.Clamp(steer,previous.SteeringRadians-CandidateSteerSlew,
            previous.SteeringRadians+CandidateSteerSlew);
        double gas = stock.Drive+schedule.DriveDelta*Math.Clamp((s-bend.Exit+8)/16,0,1);
        gas = Math.Min(gas,1-4*Math.Max(0,Math.Abs(v.SlideRadians)-.22));
        gas = Math.Clamp(gas,previous.Drive-CandidateDriveSlew,previous.Drive+CandidateDriveSlew);
        return new((float)steer,(float)Math.Clamp(gas,0,1));
    }

    internal double ApproachValue(HandlingForecast start,HandlingForecast end,Motion191 reference)
    {
        // The outer tactical stage can see from the approach to the following bend.
        // Its original hard occupancy/clearance checks and hysteresis still veto choices.
        double s = start.Rider.TotalReferenceMeters;
        var location = bends.Locate(s);
        var bend = location.Current ?? location.Next;
        return s >= bend.Entry - 28 && s < bend.Exit - BeginBeforeExitMeters
            ? RemainingSeconds(end,bend,reference)
            : 0;
    }

    private double RemainingSeconds(HandlingForecast end,BendVisit bend,Motion191 reference)
    {
        // Terminal value through the rest of the straight to the next entry.
        // Spatial, through-speed estimate only. Contact transitions are scored by H25 above.
        var v = end.Rider;
        double s = v.TotalReferenceMeters;
        double destination = bend.NextEntry;
        double speed = Math.Max(5,v.Speed*Math.Cos(CourseError(v)));
        double limit = RadiusMeters*Engine500.CutoffRpm*Math.Tau/60/ratio;
        double seconds = 0;
        double entryCurvature = Math.Abs(frame.Shape!.At(destination+5).Curvature);
        double entryLimit = Math.Min(reference.At(destination).Speed,
            Math.Sqrt(GeometrySpeedMargin*lateralBudget/Math.Max(.0001,entryCurvature)));
        const double spacingMeters = 5;
        while (s < destination)
        {
            double ds = Math.Min(spacingMeters,destination-s);
            double k = frame.Shape!.At(s+ds*.5).Curvature;
            double distance = ds*(1+k*v.Road.OffsetMeters);
            double remaining = destination-s-ds;
            double drive = Engine500.Evaluate(speed/RadiusMeters,spec.Setup,1).AvailableWheelTorqueNewtonMeters/RadiusMeters;
            double resistance = LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)speed);
            double lateral = k*speed*speed/Math.Max(1,1+k*v.Road.OffsetMeters);
            double capacity = mass*Math.Sqrt(Math.Max(0,lateralBudget*lateralBudget-lateral*lateral));
            double acceleration = (Math.Min(.72*drive,capacity)-resistance)/mass;
            double next = Math.Sqrt(Math.Max(25,speed*speed+2*acceleration*distance));
            double envelope = Math.Sqrt(entryLimit*entryLimit+2*1.35*remaining);
            double localLimit = Math.Sqrt(GeometrySpeedMargin*lateralBudget/Math.Max(.0001,Math.Abs(k)));
            double nextSpeed = Math.Max(5,Math.Min(Math.Min(limit,localLimit),Math.Min(next,envelope)));
            seconds += 2*distance/(speed+nextSpeed);
            speed = nextSpeed;
            s += ds;
        }
        return seconds;
    }
}

