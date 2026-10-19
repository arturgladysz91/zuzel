using System.Globalization;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Read-only geometry export. Does not construct a world or execute a simulation.</summary>
public static class ReplayGeometry
{
    public const double MaximumReferenceSpacingMeters=.2;
    public static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public static ReplayGeometryData Create()
    {
        var frame=new TrackFrame(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        var segments=new List<ReplaySegment>();double start=0;
        foreach(var segment in frame.Embedding.Segments) {
            var length=segment.SegmentType==SegmentType.Straight?segment.StraightLengthMeters:segment.InnerRadiusMeters*segment.TurnAngleRadians;
            var count=(int)Math.Ceiling(length/MaximumReferenceSpacingMeters);
            var width=LaneModel.PhysicalTrackWidthMeters(segment.SegmentType,frame.Track.Geometry);
            var sections=new List<ReplaySection>();
            for(var i=0;i<=count;i++) {
                var p=(double)i/count;var sample=segment.Map(p,0);
                var normal=new MeterPoint(Math.Sin(sample.TangentHeadingRadians),-Math.Cos(sample.TangentHeadingRadians));
                sections.Add(new(start+p*length,Point(sample.Position),Point(normal),
                    Point(sample.Position+normal*(-TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters)),
                    Point(sample.Position+normal*(width-TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters))));
            }
            segments.Add(new(segment.SegmentIndex,segment.SegmentId,segment.SegmentType.ToString(),start,length,width,
                LaneModel.UsableRacingWidthMeters(segment.SegmentType,frame.Track.Geometry),sections.ToArray()));
            start+=length;
        }
        var bike=BikeFootprint.Create(new(0,0),0,SpeedwayBikeDimensions.Reference);
        ReplayCapsule Capsule(string name,MechanicalCapsule c)=>new(name,Point(c.Start),Point(c.End),c.RadiusMeters);
        return new(1,"MatchedVenueProfiles.CreateMotoarenaStandingStartTrack() / TrackFrame / TrackMetricEmbedding",frame.Embedding.FrameId,
            frame.LapMeters,MaximumReferenceSpacingMeters,TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters,frame.Track.Geometry,
            SpeedwayBikeDimensions.Reference,new[]{Capsule("Chassis",bike.Chassis),Capsule("Handlebar",bike.Handlebar)},
            new(){["activity"]=Names<RiderActivity>(),["action"]=Names<TrafficAction>(),["intent"]=Names<RacingIntent>(),
                ["channels"]=Names<ControlChannels>(),["severity"]=Names<CoreSim.Interactions.PhysicalContactSeverity>()},segments.ToArray());
    }
    public static ReplayGeometryData Create(TrackFrame frame)
    {
        if(frame.Shape is null)return Create();
        var source=Create();var segments=new List<ReplaySegment>();
        for(var i=0;i<frame.Track.Segments.Count;i++){
            var start=frame.SegmentStartMeters(i);var length=frame.SegmentLengthMeters(i);var count=(int)Math.Ceiling(length/MaximumReferenceSpacingMeters);
            var sections=new ReplaySection[count+1];
            for(var j=0;j<=count;j++){var s=start+(double)j/count*length;var q=frame.Shape.At(s);var n=new MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading));
                sections[j]=new(s,Point(q.Position),Point(n),Point(q.Position-n),Point(q.Position+n*(frame.Shape.Width(s)-1)));}
            var width=frame.Shape.Width(start+length/2);var segment=frame.Track.Segments[i];
            segments.Add(new(i,segment.Id,segment.Type.ToString(),start,length,width,width-2,sections));
        }
        return source with{Source="ExperimentalTrackShape / "+frame.Shape.Variant.Name,FrameId="track-shape-101:"+frame.Shape.Variant.Name,LapMeters=frame.LapMeters,Segments=segments.ToArray()};
    }
    private static ReplayPoint Point(MeterPoint p)=>new(p.X,p.Y);
    private static Dictionary<string,string> Names<T>() where T:struct,Enum=>Enum.GetValues<T>().ToDictionary(
        v=>Convert.ToInt32(v,CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),v=>v.ToString());
    public static string Serialize()=>JsonSerializer.Serialize(Create(),Json)+"\n";
    public static void Save(string path)=>File.WriteAllText(path,Serialize());
}
public sealed record ReplayPoint(double X,double Y);
public sealed record ReplaySection(double ReferenceMeters,ReplayPoint Reference,ReplayPoint Outward,ReplayPoint Inner,ReplayPoint Outer);
public sealed record ReplaySegment(int Index,int Id,string Type,double StartReferenceMeters,double LengthReferenceMeters,
    double PhysicalWidthMeters,double UsableWidthMeters,ReplaySection[] Sections);
public sealed record ReplayGeometryData(int Version,string Source,string FrameId,double LapMeters,double MaximumReferenceSpacingMeters,
    double InnerReferenceOffsetMeters,TrackGeometry Track,SpeedwayBikeDimensions Dimensions,ReplayCapsule[] Capsules,
    Dictionary<string,Dictionary<string,string>> Enums,ReplaySegment[] Segments);
public sealed record ReplayCapsule(string Component,ReplayPoint Start,ReplayPoint End,double RadiusMeters);
