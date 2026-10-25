using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

public sealed record TrackKnowledgePoint134(double ReferenceMeters,double ReferenceDistanceMeters,double MetricDistanceAtCurrentOffsetMeters,
    double? ArrivalSecondsAtCurrentVelocity,int Segment,double Curvature,double WidthMeters,double Heading,double X,double Y,double OutwardX,double OutwardY);
public sealed record TrackKnowledge134(string Geometry,int Segment,double ReferenceMeters,double KnowledgeRangeMeters,
    double NextCornerStartReferenceMeters,double DistanceToNextCornerMeters,double? SecondsToNextCorner,double NextCornerLengthMeters,
    double NextCornerEndReferenceMeters,double FollowingStraightMeters,double FollowingCornerStartReferenceMeters,
    double? DistanceToCurrentCornerEndMeters,double ReactionDistanceMeters,double MaximumSteeringTravelSeconds,
    double ReachableSteeringLowRadians,double ReachableSteeringHighRadians,IReadOnlyList<TrackKnowledgePoint134> Profile){
    public int NextCornerDirection {get;init;}
}
public sealed record TrackReachability134(double OriginalGeometryCost,double ProfileGeometryCost,double ScoreDelta,double CoastDecelerationObserved,
    double ReactionSeconds,double TyreResponseSeconds,double YawAccelerationObserved,double CriticalDistanceMeters,double? SecondsToCriticalGeometry,
    double AttainableOuterOffsetEstimate,double SpeedHintMetersPerSecond,double ResidualSpeedMetersPerSecond,bool Applied,
    string Classification="uncertified heuristic; H09 safety unchanged");

/// <summary>Read-only spatial facts. Arrival times assume the current velocity;
/// they are estimates, never future dynamic observations or an authored line.</summary>
public static class TrackAwareness134
{
    public const double ProfileSpacingMeters=2;
    public static TrackKnowledge134 Observe(FreeRacingInput input,TrackFrame frame,WorldParameters p,double reaction=.24){
        var rider=input.Rider;var road=rider.Road;var total=rider.TotalReferenceMeters;
        var corners=frame.Track.CornerTopology.Corners.OrderBy(c=>c.StartSegmentIndex).ToArray();
        double Forward(double reference){var d=(reference-road.ReferenceMeters)%frame.LapMeters;return d<0?d+frame.LapMeters:d;}
        var next=corners.OrderBy(c=>Forward(frame.SegmentStartMeters(c.StartSegmentIndex))).First();
        var nextStart=frame.SegmentStartMeters(next.StartSegmentIndex);var distance=Forward(nextStart);
        var end=frame.SegmentStartMeters(next.StartSegmentIndex+next.SegmentCount);var length=end-nextStart;
        var following=corners.Where(c=>c!=next).OrderBy(c=>(frame.SegmentStartMeters(c.StartSegmentIndex)-end+frame.LapMeters)%frame.LapMeters).First();
        var followingStart=frame.SegmentStartMeters(following.StartSegmentIndex);
        var straight=(followingStart-end+frame.LapMeters)%frame.LapMeters;
        var current=frame.Track.CornerTopology.CornerForSegment(road.Segment);
        double? currentEnd=current is null?null:Forward(frame.SegmentStartMeters(current.StartSegmentIndex+current.SegmentCount));
        var along=Vector2.Dot(rider.Velocity,road.Tangent);
        var bike=p.TwoContactBike??TwoContactBikeParameters.Default;
        // The grid remains the inherited +/-15 degrees plus the current commands.
        var gridAngle=Math.Max(15*Math.PI/180,Math.Max(Math.Abs(input.Steering),Math.Abs(input.Active.SteeringRadians)));
        var travel=(gridAngle+Math.Abs(input.Steering))/bike.SteeringRateRadiansPerSecond;
        var physicalPreparation=reaction+FreeRacingModel.MaximumHorizonSeconds+p.LateralResponseSeconds+p.SlipYawResponseSeconds+travel;
        var adaptive=Math.Clamp(Math.Max(0,along)*physicalPreparation,20,60);
        var range=Math.Max(adaptive,distance+Math.Min(length,20));
        // A full next-corner descriptor is available even when its interior is
        // outside the dense diagnostic range. No future pose is synthesized.
        var ds=Enumerable.Range(0,(int)Math.Ceiling(range/ProfileSpacingMeters)+1).Select(i=>Math.Min(range,i*ProfileSpacingMeters))
            .Concat(frame.Track.Segments.Select((_,i)=>Forward(frame.SegmentStartMeters(i))).Where(d=>d<=range))
            .Append(distance).Append(Math.Min(range,distance+length)).Distinct().Order().ToArray();
        var points=new List<TrackKnowledgePoint134>();double metric=0;double previous=0;double heading=road.Heading;
        foreach(var d in ds){
            var sample=frame.RoadAt(total+d,0);var q=frame.At(total+d,0);var delta=BikeAngles.Wrap(sample.Heading-heading);
            metric+=d-previous+Math.Abs(delta)*Math.Max(0,road.OffsetMeters);heading=sample.Heading;previous=d;
            var k=sample.IsTurn&&double.IsFinite(sample.RadiusMeters)&&sample.RadiusMeters>0?1/sample.RadiusMeters:0;
            points.Add(new(sample.ReferenceMeters,d,metric,along>.1?metric/along:null,sample.Segment,k,frame.PhysicalWidthMeters(sample),sample.Heading,
                q.Position.X,q.Position.Y,sample.Outward.X,sample.Outward.Y));
        }
        var nextPoint=points.OrderBy(q=>Math.Abs(q.ReferenceDistanceMeters-distance)).First();
        var arrival=along>.1?nextPoint.MetricDistanceAtCurrentOffsetMeters/along:(double?)null;
        var steeringTime=arrival is{} t?Math.Max(0,t-reaction):0;
        var reachable=bike.SteeringRateRadiansPerSecond*steeringTime;
        var direction=frame.Shape is not null?Math.Sign(frame.Shape.At(nextStart+length/2).Curvature):Math.Sign(frame.RoadAt(nextStart+length/2,0).Heading-frame.RoadAt(nextStart,0).Heading);
        return new(frame.Shape?.Variant.Name??"native-arcs",road.Segment,total,range,nextStart,distance,arrival,length,end,straight,followingStart,currentEnd,
            Math.Max(0,along)*reaction,travel,Math.Max(-bike.MaximumSteeringRadians,input.Steering-reachable),Math.Min(bike.MaximumSteeringRadians,input.Steering+reachable),Array.AsReadOnly(points.ToArray())){NextCornerDirection=direction};
    }

