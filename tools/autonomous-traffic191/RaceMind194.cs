using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Safety153;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record WideObservation194(double BendEntry, double BendExit, double Settle, double Preparation,
    double WideOffset, double ExitOffset, double? ProjectedSweep, double? ProjectedGap, int Candidates,
    double CourseRadians, double BetaRadians, double TrackCurvature, double? ForecastSpeed,
    double? ForecastCourse, double? ForecastYaw, double? ForecastLean, double? ForecastBeta,
    double? ForecastWheel, double? ForecastShaft, double? ForecastRearUse);

internal sealed class RaceMind194
{
    private const double MaxSteerStepRadians = .025;
    private const double MaxDriveStep = .18;
    private const double PlanPeriodSeconds = .60;
    private const double CloseReviewSeconds = .25;
    private const double ChassisAllowanceMeters = .62;
    private readonly WideTrack194 field;
    private readonly TrackFrame frame;
    private readonly TrackSurfaceState surface;
    private readonly WorldParameters physics;
    private readonly RiderSpec spec;
    private readonly HandlingSettings settings;
    private readonly Schedule188 timing;
    private readonly float mass, inertia;
    private readonly HandlingController fast50;
    private readonly double capacity, wheelbase;
    private readonly Dictionary<int,(double Time,float Speed,double Acceleration)> peers = [];
    private SectorPath194? path;
    private Motion191? motion;
    private double reviewAt;
    private double tacticAt;
    private double lastChangeAt = -10;
    private string planReason = "launch";
    private int rivalId;
    private static readonly double[] FootprintAngles = [-.13,0,.13];

    internal List<Decision191> Journal { get; } = [];

    private sealed record Candidate(SectorPath194 Path, Motion191 Motion, bool Safe,
        string Reason, double Edge, double Gap, HandlingForecast Terminal, double RearUse, double Score);

    internal RaceMind194(WideTrack194 field, TrackStateSnapshot surface, WorldParameters physics, RiderSpec spec,
        HandlingSettings settings, Schedule188 timing, float mass, float inertia)
    {
        this.field=field;
        frame=field.Frame;
        this.surface=surface.SampleSurface(0,2);
        this.physics=physics;
        this.spec=spec;
        this.settings=settings;
        this.timing=timing;
        this.mass=mass;
        this.inertia=inertia;
        var bike=TwoContactBikeParameters.Default;
        wheelbase=bike.FrontAxleFromComMeters+bike.RearAxleFromComMeters;
        capacity=LoadBasedTyreGrip.Budget(mass,this.surface,bike,physics.LoadBasedTyres).TotalCapacityNewtons *
            settings.GripScale/mass;
        fast50=new(frame,surface,physics,spec,mass,inertia,settings,new(),timing);
    }

