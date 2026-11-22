using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal readonly record struct ExitMotion208(double Forward,double Outward,double Course,double Momentum,double Yaw)
{
    internal static ExitMotion208 From(RiderView rider,RoadPoint straight,double mass)
    {
        double along=Vector2.Dot(rider.Velocity,straight.Tangent);
        double lateral=Vector2.Dot(rider.Velocity,straight.Outward);
        return new(along,lateral,Math.Atan2(lateral,along),mass*lateral,rider.AngularVelocity);
    }

    internal static double Inward(Vector2 force,RoadPoint straight,double mass)=>-Vector2.Dot(force,straight.Outward)/mass;

    // Constant-deceleration diagnostic. Only an independently established upper bound on
    // inward acceleration gives a lower bound on stopping distance. Samples are estimates.
    internal double? StopDistance(double inward) =>
        Outward<=0?0:inward>0?Outward*Outward/(2*inward):null;

    internal double? StopTime(double inward)=>Outward<=0?0:inward>0?Outward/inward:null;

    internal double Room(double clearance,double sweep,double threshold)=>Math.Min(clearance,sweep)-threshold;
}

internal readonly record struct Shape208(double Prepare,double EntryLine,double ApexLine,double ExitLine,
    double ApexShift,double Turn,double MidYaw,double ExitYaw,double ExitCourse,double Flatten,
    double Drift,double RouteMix,double Coast,double MidPace,double ExitPace,double Ramp,
    double EntrySteer,double LateSteer,double CourseGain,double Beta,double Geometry);

internal readonly record struct Recovery208(double Steer,double Gas,double Gain);
internal sealed record Maneuver208(int Visit,Shape208 Shape,Recovery208 Recovery,bool Late=false,bool Speed=true);
internal readonly record struct RoutePoint208(double S,double Offset,double Course,double Speed,double Yaw,double Beta);

internal sealed class ExitPolicy208
{
    private readonly BendMap map;
    private readonly double lapMeters;
    private readonly Func<Start197,FreeRacingAction> baseline;
    private readonly RoutePoint208[] learned;
    private readonly RoutePoint208[] geometry;
    private static readonly TwoContactBikeParameters Bike=TwoContactBikeParameters.Default;

    internal ExitPolicy208(BendMap map,double lapMeters,Func<Start197,FreeRacingAction> baseline,
        RoutePoint208[] learned,RoutePoint208[] geometry)
    {
        this.map=map;
        this.lapMeters=lapMeters;
        this.baseline=baseline;
        this.learned=learned;
        this.geometry=geometry;
    }

    internal BendVisit Bend(int visit)=>map.At(visit/map.Count,visit%map.Count);
    internal int Visit(BendVisit bend)=>bend.Lap*map.Count+bend.Index;
    internal BendVisit Previous(int visit)=>Bend(visit-1);
    internal RoadPoint ExitRoad(BendVisit bend)=>map.Locate(bend.Exit+1).Road;
    internal RoadPoint Road(double s)=>map.Locate(s).Road;

    internal FreeRacingAction Action(Start197 state,IReadOnlyDictionary<int,Maneuver208> plan)
    {
        var r=state.State.Rider;
        double s=r.TotalReferenceMeters;
        var location=map.Locate(s);
        var next=location.Current??location.Next;
        int n=Visit(next);
        if(location.Current is null&&s>0&&n>0)
        {
            var previous=Bend(n-1);
            if(s-previous.Exit<20&&plan.TryGetValue(n-1,out var outgoing))
                return At(state,previous,outgoing);
        }
        return plan.TryGetValue(n,out var action)?At(state,next,action):baseline(state);
    }

    internal double Begin(BendVisit bend,Maneuver208 proposal) => proposal.Late?bend.Exit:
        Math.Max(Visit(bend)>0?Previous(Visit(bend)).Exit+6:bend.Entry-42,bend.Entry-proposal.Shape.Prepare);