    /// <summary>A soft continuation estimate using only the existing H09 held
    /// forecasts and static future geometry. It never changes a safety class.</summary>
    public static TrackReachability134 Evaluate(FreeRacingInput input,FreeLearningPolicy policy,TrackFrame frame,WorldParameters p,
        FreeLearningCandidate candidate,FreeRacingPrediction forecast,FreeRacingPrediction coast,double capacity,double oldCost){
        var end=forecast.Points[^1];var rider=end.Rider;var reaction=.24;
        var at=coast.Points.First(x=>x.Time>=input.Time+reaction-1e-8);var coastEnd=coast.Points[^1];
        var measuredTime=Math.Max(.01,coastEnd.Time-at.Time);
        var deceleration=Math.Max(0,(at.Rider.Speed-coastEnd.Rider.Speed)/measuredTime);
        var yawAuthority=forecast.Points.Zip(forecast.Points.Skip(1),(a,b)=>Math.Abs(b.Rider.AngularVelocity-a.Rider.AngularVelocity)/Math.Max(1e-6,b.Time-a.Time)).DefaultIfEmpty(0).Max();
        var futureInput=new FreeRacingInput(rider,end.Time,end.Omega,end.Steering,candidate.Action,Array.Empty<FreeRacingCommand>(),end.Time);
        var knowledge=Observe(futureInput,frame,p,reaction);
        var response=p.LateralResponseSeconds+p.SlipYawResponseSeconds;
        var outward=Vector2.Dot(rider.Velocity,rider.Road.Outward);double worst=0,critical=0,offsetHint=rider.Road.OffsetMeters,speedHint=rider.Speed;double? timeHint=null;
        foreach(var point in knowledge.Profile.Where(q=>q.Curvature>1e-7&&q.ArrivalSecondsAtCurrentVelocity is not null)){
            var t=point.ArrivalSecondsAtCurrentVelocity!.Value;
            var steerDelay=Math.Max(0,knowledge.MaximumSteeringTravelSeconds);
            var usable=Math.Max(0,t-reaction-steerDelay-response);
            // Optimistic spatial affordance, not a lateral destination. The
            // current velocity/yaw/tyre response constrain its approach. Beta
            // enters through actual velocity, with no direct slip reward/cost.
            var outer=Math.Clamp(rider.Road.OffsetMeters+outward*t+.5*capacity*usable*usable,0,Math.Max(0,point.WidthMeters-1-SpeedwayBikeDimensions.Reference.BoundingRadiusMeters));
            var curvature=1/(1/point.Curvature+outer);
            var hint=Math.Sqrt(.8*capacity/curvature);
            var desiredYaw=rider.Speed*curvature;
            var yawSeconds=yawAuthority>1e-6?Math.Abs(desiredYaw-rider.AngularVelocity)/yawAuthority:Math.Abs(desiredYaw-rider.AngularVelocity)>.01?double.PositiveInfinity:0;
            var available=Math.Max(0,t-reaction-Math.Min(response,yawSeconds));
            // Extrapolated coast response is explicitly heuristic. It comes
            // from the same-angle coast forecast, not an invented brake.
            var drop=deceleration*available;
            var residual=Math.Max(0,rider.Speed-drop-hint);var risk=residual*residual;
            if(risk>worst){worst=risk;critical=point.MetricDistanceAtCurrentOffsetMeters;timeHint=t;offsetHint=outer;speedHint=hint;}
        }
        var applied=candidate.Safety==FreeActionSafety.SafeInHorizon&&candidate.FutureClearance>=.2&&candidate.Progress>0;
        var newCost=applied?policy.GeometrySpeedWeight*worst:oldCost;
        return new(oldCost,newCost,newCost-oldCost,deceleration,reaction,response,yawAuthority,critical,timeHint,offsetHint,speedHint,Math.Sqrt(worst),applied);
    }
}