    internal HandlingDecision Choose(FreeRacingInput input, CrankClutch144 shaft, double lean, WorldSnapshot snapshot)
    {
        long watch=Stopwatch.GetTimestamp();
        double delay=timing.Horizon(input,RiderController.ReactionSeconds(spec));
        var bike=Advance(new(input.Rider,input.Time,input.Omega,input.Steering,lean,shaft.Omega),delay,input.Active,
            input.Pending,shaft.Parameters);
        var v=bike.Rider;
        double s=v.TotalReferenceMeters, rate=Math.Max(1,frame.ReferenceSpeed(v));
        BendVisit bend=field.Sector(s,rate);
        double band=field.WideOffset(bend);
        double activationMs=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;

        watch=Stopwatch.GetTimestamp();
        var nearby=Observe(snapshot,v);
        double lookOffset=path?.At(s+rate*.55).Offset??v.Road.OffsetMeters;
        var front=TrafficThrottle.Front(snapshot,v,(float)lookOffset,delay,physics);
        double followSpeed=FollowingSpeed(front,nearby,v.Speed);
        double available=AvailableCorridor(nearby,v,band,rate);
        double tacticMs=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;

        watch=Stopwatch.GetTimestamp();
        bool recreate=path is null || path.Bend!=bend;
        bool immediate=front is {GapAfterReactionMeters:<2.1,ClosingMetersPerSecond:>.30};
        bool newReview=recreate || input.Time>=reviewAt || immediate && input.Time>=tacticAt;
        int evaluations=0;
        bool switched=false;
        Candidate? selected=null;
        if (newReview)
        {
            double period=nearby.Length>0?PlanPeriodSeconds*.75:PlanPeriodSeconds;
            reviewAt=input.Time+period;
            tacticAt=input.Time+CloseReviewSeconds;
            var initial=path?.At(s)??MeasuredLateral(v);
            // Anchor the exit before the apex. An already accepted path remains
            // the stitching state for any later defensive or recovery choice.
            double apex=FirstApexOffset(bend,initial.Offset,nearby);
            double hold=Math.Clamp(initial.Offset,.30,band);
            double recoveryLength=Math.Clamp(1.60*rate,19,38);
            bool roomToResume=s+recoveryLength<bend.NextEntry-Math.Clamp(WideTrack194.PrepareNextSeconds*rate,12,19);
            double destination=s>=bend.Exit-12 && path is not null && available>path.Target+.25 && !roomToResume
                ? path.Target:available;
            var proposals=new List<Candidate>(3);

            void Test(SectorPath194 proposal)
            {
                var profile=new Motion191(field.Track,proposal.At,spec,bike,mass,capacity,proposal.Intent.ToString(),approach:.95);
                proposals.Add(Predict(bike,input,profile,proposal,nearby,shaft.Parameters,rate,followSpeed));
                evaluations++;
            }

            bool mayRetain=!recreate && path is not null;
            if (mayRetain) Test(path!);
            if (recreate || path is null || Math.Abs(path.Target-destination)>.28)
            {
                var want=destination>=band-.25 ?
                    (front is not null && band>v.Road.OffsetMeters+.6?Intent191.AttackOutside:Intent191.PrepareExit):
                    front is not null?Intent191.AttackInside:Intent191.Defend;
                Test(new(field,bend,s,initial,rate,apex,destination,want,
                    s>=bend.Exit-12 && destination>initial.Offset+.35));
            }

            var leading = front is { } gap ? nearby.FirstOrDefault(p=>p.View.Id==gap.OpponentId) : null;
            if (proposals.All(x=>!x.Safe) && leading is not null)
            {
                double inside=Math.Clamp(Math.Min(hold,leading.View.Road.OffsetMeters-1.65),.55,Math.Max(.55,destination));
                Test(new(field,bend,s,initial,rate,Math.Min(apex,Math.Max(.85,inside)),
                    inside,Intent191.AttackInside,false));
            }
            if (proposals.All(x=>!x.Safe))
                Test(new(field,bend,s,initial,rate,Math.Clamp(hold,.65,band-.7),
                    Math.Clamp(hold,.65,band-.7),Intent191.Follow,false));

            var safe=proposals.Where(x=>x.Safe).ToArray();
            Candidate? incumbent=mayRetain?proposals[0]:null;
            // Wall clearance, contact and H25 feasibility exclude candidates.
            // An open wide corridor then outranks a faster middle lane.
            var preferred=safe.Where(x=>Math.Abs(x.Path.Target-destination)<.28).ToArray();
            var reserved=safe.Where(x=>x.Path.Target<=destination+.30).ToArray();
            selected=(preferred.Length>0?preferred:reserved.Length>0?reserved:safe).MaxBy(x=>x.Score);
            if (selected is not null && incumbent is { Safe:true } && !ReferenceEquals(incumbent.Path,selected.Path) &&
                input.Time-lastChangeAt<.60 && Math.Abs(incumbent.Path.Target-destination)<.28)
                selected=incumbent;

            if (selected is null)
            {
                // Preserve the physically safest forecast prefix, keep the pending
                // queue, and coast when there is no certified alternative.
                selected=proposals.MaxBy(x=>Math.Min(x.Edge-.5,x.Gap))!;
                planReason=selected.Reason;
            }
            else planReason=Math.Abs(selected.Path.Target-band)<.25?"wide-certified":"occupied-or-unreachable";
            switched=!ReferenceEquals(path,selected.Path);
            if (switched) lastChangeAt=input.Time;
            path=selected.Path;
            motion=selected.Motion;
            rivalId=front?.OpponentId??0;
        }
        double plannerMs=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;

        watch=Stopwatch.GetTimestamp();
        var current=path!;
        var envelope=motion!;
        var previous=input.Pending.LastOrDefault(p=>p.ActivationAt<=bike.Time+1e-9)?.Action??input.Active;
        double cap=followSpeed;
        if (planReason is "no-safe-corridor" or "wall-reserve" or "attitude")
            cap=Math.Min(v.Speed-.3,cap);
        var command=Request(input,bike,previous,envelope,cap);
        var startPoint=envelope.At(s);
        var ahead=envelope.At(s+v.Speed*.19);
        var marker=field.Track.XY(s+v.Speed*.19,ahead.Offset);
        var observation=new WideObservation194(current.Bend.Entry,current.Bend.Exit,current.Settle,current.Preparation,
            band,current.Target,selected?.Edge,selected?.Gap,evaluations,Course(v),v.SlideRadians,startPoint.Curvature,
            selected?.Terminal.Rider.Speed,selected is null?null:Course(selected.Terminal.Rider),selected?.Terminal.Rider.AngularVelocity,
            selected?.Terminal.Lean,selected?.Terminal.Rider.SlideRadians,selected?.Terminal.Wheel,selected?.Terminal.Shaft,
            selected?.RearUse);
        Journal.Add(new(input.Time,v.Id,current.Intent,rivalId,current.Target,switched,newReview,planReason,
            nearby.Select(o=>new Match191(o.View.Id,o.View.TotalReferenceMeters-s,v.Speed-o.View.Speed,
                o.View.Road.OffsetMeters-v.Road.OffsetMeters,o.Acceleration)).ToArray(),null,tacticMs,plannerMs,
            activationMs+Stopwatch.GetElapsedTime(watch).TotalMilliseconds,command)
        {
            Motion=new(s,startPoint.Course,startPoint.Curvature,ahead.Curvature,marker.X,marker.Y,current.Settle-s),
            Wide=observation
        });
        return command;
    }

