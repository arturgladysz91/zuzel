using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record RacingFailure206(string Gate,string Condition,double Time,double S,RiderView State,
    double Outer,double Swept,double RequiredClearance,Corridor199.Violation? Corridor=null,
    Technique200.Checkpoint? Technique=null,Window199? TechniqueWindow=null,
    double? Course=null,double? CorrectionSeconds=null,double? CourseChange=null);

internal sealed class RacingProfile206
{
    private readonly Corridor199 corridor;
    private readonly Technique200 technique;
    private readonly RacingRestriction206 omit;

    internal RacingProfile206(BendMap map,RacingRestriction206 omit=RacingRestriction206.None)
    {
        this.omit=omit;
        corridor=new(map);
        technique=new(corridor.Windows);
    }

    internal Cursor Begin(Start197 start)
    {
        if(start.TechniqueHistory is {} state) return new(corridor,technique.Restore(state),omit);
        if(Math.Abs(start.State.Time)>1e-8)
            throw new InvalidOperationException("Strict interior forecast requires the observed technique checkpoint.");
        var cursor=Begin();
        cursor.ObserveInitial(start.State.Rider,start.State.Time);
        return cursor;
    }

    internal Cursor Begin()=>new(corridor,technique.Begin(),omit);

    internal static string? Physical(RiderView body,double time,float dt,TwoContactForces forces,ClutchStep144 power)
    {
        if(!float.IsFinite(body.Speed)||!float.IsFinite(body.AngularVelocity)||
            !double.IsFinite(power.Wheel.RearUtilization)||!double.IsFinite(power.ClutchHeatJoules)||
            !double.IsFinite(power.Wheel.LongitudinalLossJoules)||power.Wheel.RearUtilization>1.000001||
            power.ClutchHeatJoules<-.001||power.Wheel.LongitudinalLossJoules<-.001) return "force-or-energy";
        if(Math.Abs(body.SlideRadians)>Math.PI/4) return "beta";
        if(Vector2.Dot(body.Velocity,body.Road.Tangent)<-.001||body.Speed<.2&&time>2) return "progress";
        if(!FreeLearningExperiment.SteeringStepIsPhysical(forces,dt)) return "actuator invariant";
        return null;
    }

    internal sealed class Cursor(Corridor199 corridor,Technique200.Cursor observer,RacingRestriction206 omit)
    {
        internal Technique200.Checkpoint Capture()=>observer.Capture();

        internal void ObserveInitial(RiderView body,double time)=>observer.Observe(body,time);

        internal (RacingFailure206? Corridor,RacingFailure206? Technique) InspectStyle(RiderView body,double time,
            double outer,double swept,double clearance,bool finish=false)
        {
            var course=InspectCorridor(body,time,outer,swept,clearance);
            var technique=InspectTechnique(body,time,outer,swept,clearance,finish);
            return (course,technique);
        }

        internal RacingFailure206? Check(RiderView body,double time,double outer,double swept,double clearance,
            string? physical,bool finish)
        {
            if(physical is not null) return new("physical",physical,time,body.TotalReferenceMeters,body,outer,swept,clearance);
            if(!double.IsFinite(swept)||swept<clearance)
                return new("swept","swept",time,body.TotalReferenceMeters,body,outer,swept,clearance);
            return InspectCorridor(body,time,outer,swept,clearance)??InspectTechnique(body,time,outer,swept,clearance,finish);
        }

        private RacingFailure206? InspectCorridor(RiderView body,double time,double outer,double swept,double clearance)
        {
            if(corridor.Inspect(body,outer,omit) is not {} violation) return null;
            string gate=violation.YawExceeded?"corridor-yaw":"course";
            return new(gate,violation.Reason,time,body.TotalReferenceMeters,body,outer,swept,clearance,Corridor:violation);
        }

        private RacingFailure206? InspectTechnique(RiderView body,double time,double outer,double swept,double clearance,bool finish)
        {
            bool turn=omit.HasFlag(RacingRestriction206.Turn), slip=omit.HasFlag(RacingRestriction206.Slip);
            string? reason=observer.Observe(body,time,omit.HasFlag(RacingRestriction206.Correction),turn,slip)
                ??(finish?observer.Finish(turn,slip):null);
            if(reason is null) return null;
            var history=observer.Capture();
            double course=Corridor199.CourseError(body);
            return new("technique",reason,time,body.TotalReferenceMeters,body,outer,swept,clearance,
                Technique:history,TechniqueWindow:history.Index<corridor.Windows.Length?corridor.Windows[history.Index]:null,
                Course:course,CorrectionSeconds:time-history.CorrectionSince,
                CourseChange:history.CorrectionSince.HasValue?BikeAngles.Wrap(course-history.CorrectionCourse):null);
        }
    }
}
