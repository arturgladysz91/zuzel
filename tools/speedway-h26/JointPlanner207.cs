using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal readonly record struct Move207(double DriveBias,double YawBias,double LineBias,
    double GasLeadSeconds,double SteeringLeadSeconds,double StraightYawTarget,double DriftFeedback=0,double? ThrottleCeiling=null);

internal sealed record Phase207(int Index,int Visit,string Name,double Start,double End,double WindowOrigin);

internal sealed record Reserve207(double Swept,double CourseError,double Yaw,double Beta,double Speed,
    double Outer,double Inner,double? FrontNormal,double? RearFraction,double? YawMoment,double? SteeringTravel,
    double DriftOneSecond,double YawRisk,double LineRisk,double CorrectionRemaining,double PendingDelay,
    double SecondsToPhaseEnd,double Quality);

internal sealed record PhaseRun207(string Fixture,bool Accepted,string Stop,Start197 Origin,Start197? Checkpoint,
    HeadingSample198 End,double Swept,Reserve207 Reserve,RacingFailure206? Failure,ContactFailure203? Contact,int Steps);

internal sealed record Branch207(int Id,int Parent,int Phase,Move207 Move,PhaseRun207[] Outcomes)
{
    internal bool Accepted=>Outcomes.All(x=>x.Accepted&&x.Checkpoint is not null);
    internal double WorstReserve=>Outcomes.Min(x=>x.Reserve.Quality);
    internal double WorstSwept=>Outcomes.Min(x=>x.Swept);
    internal double WorstProgress=>Outcomes.Min(x=>x.End.S);
    internal double FastestWorstClock=>Outcomes.Max(x=>x.End.Time);
}

internal static class Beam207
{
    // Work items with the same reference distance are not equivalent. Even
    // exact-state deduplication requires the whole ordered pending and observer.
    internal static bool SameCheckpoint(Start197 a,Start197 b)=>a.State==b.State&&a.Active==b.Active&&
        a.Review==b.Review&&a.RiderHeightOffsetMeters==b.RiderHeightOffsetMeters&&
        a.TechniqueHistory==b.TechniqueHistory&&a.Pending.SequenceEqual(b.Pending);

    internal static bool CanResume(int expectedParent,int storedParent,Start197 expected,Start197 stored)=>
        expectedParent==storedParent&&SameCheckpoint(expected,stored);

    internal static Branch207[] Retain(IEnumerable<Branch207> candidates,int width)
    {
        var eligible=candidates.Where(x=>x.Accepted).ToArray();
        var keep=new List<Branch207>();
        if(eligible.Length==0) return [];
        // Different objectives compete for their own slots. Nothing here is
        // a legality threshold, and no spatial bin claims state equivalence.
        Add(eligible.OrderByDescending(x=>x.WorstReserve).ThenByDescending(x=>x.WorstSwept));
        Add(eligible.OrderByDescending(x=>x.WorstSwept).ThenByDescending(x=>x.WorstReserve));
        Add(eligible.OrderBy(x=>x.Outcomes.Max(r=>r.Reserve.YawRisk+r.Reserve.LineRisk))
            .ThenByDescending(x=>x.WorstReserve));
        Add(eligible.OrderBy(x=>x.FastestWorstClock).ThenByDescending(x=>x.WorstReserve));
        Add(eligible.OrderByDescending(x=>x.Outcomes.Min(r=>r.Reserve.FrontNormal??0))
            .ThenBy(x=>x.Outcomes.Max(r=>r.Reserve.RearFraction??1)));
        if(keep.Count<width)
            foreach(var candidate in eligible.OrderByDescending(x=>x.WorstReserve).ThenBy(x=>x.Id))
                Add([candidate]);
        return keep.ToArray();

        void Add(IEnumerable<Branch207> ranked)
        {
            if(keep.Count>=width) return;
            var distinct=ranked.Where(x=>keep.All(y=>!Equivalent(x,y))).ToArray();
            var independent=distinct.FirstOrDefault(x=>keep.All(y=>x.Parent!=y.Parent));
            if((independent??distinct.FirstOrDefault()) is {} selected) keep.Add(selected);
        }

        static bool Equivalent(Branch207 a,Branch207 b)=>a.Outcomes.Zip(b.Outcomes)
            .All(pair=>SameCheckpoint(pair.First.Checkpoint!,pair.Second.Checkpoint!));
    }