    private double FirstApexOffset(BendVisit bend,double initial,Rival191[] nearby)
    {
        if (bend.Lap==0 && bend.Index==0)
            return Math.Clamp(.85*spec.OffsetMeters+.40,.85,8.6);
        // Parallel riders approach the turn in distinct corridors. A free rider
        // can move inward for the apex while retaining the downstream exit.
        return nearby.Length>0?Math.Clamp(initial,.85,8.8):Math.Clamp(.61*initial+1.05,.85,7.0);
    }

    private Lateral191 MeasuredLateral(RiderView body)
    {
        double along=Math.Max(4,Vector2.Dot(body.Velocity,body.Road.Tangent));
        double across=Vector2.Dot(body.Velocity,body.Road.Outward);
        double stretch=1+frame.Shape!.At(body.TotalReferenceMeters).Curvature*body.Road.OffsetMeters;
        return new(body.Road.OffsetMeters,Math.Clamp(stretch*across/along,-.18,.18),0);
    }

    private Rival191[] Observe(WorldSnapshot snapshot,RiderView own)
    {
        var rivals=new List<Rival191>(3);
        foreach (var peer in snapshot.Riders.OrderBy(p=>p.Id))
        {
            if (peer.Id==own.Id || peer.Activity==RiderActivity.Crashed) continue;
            double acceleration=0;
            if (peers.TryGetValue(peer.Id,out var earlier) && snapshot.TimeSeconds>earlier.Time)
                acceleration=.80*earlier.Acceleration+.20*Math.Clamp((peer.Speed-earlier.Speed)/
                    (snapshot.TimeSeconds-earlier.Time),-5,6);
            peers[peer.Id]=(snapshot.TimeSeconds,peer.Speed,acceleration);
            if (Vector2.DistanceSquared(peer.Position,own.Position)>37*37) continue;
            rivals.Add(new(peer,acceleration*.55,frame.ReferenceSpeed(peer),field.Track));
        }
        return rivals.ToArray();
    }

