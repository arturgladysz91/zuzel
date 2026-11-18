using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal enum Intent191 { Pace, Follow, AttackInside, AttackOutside, PrepareExit, Defend, Abort, Recover }
internal sealed record Match191(int Id,double Along,double RelativeSpeed,double Lateral,double Accel);
internal sealed record Candidate191(Intent191 Intent,double Offset,double Length,double Risk,double Score,
    bool Feasible,string Reason,double Progress,double EndSpeed,double Edge,double PairGap,double RearUse);
internal sealed record Decision191(double Time,int Rider,Intent191 Intent,int Rival,double Target,
    bool Replanned,bool Tactical,string Reason,Match191[] Rivals,Candidate191[]? Options,
    double TacticMs,double PlannerMs,double Fast50Ms,HandlingDecision Control)
{
    public MotionObservation193? Motion { get; init; }
    public ExitObservation193? Exit { get; init; }
    public WideObservation194? Wide { get; init; }
}

internal sealed class Rival191(RiderView view,double acceleration,double frameRate,Track191 track)
{
    internal RiderView View => view;
    internal double Acceleration => acceleration;
    internal PhysicalBikePose At(double seconds)
    {
        var frame=track.Frame;
        double rate=Math.Max(0,frameRate);
        double end=Math.Max(0,rate+acceleration*seconds);
        double s=view.TotalReferenceMeters+seconds*.5*(rate+end);
        var road=frame.RoadAt(s,view.Road.OffsetMeters);
        double outward=Vector2.Dot(view.Velocity,view.Road.Outward);
        double offset=view.Road.OffsetMeters+outward*.55*(1-Math.Exp(-seconds/.55));
        offset=Math.Clamp(offset,.05,frame.PhysicalWidthMeters(road)-1.12);
        // Course from road plus a fading observed heading error. Predict yaw
        // deviations only over the short response transient.
        double drift=BikeAngles.Wrap(view.Heading-view.Road.Heading);
        double yawError=view.AngularVelocity-frameRate*frame.Shape!.At(view.Road.ReferenceMeters).Curvature;
        double heading=road.Heading+drift*Math.Exp(-seconds/.85)+Math.Clamp(yawError,-1,1)*seconds*Math.Exp(-seconds/.45);
        var xy=track.XY(s,offset);
        return new(view.Id,"traffic191",xy,new(0,heading-view.SlideRadians,heading),SpeedwayBikeDimensions.Reference);
    }
}

internal sealed class RaceMind191
{
    private const double Review=.05, FullHorizon=1.45, EvaluateStep=Review;
    private readonly Track191 track;
    private readonly TrackStateSnapshot surface;
    private readonly WorldParameters physics;
    private readonly RiderSpec spec;
    private readonly HandlingSettings handling;
    private readonly Schedule188 timing;
    private readonly HandlingController controller;
    private readonly Dictionary<int, (double Time,float Speed)> history=new();
    private readonly double grip;
    private readonly float mass=147,inertia=52.72097f;
    private readonly bool dynamic;
    private readonly ExitPlanner193? exitPlanner;
    private Curve191? current;
    private Motion191? motion;
    private double nextReview, appliedAt, nextSafety;
    private Intent191 intent;
    private int opponent;
    internal List<Decision191> Journal {get;}=[];

    internal RaceMind191(Track191 track,TrackStateSnapshot surface,WorldParameters physics,RiderSpec spec,
        HandlingSettings handling,Schedule188 timing,bool dynamic,bool experimentalExit=false)
    {
        this.track=track; this.surface=surface; this.physics=physics; this.spec=spec;
        this.handling=handling; this.timing=timing; this.dynamic=dynamic;
        controller=new(track.Frame,surface,physics,spec,mass,inertia,handling,new(),timing);
        if (experimentalExit)
            exitPlanner=new(track,surface,physics,spec,handling,timing,mass,inertia);
        grip=LoadBasedTyreGrip.Budget(mass,surface.SampleSurface(0,2),
            TwoContactBikeParameters.Default,physics.LoadBasedTyres).TotalCapacityNewtons*handling.GripScale/mass;
    }