    internal static IOrderedEnumerable<T> RankRobust<T>(IEnumerable<T> items,Func<T,double> worstProgress,
        Func<T,int> completed,Func<T,double> reserve,Func<T,double> time) =>
        items.OrderByDescending(worstProgress).ThenByDescending(completed).ThenByDescending(reserve).ThenBy(time);
}

internal sealed class GeometryPolicy207
{
    private readonly BendMap map;
    private readonly Corridor199 corridor;
    private readonly double lapMeters;
    private readonly RouteKnot[] launch,flying;
    private readonly TwoContactBikeParameters bike=TwoContactBikeParameters.Default;

    private readonly record struct RouteKnot(double Local,double Offset,double Course,double Yaw,double Beta,
        double Outward,double Speed,FreeRacingAction FeedForward);

    internal GeometryPolicy207(BendMap map,double lapMeters,Knot199[] reference)
    {
        this.map=map;
        this.lapMeters=lapMeters;
        corridor=new(map);
        RouteKnot[] Template(int lap)=>reference.Where(k=>k.S>=lap*lapMeters&&k.S<(lap+1)*lapMeters)
            .Select(k=>
            {
                var r=k.Start.State.Rider;
                return new RouteKnot(k.S-lap*lapMeters,r.Road.OffsetMeters,Math.Atan2(r.Velocity.Y,r.Velocity.X),
                    r.AngularVelocity,r.SlideRadians,Vector2.Dot(r.Velocity,r.Road.Outward),r.Speed,k.Action);
            }).ToArray();
        launch=Template(0);
        flying=Template(1);
        if(launch.Length<2||flying.Length<2) throw new InvalidOperationException("Missing geometric driving reference.");
    }

    internal Phase207[] Phases()
    {
        var gates=new List<(int Visit,string Name,double S,double Origin)>();
        for(int lap=0;lap<3;lap++)
        for(int i=0;i<map.Count;i++)
        {
            int number=lap*map.Count+i;
            var w=corridor.Windows[number];
            var b=map.At(lap,i);
            gates.Add((number,"straight stabilization",w.Start+12,w.Start));
            gates.Add((number,"early approach",w.Start+30,w.Start));
            gates.Add((number,"turn preparation",w.Start+48,w.Start));
            gates.Add((number,"release from yaw core",w.Start+54,w.Start));
            gates.Add((number,"entry gate",b.Entry+1,b.Entry));
            gates.Add((number,"turn initiation",b.Entry+12,b.Entry));
            gates.Add((number,"forming slide",b.Entry+29,b.Entry));
            gates.Add((number,"apex",b.Entry+b.Length*.50,b.Entry));
            gates.Add((number,"holding slide",b.Entry+b.Length*.68,b.Entry));
            gates.Add((number,"exit preparation",b.Exit-14,b.Entry));
            gates.Add((number,"bend exit",b.Exit,b.Entry));
        }
        gates.Add((3*map.Count,"strict S3 finish",3*lapMeters,3*lapMeters));
        double last=0;
        var phases=new List<Phase207>();
        foreach(var gate in gates.OrderBy(x=>x.S))
        {
            if(gate.S-last<2) continue;
            phases.Add(new(phases.Count,gate.Visit,gate.Name,last,gate.S,gate.Origin));
            last=gate.S;
        }
        return phases.ToArray();
    }

    internal double StraightCoreRisk(RiderView rider)
    {
        double s=rider.TotalReferenceMeters;
        foreach(var w in corridor.Windows)
            if(s>=w.Start+10&&s<=w.Start+55)
                return Math.Max(Math.Abs(rider.AngularVelocity)/Corridor199.YawLimit,
                    Math.Abs(Corridor199.CourseError(rider))/Corridor199.CourseLimit);
        return Math.Abs(Corridor199.CourseError(rider))/.15;
    }

    internal double PreExitRisk(RiderView rider,double outer)
    {
        var location=map.Locate(rider.TotalReferenceMeters);
        var reference=location.Current?.Exit;
        if(reference is not double exit||exit-rider.TotalReferenceMeters>46) return 0;
        var next=map.Locate(exit+1).Road;
        double drift=Vector2.Dot(rider.Velocity,next.Outward);
        double course=Math.Atan2(drift,Vector2.Dot(rider.Velocity,next.Tangent));
        double proximity=1-Math.Clamp((exit-rider.TotalReferenceMeters)/46,0,1);
        return proximity*(Math.Max(0,drift)*.10+Math.Max(0,course-.07)*1.8+Math.Max(0,1.2-outer)*.3);
    }