    private double AvailableCorridor(Rival191[] others,RiderView body,double band,double rate)
    {
        int outside=0;
        foreach (var peer in others)
        {
            var v=peer.View;
            double gap=v.TotalReferenceMeters-body.TotalReferenceMeters;
            double projected=gap+1.65*(frame.ReferenceSpeed(v)-rate);
            double outward=v.Road.OffsetMeters-body.Road.OffsetMeters;
            // Anticipate a simultaneous lane merge. Keep a lateral slot for any
            // peer already outside who can overlap before the exit settles.
            if (Math.Min(gap,projected)<9 && Math.Max(gap,projected)>-10 &&
                (outward>.35 || Math.Abs(outward)<=.35 && v.Id>body.Id))
                outside++;
        }
        return Math.Max(.70,band-1.83*outside);
    }

    private static double FollowingSpeed(TrafficGap? front,Rival191[] peers,double speed)
    {
        if (front is not { } g || g.GapAfterReactionMeters>=8.5) return 100;
        var target=peers.FirstOrDefault(p=>p.View.Id==g.OpponentId);
        if (target is null) return 100;
        double desired=target.View.Speed + .50*(g.GapAfterReactionMeters-3.0);
        if (g.GapAfterReactionMeters<1.2 && g.ClosingMetersPerSecond>.2) desired=Math.Min(desired,speed-.4);
        return Math.Max(3,desired);
    }

    private HandlingDecision Request(FreeRacingInput input,HandlingForecast state,FreeRacingAction last,
        Motion191 reference,double speedLimit)
    {
        var baseControl=fast50.ChooseAt(input,state,reference);
        var v=state.Rider;
        double s=v.TotalReferenceMeters;
        double speed=v.Speed;
        if (speed<5)
        {
            float launchGas=(float)Math.Min(baseControl.Action.Drive,Math.Clamp((speedLimit-speed)*.42+.25,0,1));
            return baseControl with { Action=new(0,Math.Clamp(launchGas,last.Drive-(float)MaxDriveStep,last.Drive+(float)MaxDriveStep)) };
        }
        var at=reference.At(s);
        double rate=Math.Max(5,frame.ReferenceSpeed(v));
        double k=.45*reference.At(s+.14*rate).Curvature+.55*reference.At(s+.26*rate).Curvature;
        double vOut=Vector2.Dot(v.Velocity,v.Road.Outward);
        double desiredOut=Vector2.Dot(v.Velocity,v.Road.Tangent)*Math.Tan(BikeAngles.Wrap(v.Road.Heading-at.Course));
        double offset=v.Road.OffsetMeters-at.Offset+.25*(vOut-desiredOut);
        double aim=BikeAngles.Wrap(at.Course+Math.Atan(offset/(7+.28*speed)));
        double use=Math.Clamp(k*speed*speed/capacity,-.90,.90);
        double desiredBeta=Math.Clamp(.081*use/Math.Sqrt(1-use*use)-.60*k,-.20,.20);
        double feed=speed*k;
        double yaw=feed+2.85*BikeAngles.Wrap(aim-Course(v))+
            2.05*(desiredBeta-v.SlideRadians)+.15*(feed-v.AngularVelocity);
        double steer=Math.Clamp(Math.Atan(wheelbase*yaw/speed),-.31,.31);
        steer=Math.Clamp(steer,last.SteeringRadians-MaxSteerStepRadians,last.SteeringRadians+MaxSteerStepRadians);
        double drive=Math.Min(baseControl.Action.Drive,Math.Clamp(.12+.30*(speedLimit-speed),0,1));
        drive=Math.Clamp(drive,last.Drive-MaxDriveStep,last.Drive+MaxDriveStep);
        return baseControl with { Action=new((float)steer,(float)Math.Clamp(drive,0,1)) };
    }

