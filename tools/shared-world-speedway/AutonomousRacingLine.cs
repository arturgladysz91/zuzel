using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public enum AutonomousLinePhase { Approach, Entry, Apex, Exit }
public sealed record AutonomousLineObservation(double TimeSeconds,int RiderId,AutonomousLinePhase Phase,
    double HorizonSeconds,float TargetMeters,bool Available,double BaselineProgress,double? CandidateProgress,string Reason);

/// <summary>Three own-control alternatives using the existing analytic envelope. No world rollout.</summary>
public static class AutonomousRacingLine
{
    public const float LineStepMeters=1.5f;
    public const double MinimumGainMetersPerSecond=.15,MaximumHorizonSeconds=12,ExitStraightMeters=20;
    private const double ReviewSeconds=.5,ResponseTimeConstants=3,MinimumSpeed=8,FollowingGapMeters=10;
    public static AutonomousLinePhase Phase(RoadPoint road)=>!road.IsTurn?AutonomousLinePhase.Approach
        :road.CornerProgress<1d/3?AutonomousLinePhase.Entry:road.CornerProgress<2d/3?AutonomousLinePhase.Apex:AutonomousLinePhase.Exit;

    public static double HorizonSeconds(RiderView own,RiderSpec spec,TrackFrame frame)
    {
        var distance=frame.DistanceToCurvatureChange(own.Road);
        // The helper returns physical arc length in a bend; Progress projects
        // in inner-reference metres, so convert before dividing by reference rate.
        if(own.Road.IsTurn)distance*=frame.Embedding.Segments[own.Road.Segment].InnerRadiusMeters/own.Road.RadiusMeters;
        if(!own.Road.IsTurn) {
            var next=(own.Road.Segment+1)%frame.Track.Segments.Count;
            for(var n=0;n<frame.Track.Segments.Count&&frame.Track.Segments[next].Type==SegmentType.Straight;n++)
                next=(next+1)%frame.Track.Segments.Count;
            var corner=frame.Track.CornerTopology.CornerForSegment(next);
            if(corner is null)return 0;
            for(var i=corner.StartSegmentIndex;i<corner.StartSegmentIndex+corner.SegmentCount;i++) {
                var s=frame.Embedding.Segments[i];distance+=s.InnerRadiusMeters*s.TurnAngleRadians;
            }
        }
        var horizon=(distance+ExitStraightMeters)/Math.Max(1,frame.ReferenceSpeed(own))-RiderController.ReactionSeconds(spec);
        return horizon>MaximumHorizonSeconds?0:Math.Max(2,horizon);
    }
    public static DecisionEvent? Observe(WorldSnapshot snapshot,RiderView own,RiderSpec spec,WorldParameters p,WorldWork work,
        TacticalMemory memory,TrackFrame frame,TrackStateSnapshot surface,Func<float,double,int,bool> corridor,
        Action<AutonomousLineObservation>? observe=null)
    {
        var watch=Stopwatch.GetTimestamp();
        try {return Choose();}finally{work.AutonomousElapsedMilliseconds+=Stopwatch.GetElapsedTime(watch).TotalMilliseconds;}
        DecisionEvent? Choose() {
            work.AutonomousObservations++;
            if(spec.Passive||own.Activity!=RiderActivity.Racing||own.Speed<MinimumSpeed||memory.Intent!=RacingIntent.None
                ||snapshot.TimeSeconds<memory.AutonomousReviewAt||snapshot.TimeSeconds<memory.SafetyUntil)return null;
            var reaction=RiderController.ReactionSeconds(spec);
            var front=TrafficThrottle.Front(snapshot,own,own.TargetOffsetMeters,reaction,p);
            if(front is {} g&&g.GapMeters<FollowingGapMeters&&g.LateralGapMeters<=TrafficThrottle.ClearanceMeters)return null;
            var horizon=HorizonSeconds(own,spec,frame);if(horizon==0)return null;
            var phase=Phase(own.Road);memory.AutonomousPhase=phase;
            memory.AutonomousReviewAt=snapshot.TimeSeconds+ReviewSeconds;
            work.AutonomousEvaluations++;
            double Progress(float target) {work.AutonomousCandidates++;return ContestScoring.Progress(own,spec,p,frame,surface,target,horizon);}
            var baseline=Progress(own.TargetOffsetMeters);var best=baseline;var chosen=own.TargetOffsetMeters;
            observe?.Invoke(new(snapshot.TimeSeconds,own.Id,phase,horizon,chosen,true,baseline,baseline,"Hold"));
            var movement=reaction+ResponseTimeConstants*p.LateralResponseSeconds;
            foreach(var direction in new[]{-1,1}) {
                var target=own.TargetOffsetMeters+direction*LineStepMeters;
                var free=target>=.5f&&target<=frame.MinimumUsableWidthMeters-.5f
                    &&corridor(target,movement,0)&&!TrafficThrottle.CrossesOccupied(snapshot,own,target,reaction,p);
                if(free)foreach(var peer in snapshot.Riders) {
                    if(peer.Id==own.Id||Vector2.DistanceSquared(own.Position,peer.Position)>225)continue;
                    var a=new PhysicalBikePose(own.Id,"shared-world",new(own.Position.X,own.Position.Y),
                        new(snapshot.TimeSeconds,own.Road.Heading,own.Heading),SpeedwayBikeDimensions.Reference);
                    var b=new PhysicalBikePose(peer.Id,"shared-world",new(peer.Position.X,peer.Position.Y),
                        new(snapshot.TimeSeconds,peer.Road.Heading,peer.Heading),SpeedwayBikeDimensions.Reference);
                    if(MechanicalSeparation.Between(a,b).SignedMeters<.3){free=false;break;}
                }
                if(!free) {work.AutonomousBlockedCandidates++;observe?.Invoke(new(snapshot.TimeSeconds,own.Id,phase,horizon,target,false,baseline,null,"OccupiedOrOutside"));continue;}
                var progress=Progress(target);observe?.Invoke(new(snapshot.TimeSeconds,own.Id,phase,horizon,target,true,baseline,progress,"Compared"));
                if(progress>best&&progress>baseline+MinimumGainMetersPerSecond*horizon){best=progress;chosen=target;}
            }
            if(chosen==own.TargetOffsetMeters)return null;
            work.AutonomousCommands++;
            memory.AutonomousReviewAt=snapshot.TimeSeconds+movement;
            return new(own.Id,0,TrafficAction.KeepIntent,snapshot.TimeSeconds,snapshot.TimeSeconds,snapshot.TimeSeconds+reaction,null,
                chosen,own.Drive,RacingIntent.None,"Autonomous"+phase,Channels:ControlChannels.Line);
        }
    }
}