    internal FreeRacingAction At(Start197 state,Move207 move)
    {
        var r=state.State.Rider;
        var s=r.TotalReferenceMeters;
        var v=r.Speed;
        var where=map.Locate(s);
        var template=where.Lap==0?launch:flying;
        // The reference shape is periodic after launch and is sampled with
        // the observed road coordinate, never a clocked command sequence.
        var current=AtLocal(template,where.Local);
        var turn=AtLocal(template,where.Local+v*move.SteeringLeadSeconds);
        var power=AtLocal(template,where.Local+v*move.GasLeadSeconds);
        double vOut=Vector2.Dot(r.Velocity,r.Road.Outward);
        double targetOffset=current.Offset+move.LineBias;
        double direction=Math.Atan2(r.Velocity.Y,r.Velocity.X);
        double course=BikeAngles.Wrap(current.Course-direction)+
            Math.Atan((r.Road.OffsetMeters-targetOffset+.24*(vOut-current.Outward))/(7+.27*v));
        double correction=2.25*course+.36*(current.Yaw-r.AngularVelocity)+.6*(current.Beta-r.SlideRadians);
        if(move.DriftFeedback!=0&&where.Current is {} bend&&bend.Exit-s<=49)
        {
            // Velocity relative to the next straight, observed while the bike
            // is still in the bend. It changes the steering target, not yaw.
            var exitRoad=map.Locate(bend.Exit+1).Road;
            double exitDrift=Vector2.Dot(r.Velocity,exitRoad.Outward);
            correction+=move.DriftFeedback*Math.Clamp(exitDrift,-6,6);
        }
        double wheelbase=bike.FrontAxleFromComMeters+bike.RearAxleFromComMeters;
        double requested=v<4?turn.FeedForward.SteeringRadians:
            turn.FeedForward.SteeringRadians+Math.Atan(wheelbase*(correction+move.YawBias)/v);
        if(v>=4&&move.StraightYawTarget>0)
        {
            double predicted=s+v*Math.Max(0,.06+move.SteeringLeadSeconds);
            foreach(var window in corridor.Windows)
            {
                if(predicted<window.Start+9||predicted>window.Start+54) continue;
                double cap=Math.Atan(wheelbase*move.StraightYawTarget/v);
                requested=Math.Clamp(requested,-cap,cap);
                break;
            }
        }
        double gas=power.FeedForward.Drive+move.DriveBias+.11*(current.Speed-v);
        ThrottleTrace209.Current?.Base(power.FeedForward.Drive,move.DriveBias,.11*(current.Speed-v),
            current.Speed,power.Speed,move.ThrottleCeiling,gas);
        if(move.ThrottleCeiling is {} ceiling)
        {
            double before=gas;
            gas=Math.Min(gas,ceiling);
            ThrottleTrace209.Current?.Add("207 ceiling",before,gas);
        }
        ThrottleTrace209.Current?.Add("207 actuator bound",gas,Math.Clamp(gas,0,1));
        return new((float)Math.Clamp(requested,-bike.MaximumSteeringRadians,bike.MaximumSteeringRadians),
            (float)Math.Clamp(gas,0,1));
    }

    private RouteKnot AtLocal(RouteKnot[] route,double local)
    {
        local=Math.Clamp(local,route[0].Local,route[^1].Local);
        int a=0,b=route.Length-1;
        while(b-a>1)
        {
            int mid=(a+b)/2;
            if(route[mid].Local<local) a=mid;
            else b=mid;
        }
        var x=route[a];
        var y=route[b];
        double u=(local-x.Local)/Math.Max(.001,y.Local-x.Local);
        double L(double from,double to)=>from+(to-from)*u;
        return new(local,L(x.Offset,y.Offset),BikeAngles.Wrap(x.Course+u*BikeAngles.Wrap(y.Course-x.Course)),
            L(x.Yaw,y.Yaw),L(x.Beta,y.Beta),L(x.Outward,y.Outward),L(x.Speed,y.Speed),
            new((float)L(x.FeedForward.SteeringRadians,y.FeedForward.SteeringRadians),
                (float)L(x.FeedForward.Drive,y.FeedForward.Drive)));
    }
}