    internal HandlingDecision Choose(FreeRacingInput input,CrankClutch144 shaft,double lean,WorldSnapshot snapshot)
    {
        var mine=input.Rider;
        long stamp=Stopwatch.GetTimestamp();
        var rivals=new List<Rival191>(3);
        foreach(var peer in snapshot.Riders)
        {
            if(peer.Id==mine.Id) continue;
            double accel=0;
            if(history.TryGetValue(peer.Id,out var prior) && input.Time>prior.Time)
                accel=Math.Clamp((peer.Speed-prior.Speed)/(input.Time-prior.Time),-4,5);
            history[peer.Id]=(input.Time,peer.Speed);
            rivals.Add(new(peer,accel*.60,track.Frame.ReferenceSpeed(peer),track));
        }
        var seen=rivals.Select(r=>new Match191(r.View.Id,r.View.TotalReferenceMeters-mine.TotalReferenceMeters,
            mine.Speed-r.View.Speed,r.View.Road.OffsetMeters-mine.Road.OffsetMeters,r.Acceleration)).ToArray();
        var lead=rivals.Where(r=>r.View.TotalReferenceMeters>mine.TotalReferenceMeters+.5 &&
                r.View.TotalReferenceMeters-mine.TotalReferenceMeters<23 &&
                Math.Abs(r.View.Road.OffsetMeters-mine.Road.OffsetMeters)<2)
            .MinBy(r=>r.View.TotalReferenceMeters);
        var back=rivals.Where(r=>mine.TotalReferenceMeters>r.View.TotalReferenceMeters &&
                mine.TotalReferenceMeters-r.View.TotalReferenceMeters<7 && r.View.Speed>mine.Speed+.25 &&
                Math.Abs(mine.Road.OffsetMeters-r.View.Road.OffsetMeters)<1.4)
            .MaxBy(r=>r.View.TotalReferenceMeters);
        var gap=TrafficThrottle.Front(snapshot,mine,(float)(current?.Target??mine.Road.OffsetMeters),Review,physics);
        bool urgent=(gap is {GapAfterReactionMeters:<2.7,ClosingMetersPerSecond:>.35} &&
            (current is null || Math.Abs(current.Target-mine.Road.OffsetMeters)<1.0)) ||
            mine.Activity==RiderActivity.Disturbed && intent!=Intent191.Recover;
        bool tactical=dynamic && (input.Time>=nextReview-1e-9 || urgent && input.Time>=nextSafety-1e-9)
            && (input.Time>=.35 || current is null);
        var role=mine.Activity==RiderActivity.Disturbed ? Intent191.Recover :
            lead is not null ? Intent191.Follow : back is not null ? Intent191.Defend : Intent191.Pace;
        if(!dynamic && gap is {CorridorOccupied:true,GapAfterReactionMeters:<9,ClosingMetersPerSecond:>.3})
            role=Intent191.Follow;
        double tacticMs=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;

        stamp=Stopwatch.GetTimestamp();
        var state=new HandlingForecast(mine,input.Time,input.Omega,input.Steering,lean,shaft.Omega);
        state=HandlingPreview.Advance(state,timing.Horizon(input,RiderController.ReactionSeconds(spec)),input.Active,
            input.Pending,surface.SampleSurface(0,2),handling,spec,physics,track.Frame,mass,inertia,shaft.Parameters);
        double activationMs=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
        stamp=Stopwatch.GetTimestamp();
        Candidate191[]? candidates=null;
        bool replaced=false;
        string reason="retained";
        if(!dynamic)
        {
            // Shared baseline uses the original FAST50 reference and its
            // ordinary tracking. Its traffic guard consumes observed gaps only.
            var saved=controller.ChooseAt(input,state,null);
            var available=TrafficThrottle.Front(snapshot,mine,
                (float)mine.Road.OffsetMeters,Review,physics);
            float drive=saved.Action.Drive;
            float steer=saved.Action.SteeringRadians;
            if(mine.Speed<16 && mine.TotalReferenceMeters<24)
                steer=0; // parallel starting corridors until approach
            if(available is {GapAfterReactionMeters:<8,ClosingMetersPerSecond:>.2} ahead)
            {
                drive=Math.Min(drive,(float)Math.Clamp((ahead.GapAfterReactionMeters-2.2)/6,0,1));
                reason="observed-follow";
            }
            saved=saved with{Action=new(steer,drive)};
            Journal.Add(new(input.Time,mine.Id,role,available?.OpponentId??0,saved.TargetOffset,false,false,reason,
                seen,null,tacticMs,0,activationMs+Stopwatch.GetElapsedTime(stamp).TotalMilliseconds,saved));
            return saved;
        }

        var v=state.Rider;
        double minLength=Math.Clamp(v.Speed*1.20,14,38);
        current??=new(v.TotalReferenceMeters,Lateral191.Rest(spec.OffsetMeters),spec.OffsetMeters,minLength);
        if(tactical)
        {
            nextReview=input.Time+.40;
            if(urgent) nextSafety=input.Time+.15;
            var choices=new List<(Curve191 Curve,Motion191 Reference,Candidate191 Evaluation)>();
            double span=Math.Clamp(v.Speed*1.48,19,44);
            double s=v.TotalReferenceMeters;
            var match=current.At(s);
            bool launch=v.TotalReferenceMeters<17 && input.Time<3;
            var bounds=track.Bounds(s+span);
            double nowTarget=current.Target;
            double lo=bounds.Minimum;
            double hi=bounds.Maximum;
            double scoreBias=(spec.Profile.Gameplay.Abilities.Attack-50)/98d;
            double defense=(spec.Profile.Gameplay.Abilities.Defense-50)/98d;
            double safeSpeed=lead is null?100:
                Math.Clamp(lead.View.Speed+(lead.View.TotalReferenceMeters-mine.TotalReferenceMeters-3.5)*.28,6,32);
            void Evaluate(double dest, double length,Intent191 goal,double limit=100,double drive=1,Curve191? retained=null)
            {
                dest=Math.Clamp(dest,lo,hi);
                var curve=retained??new Curve191(s,match,dest,length);
                double reserve=goal==Intent191.Recover?.85:1;
                var reference=new Motion191(track,curve,spec,state,mass,grip,goal.ToString(),limit,drive,reserve);
                var score=Forecast(state,input,reference,rivals,goal,scoreBias,defense,lead?.View.Id??0);
                choices.Add((curve,reference,score));
            }

            Evaluate(nowTarget,span,intent, intent is Intent191.Follow or Intent191.Abort ? safeSpeed : 100,retained:current);
            if(launch)
            {
                // Near-edge starts can use their actual capsule margin. A
                // gentle inward/outward move opens clearance before the turn.
                Evaluate(Math.Clamp(spec.OffsetMeters,lo,hi),span,Intent191.Pace);
            }
            else
            {
                if(urgent || role==Intent191.Recover)
                {
                    Evaluate(match.Offset,span*1.20,role==Intent191.Recover?Intent191.Recover:Intent191.Abort,
                        Math.Min(safeSpeed,v.Speed-(role==Intent191.Recover?2.0:.8)),.45);
                }
                if(lead is not null)
                {
                    double target=lead.View.Road.OffsetMeters;
                    double separation=1.48+Math.Abs(lead.View.SlideRadians)*.55;
                    Evaluate(match.Offset,span,Intent191.Follow,safeSpeed);
                    Evaluate(target-separation,span*.83,Intent191.AttackInside);
                    Evaluate(target+separation,span*.91,Intent191.AttackOutside);
                    Evaluate(target+Math.CopySign(separation,mine.Road.OffsetMeters-target),span*1.32,Intent191.PrepareExit);
                }
                else
                {
                    double k=track.Frame.Shape!.At(s+span*.7).Curvature;
                    double optimum=k>.01?5.0:5.65;
                    if(role==Intent191.Defend)
                        Evaluate(match.Offset,span*1.08,Intent191.Defend);
                    if(role!=Intent191.Recover)
                    {
                        Evaluate(Math.Clamp(optimum,nowTarget-1.35,nowTarget+1.35),span,Intent191.Pace);
                        Evaluate(nowTarget-.86,span*1.10,Intent191.Pace);
                        Evaluate(nowTarget+.86,span*1.10,Intent191.Pace);
                    }
                }
            }
            var incumbent=choices[0];
            var choice=choices.Where(x=>x.Evaluation.Feasible).OrderByDescending(x=>x.Evaluation.Score).FirstOrDefault();
            bool improvement=choice.Evaluation is not null &&
                (!incumbent.Evaluation.Feasible || choice.Evaluation.Score>incumbent.Evaluation.Score+.48) &&
                (launch || urgent || !incumbent.Evaluation.Feasible || input.Time-appliedAt>=.80);
            if(improvement)
            {
                current=choice.Curve; motion=choice.Reference;
                replaced=!ReferenceEquals(incumbent.Curve,current); appliedAt=input.Time;
                intent=choice.Evaluation!.Intent;
                opponent=lead?.View.Id??back?.View.Id??0;
                reason=incumbent.Evaluation.Feasible?"score-improvement":"incumbent-unreachable";
            }
            else if(!incumbent.Evaluation.Feasible && choice.Evaluation is null)
            {
                // Older commands remain irrevocable. The new request reduces
                // drive and preserves the incumbent geometry until reassessed.
                intent=Intent191.Abort; opponent=lead?.View.Id??0;
                motion=new(track,current,spec,state,mass,grip,intent.ToString(),
                    Math.Min(safeSpeed,v.Speed-.6),.20,.88);
                reason="no-certified-alternative";
            }
            else
            {
                motion=incumbent.Reference;
                reason=launch?"parallel-launch":"hysteresis";
            }
            if(urgent && !incumbent.Evaluation.Feasible && intent!=Intent191.Recover && intent!=Intent191.Abort)
                reason="emergency-replan";
            candidates=choices.Select(x=>x.Evaluation).ToArray();
        }
        motion??=new(track,current,spec,state,mass,grip,Intent191.Pace.ToString());
        double planningMs=Stopwatch.GetElapsedTime(stamp).TotalMilliseconds;
        stamp=Stopwatch.GetTimestamp();
        var reference=state.Rider.TotalReferenceMeters;
        var point=motion.At(reference);
        var aheadReference=reference+.19*state.Rider.Speed;
        var previewPoint=motion.At(aheadReference);
        var previewTarget=track.XY(aheadReference,previewPoint.Offset);
        var command=controller.ChooseAt(input,state,motion);
        var change=exitPlanner?.Choose(input,state,command.Action,motion,controller,rivals,shaft.Parameters);
        if (change is { Action: { } replacement })
            command=command with { Action=replacement };
        Journal.Add(new(input.Time,mine.Id,intent,opponent,motion.Target,replaced,tactical,reason,seen,candidates,
            tacticMs,planningMs,activationMs+Stopwatch.GetElapsedTime(stamp).TotalMilliseconds,command)
        {
            Motion=new(reference,point.Course,point.Curvature,previewPoint.Curvature,previewTarget.X,previewTarget.Y,motion.Curve.Length),
            Exit=change
        });
        return command;
    }