    internal FreeRacingAction At(Start197 state,BendVisit bend,Maneuver208 proposal)
    {
        var q=baseline(state);
        var r=state.State.Rider;
        double s=r.TotalReferenceMeters;
        double ds=s-bend.Entry, e=s-bend.Exit;
        double start=Begin(bend,proposal);
        if(s<=start||e>=20) return q;
        var m=proposal.Shape;
        double wl=Gate((s-start)/5)*Gate((20-e)/7);
        double v=Math.Max(3,r.Speed);
        var motion=ExitMotion208.From(r,ExitRoad(bend),1);
        if(proposal.Late)
        {
            var patch=proposal.Recovery;
            double ste=patch.Steer+patch.Gain*motion.Course;
            return Bound(q.SteeringRadians+wl*ste,q.Drive+wl*patch.Gas);
        }

        double u=Math.Clamp(ds/bend.Length,0,1);
        double extra=s+Math.Sin(Math.PI*u)*m.ApexShift;
        RoutePoint208 original=AtRoute(learned,s,bend.Lap);
        RoutePoint208 curved=AtRoute(learned,extra,bend.Lap);
        RoutePoint208 h25=AtRoute(geometry,extra,bend.Lap);
        double coreEnds=bend.Entry-8;
        double released=Gate((s-coreEnds)/4);
        double approach=1-Gate(ds/14);
        double apex=Gate((u-.06)/.30)*(1-Gate((u-.63)/.30));
        double exit=Gate((u-.49)/.42);
        double mix=m.RouteMix*released;
        double referenceOffset=Lerp(curved.Offset,h25.Offset,m.Geometry);
        double goalOffset=Lerp(original.Offset,referenceOffset,mix)+m.EntryLine*approach+m.ApexLine*apex+m.ExitLine*exit;
        // The geometric target needs a corresponding rate of lateral displacement.
        // The course reference and line derivative are both optional and smoothly blended.
        double routeCourse=Lerp(curved.Course,h25.Course,m.Geometry);
        double courseGoal=Lerp(original.Course,routeCourse,mix);
        double courseNow=Corridor199.CourseError(r);
        double lineError=(r.Road.OffsetMeters-goalOffset)/(8+.22*v);
        double archCourseError=original.Course-courseNow;
        double rotation=m.Turn*Pulse(ds,-7,7,31)+m.MidYaw*Pulse(ds,9,bend.Length*.62,bend.Length*.88);
        double phaseYaw=rotation+m.CourseGain*(courseNow-courseGoal+lineError)
            +m.Beta*apex*(r.SlideRadians-Lerp(curved.Beta,h25.Beta,m.Geometry));
        // Baseline feedback already corrects the baseline course. Correct the displacement
        // caused by reshaping the reference without doubling that feedback.
        phaseYaw+=m.CourseGain*archCourseError*.60;
        double flat=Gate((e+m.Flatten)/Math.Max(5,m.Flatten));
        double tailYaw=m.ExitYaw*Math.Clamp(-r.AngularVelocity,-1.5,1.5);
        double tailCourse=m.Drift*(motion.Course-m.ExitCourse+lineError);
        double phaseSteer=q.SteeringRadians+(Bike.FrontAxleFromComMeters+Bike.RearAxleFromComMeters)*phaseYaw/v
            +m.EntrySteer*Pulse(ds,-8,8,35)+m.LateSteer*Pulse(e,-47,-19,0);
        double trim=flat*Math.Clamp(m.Drift*.26,0,1);
        double steer=Lerp(phaseSteer,(Bike.FrontAxleFromComMeters+Bike.RearAxleFromComMeters)*(tailCourse+tailYaw)/v,trim);
        if(e>=0)
        {
            var recovery=proposal.Recovery;
            steer+=Gate(e/2)*(recovery.Steer+recovery.Gain*motion.Course);
        }
        // The original strict observer still enforces position, direction and history.
        // Approach changes have less steering freedom inside the preceding yaw core.
        if(s<=coreEnds&&Math.Abs(r.AngularVelocity)>.12)
        {
            double permitted=(Bike.FrontAxleFromComMeters+Bike.RearAxleFromComMeters)*.19/v;
            steer=Lerp(steer,Math.Clamp(steer,-permitted,permitted),.45);
        }

        double drive=q.Drive;
        if(proposal.Speed)
        {
            double cut=m.Coast*Gate((s-start)/10)*(1-Gate(ds/30));
            double speed=original.Speed-cut-m.MidPace*apex-m.ExitPace*exit;
            double cap=Math.Clamp(.23+.17*(speed-v),0,1);
            double reduced=Math.Min(q.Drive,cap);
            double paceBlend=Gate((cut+m.MidPace*apex+m.ExitPace*exit)/1.4);
            drive=Lerp(q.Drive,reduced,paceBlend);
            drive+=m.Ramp*Pulse(e,-36,-10,14)+Gate(e/3)*proposal.Recovery.Gas;
        }
        return Bound(Lerp(q.SteeringRadians,steer,wl),Lerp(q.Drive,drive,wl));
    }

    private RoutePoint208 AtRoute(RoutePoint208[] guide,double s,int lap)
    {
        // Fifth and sixth use the same route shapes at homologous geometric points.
        double local=s-lap*lapMeters+lapMeters;
        int a=0,b=guide.Length-1;
        while(b-a>1)
        {
            int k=(a+b)/2;
            if(guide[k].S<=local) a=k;
            else b=k;
        }
        var x=guide[a]; var y=guide[b];
        double w=Math.Clamp((local-x.S)/Math.Max(.0001,y.S-x.S),0,1);
        return new(local,Lerp(x.Offset,y.Offset,w),Lerp(x.Course,y.Course,w),
            Lerp(x.Speed,y.Speed,w),Lerp(x.Yaw,y.Yaw,w),Lerp(x.Beta,y.Beta,w));
    }

    internal static FreeRacingAction Bound(double steer,double drive)=>new(
        (float)Math.Clamp(steer,-Bike.MaximumSteeringRadians,Bike.MaximumSteeringRadians),
        (float)Math.Clamp(drive,0,1));

    internal static double Lerp(double a,double b,double x)=>a+(b-a)*x;

    internal static double Gate(double x)
    {
        x=Math.Clamp(x,0,1);
        return x*x*(3-2*x);
    }

    private static double Pulse(double s,double from,double middle,double to)=>
        Gate((s-from)/(middle-from))*(1-Gate((s-middle)/(to-middle)));
}
