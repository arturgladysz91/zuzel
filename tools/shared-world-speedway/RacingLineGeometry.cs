using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Offline clearance against native straight/arc boundaries and explicit width-step connectors.
/// The connectors diagnose the current discontinuity; they do not smooth or change the track.</summary>
public sealed class RacingLineGeometry
{
    public const double NumericalAllowanceMeters = .0001;
    private sealed record Edge(MeterPoint A, MeterPoint B, MeterPoint Center, double Radius, double Sweep);
    private readonly List<Edge> inner = new(), outer = new();
    private readonly TrackFrame frame;
    private readonly CurveBoundaryAudit? curve;
    public RacingLineGeometry(TrackFrame frame)
    {
        this.frame = frame;
        if(frame.Shape is not null){curve=new(frame);return;}
        foreach (var s in frame.Embedding.Segments)
        {
            var start = s.Map(0,0); var end = s.Map(1,0);
            var n0 = new MeterPoint(Math.Sin(start.TangentHeadingRadians),-Math.Cos(start.TangentHeadingRadians));
            var n1 = new MeterPoint(Math.Sin(end.TangentHeadingRadians),-Math.Cos(end.TangentHeadingRadians));
            var width = LaneModel.PhysicalTrackWidthMeters(s.SegmentType,frame.Track.Geometry);
            var turn = s.SegmentType != SegmentType.Straight;
            var center = start.Position - n0 * s.InnerRadiusMeters;
            inner.Add(new(start.Position-n0,end.Position-n1,center,turn?s.InnerRadiusMeters-1:0,s.TurnAngleRadians));
            outer.Add(new(start.Position+n0*(width-1),end.Position+n1*(width-1),center,turn?s.InnerRadiusMeters+width-1:0,s.TurnAngleRadians));
        }
        var originals = outer.ToArray();
        for (var i=0;i<originals.Length;i++) {
            var a=originals[i].B;var b=originals[(i+1)%originals.Length].A;
            if (Length(a-b)>.001) outer.Add(new(a,b,default,0,0));
        }
    }
    public (double Inner, double Outer) Clearance(RiderView v)
    {
        if(curve is not null)return curve.Clearance(v);
        var bike=BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,SpeedwayBikeDimensions.Reference);
        double Against(List<Edge> edges)
        {
            var result=double.PositiveInfinity;
            foreach(var capsule in new[]{bike.Chassis,bike.Handlebar})
                foreach(var edge in edges) result=Math.Min(result,Distance(capsule.Start,capsule.End,edge)-capsule.RadiusMeters);
            return result-NumericalAllowanceMeters;
        }
        var inside=Against(inner);var outside=Against(outer);
        // A whole capsule outside a boundary can have positive unsigned distance.
        // Endpoint membership supplies its sign; wall intersections are already negative.
        foreach(var c in new[]{bike.Chassis,bike.Handlebar}) foreach(var p in new[]{c.Start,c.End}) {
            var road=frame.Project(new((float)p.X,(float)p.Y));
            inside=Math.Min(inside,road.OffsetMeters+1-c.RadiusMeters-NumericalAllowanceMeters);
            var width=LaneModel.PhysicalTrackWidthMeters(frame.Track.Segments[road.Segment].Type,frame.Track.Geometry);
            if(road.OffsetMeters>width-1)outside=Math.Min(outside,width-1-road.OffsetMeters-c.RadiusMeters-NumericalAllowanceMeters);
        }
        return(inside,outside);
    }
    // Query used only while constructing the optional #102 target table. Native arcs
    // include width-step connectors; shaped tracks use the same bounded edge BVH.
    internal double OuterPointDistance(MeterPoint p)
    {
        if(curve is not null)return curve.OuterPointDistance(p);
        var distance=double.PositiveInfinity;
        foreach(var edge in outer)distance=Math.Min(distance,Distance(p,p,edge));
        return distance-NumericalAllowanceMeters;
    }
    internal double InnerPointDistance(MeterPoint p)
    {
        if(curve is not null)return curve.InnerPointDistance(p);
        return inner.Min(e=>Distance(p,p,e))-NumericalAllowanceMeters;
    }
    private static double Length(MeterPoint p)=>Math.Sqrt(MeterPoint.Dot(p,p));
    private static double PointSegment(MeterPoint p,MeterPoint a,MeterPoint b)
    {
        var d=b-a;var square=MeterPoint.Dot(d,d);
        return Length(p-(a+d*(square==0?0:Math.Clamp(MeterPoint.Dot(p-a,d)/square,0,1))));
    }
    private static bool OnArc(MeterPoint p,Edge e)
    {
        var a=Math.Atan2(p.Y-e.Center.Y,p.X-e.Center.X)-Math.Atan2(e.A.Y-e.Center.Y,e.A.X-e.Center.X);
        a=(a%(2*Math.PI)+2*Math.PI)%(2*Math.PI);
        return a<=e.Sweep+1e-10;
    }
    private static double Distance(MeterPoint a,MeterPoint b,Edge e)
    {
        var d=b-a;
        if(e.Radius==0) {
            var u=e.B-e.A;var cross=MeterPoint.Cross(d,u);
            if(cross!=0) {
                var t=MeterPoint.Cross(e.A-a,u)/cross;var s=MeterPoint.Cross(e.A-a,d)/cross;
                if(t is >=0 and <=1&&s is >=0 and <=1)return 0;
            }
            return Math.Min(Math.Min(PointSegment(a,e.A,e.B),PointSegment(b,e.A,e.B)),
                Math.Min(PointSegment(e.A,a,b),PointSegment(e.B,a,b)));
        }
        double PointArc(MeterPoint p)=>OnArc(p,e)?Math.Abs(Length(p-e.Center)-e.Radius):Math.Min(Length(p-e.A),Length(p-e.B));
        var best=Math.Min(Math.Min(PointArc(a),PointArc(b)),Math.Min(PointSegment(e.A,a,b),PointSegment(e.B,a,b)));
        var square=MeterPoint.Dot(d,d);
        if(square==0)return best;
        var q=a+d*Math.Clamp(MeterPoint.Dot(e.Center-a,d)/square,0,1);
        if(OnArc(q,e))best=Math.Min(best,Math.Abs(Length(q-e.Center)-e.Radius));
        var relative=a-e.Center;var dot=MeterPoint.Dot(relative,d);
        var disc=dot*dot-square*(MeterPoint.Dot(relative,relative)-e.Radius*e.Radius);
        if(disc>=0)foreach(var t in new[]{(-dot-Math.Sqrt(disc))/square,(-dot+Math.Sqrt(disc))/square})
            if(t is >=0 and <=1&&OnArc(a+d*t,e))return 0;
        return best;
    }
}