    private Candidate Predict(HandlingForecast initial,FreeRacingInput input,Motion191 profile,SectorPath194 proposal,
        Rival191[] opponents,LaunchParameters144 launch,double rate,double speedLimit)
    {
        // Screen the rest of the lateral path with a whole-frame bound. The
        // imminent section is rolled out using the original H25 force model.
        double start=initial.Rider.TotalReferenceMeters;
        double edge=10, pair=10, risk=0, rear=0;
        string reason="accepted";
        if (initial.Rider.Speed>12)
        {
            for (double s=start+Math.Max(9,rate*.6);s<=proposal.Preparation;s+=4)
            {
                var at=profile.At(s);
                var road=frame.RoadAt(s,at.Offset);
                double wall=frame.PhysicalWidthMeters(road)-1-at.Offset;
                double clearance=Math.Min(at.Offset+1,wall);
                edge=Math.Min(edge,clearance-ChassisAllowanceMeters);
                if (clearance<1.10 || Math.Abs(at.Curvature)>.070)
                {
                    reason="wall-reserve";
                    break;
                }
            }
        }

        var state=initial;
        if (reason=="accepted")
        {
            double step=timing.Period(input.Time);
            int count=(int)Math.Round(Math.Clamp(1.28-.012*Math.Max(0,rate-18),1.02,1.28)/step);
            var last=input.Pending.LastOrDefault(p=>p.ActivationAt<=initial.Time+1e-9)?.Action??input.Active;
            var active=Request(input,state,last,profile,speedLimit).Action;
            var oldGap=field.Boundary.Clearance(state.Rider);
            for (int i=0;i<count;i++)
            {
                var next=Request(input,state,active,profile,speedLimit).Action;
                double half=.02;
                foreach (double duration in new[] { half,step-half })
                {
                    var old=state;
                    state=Advance(state,duration,active,[],launch,(_,power)=>
                        rear=Math.Max(rear,power.Wheel.RearUtilization));
                    var gap=field.Boundary.Clearance(state.Rider);
                    edge=Math.Min(edge,Clearance153.SweepLowerBound(old.Rider,state.Rider,duration,oldGap,gap));
                    oldGap=gap;
                }
                var v=state.Rider;
                if (edge<.52) { reason="wall-reserve"; break; }
                if (Math.Abs(v.SlideRadians)>.56 || rear>1.000001) { reason="attitude"; break; }
                var pose=ContactStep191.Pose(v);
                double dt=state.Time-input.Time;
                foreach (var peer in opponents)
                {
                    var other=peer.At(dt);
                    if ((other.Position-pose.Position).Length>4.7) continue;
                    double separation=MechanicalSeparation.Between(pose,other).SignedMeters;
                    pair=Math.Min(pair,separation);
                    if (separation<.10+.30*dt)
                        reason="no-safe-corridor";
                    risk+=Math.Max(0,.9-separation)*step;
                }
                if (reason!="accepted") break;
                active=next;
            }
        }
        double score=state.Rider.TotalReferenceMeters-start+.26*state.Rider.Speed-4*risk
            -.12*Math.Abs(state.Rider.Road.OffsetMeters-profile.At(state.Rider.TotalReferenceMeters).Offset);
        return new(proposal,profile,reason=="accepted",reason,edge,pair,state,rear,score);
    }

    private HandlingForecast Advance(HandlingForecast state,double seconds,FreeRacingAction action,
        FreeRacingCommand[] pending,LaunchParameters144 launch,Action<TwoContactForces,ClutchStep144>? observe=null)
        => HandlingPreview.Advance(state,seconds,action,pending,surface,settings,spec,physics,frame,mass,inertia,launch,observe);

    private static double Course(RiderView v) => BikeAngles.Wrap(v.Heading-v.SlideRadians);
}