    private Candidate191 Forecast(HandlingForecast initial,FreeRacingInput input,Motion191 motion,
        IReadOnlyList<Rival191> rivals,Intent191 goal,double attack,double defend,int leading)
    {
        var frame=track.Frame;
        var state=initial;
        var nearby=rivals.Where(r=>Vector2.DistanceSquared(r.View.Position,input.Rider.Position)<58*58).ToArray();
        double risk=0,bikeGap=10,edge=10,maxRear=0,previousForceReserve=0;
        double previousGap=1;
        string reason="accepted";
        // First request becomes active only at the predicted deadline.
        // Later steps carry an active and a next action, one review apart.
        FreeRacingAction committed=controller.ChooseAt(input,initial,motion).Action;
        for(int i=0;i<Math.Round(FullHorizon/EvaluateStep);i++)
        {
            var queued=controller.ChooseAt(input,state,motion).Action;
            var before=state;
            var forcesRear=0d;
            state=HandlingPreview.Advance(state,EvaluateStep,committed,[],surface.SampleSurface(0,2),
                handling,spec,physics,frame,mass,inertia,LaunchParameters144.Default,
                (force,power)=>forcesRear=Math.Max(forcesRear,power.Wheel.RearUtilization));
            committed=queued;
            maxRear=Math.Max(maxRear,forcesRear);
            previousForceReserve+=Math.Max(0,forcesRear-.96);
            double s=state.Rider.TotalReferenceMeters;
            var r=state.Rider;
            double clearance=Math.Min(r.Road.OffsetMeters+1,frame.PhysicalWidthMeters(r.Road)-1-r.Road.OffsetMeters);
            double angle=r.Heading-r.Road.Heading;
            double sin=Math.Abs(Math.Sin(angle)), cos=Math.Abs(Math.Cos(angle));
            double bodyExtent=Math.Max(.90*sin+.15,.65*sin+.38*cos+.02);
            double reserve=.13+.012*r.Speed;
            double sweep=clearance-bodyExtent-reserve;
            edge=Math.Min(edge,sweep);
            if(sweep<.5)
            {
                reason="track-envelope"; break;
            }
            if(Math.Abs(r.SlideRadians)>.57 || r.Speed>8 && s<before.Rider.TotalReferenceMeters-.02)
            {
                reason="attitude-or-progress"; break;
            }
            var own=ContactStep191.Pose(r);
            foreach(var peer in nearby)
            {
                double at=state.Time-input.Time;
                var forecast=peer.At(at);
                if((forecast.Position-own.Position).Length>3.5) continue;
                double signed=MechanicalSeparation.Between(own,forecast).SignedMeters;
                bikeGap=Math.Min(bikeGap,signed);
                double buffer=.13+.24*Math.Max(0,at-.30);
                risk+=Math.Max(0,buffer+.5-signed)*EvaluateStep;
                if(signed<buffer && (at<=.46 || signed<-.04))
                    reason="occupied-corridor";
            }
            if(reason!="accepted") break;
            previousGap=Math.Min(previousGap,bikeGap);
        }

        double progress=state.Rider.TotalReferenceMeters-initial.Rider.TotalReferenceMeters;
        double end=state.Rider.Speed;
        double atEnd=state.Rider.TotalReferenceMeters+Math.Max(3,end*.75);
        double exitPace=motion.At(atEnd).Speed;
        double future=0;
        foreach(var peer in nearby)
        {
            var other=peer.View;
            double dt=state.Time-input.Time;
            double projected=other.TotalReferenceMeters+dt*frame.ReferenceSpeed(other)+.5*peer.Acceleration*dt*dt;
            double ahead=projected-state.Rider.TotalReferenceMeters;
            if(ahead>-.5 && ahead<15 && Math.Abs(motion.At(atEnd).Offset-other.Road.OffsetMeters)<1.30)
                future+=.13*(15-ahead);
        }
        double score=progress+.35*end+.08*Math.Min(end+1,exitPace)-5*risk-future
            -.14*Math.Abs(motion.Target-initial.Rider.Road.OffsetMeters)-.06*previousForceReserve
            + .08*Math.Min(2,previousGap);
        if(exitPlanner is not null && reason=="accepted")
            score-=.8*exitPlanner.ApproachValue(initial,state,motion);
        if(goal is Intent191.AttackInside or Intent191.AttackOutside) score+=.40+.45*attack;
        if(goal==Intent191.Defend) score+=.42+.40*defend;
        if(goal==Intent191.Recover && initial.Rider.Activity==RiderActivity.Disturbed) score+=2;
        return new(goal,motion.Target,motion.Curve.Length,risk,score,reason=="accepted",reason,
            progress,end,edge,bikeGap,maxRear);
    }
}

