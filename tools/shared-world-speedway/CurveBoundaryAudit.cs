using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Offline full-capsule distance to both continuous physical edges.
/// Edge chord spacing <= 5 cm; |edge''| < 2/m for frozen inputs gives < 0.625 mm sag.
/// An extra 0.7 mm is subtracted from every clearance, never added to acceptance.</summary>
internal sealed class CurveBoundaryAudit
{
    public const double SpacingMeters=.05, ChordAllowanceMeters=.0007;
    private readonly TrackFrame frame;
    private readonly Boundary inner,outer;
    public CurveBoundaryAudit(TrackFrame frame)
    {
        this.frame=frame;var count=(int)Math.Ceiling(frame.LapMeters/SpacingMeters);
        var a=new MeterPoint[count+1];var b=new MeterPoint[count+1];
        for(var i=0;i<=count;i++){var s=i*frame.LapMeters/count;var q=frame.Shape!.At(s);var n=new MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading));
            a[i]=q.Position-n;b[i]=q.Position+n*(frame.Shape.Width(s)-1);}
        a[^1]=a[0];b[^1]=b[0];inner=new(a);outer=new(b);
    }
    public (double Inner,double Outer) Clearance(RiderView v)
    {
        var bike=BikeFootprint.Create(new(v.Position.X,v.Position.Y),v.Heading,SpeedwayBikeDimensions.Reference);
        double inside=double.PositiveInfinity,outside=double.PositiveInfinity;
        void Capsule(MechanicalCapsule c){
            var margin=c.RadiusMeters+ChordAllowanceMeters+RacingLineGeometry.NumericalAllowanceMeters;
            inside=Math.Min(inside,inner.Distance(c.Start,c.End)-margin);outside=Math.Min(outside,outer.Distance(c.Start,c.End)-margin);
            void Sign(MeterPoint p){var road=frame.Project(new((float)p.X,(float)p.Y));
                if(road.OffsetMeters< -1)inside=Math.Min(inside,road.OffsetMeters+1-margin);
                if(road.OffsetMeters>frame.PhysicalWidthMeters(road)-1)outside=Math.Min(outside,frame.PhysicalWidthMeters(road)-1-road.OffsetMeters-margin);}
            Sign(c.Start);Sign(c.End);
        }
        Capsule(bike.Chassis);Capsule(bike.Handlebar);return(inside,outside);
    }
    internal double OuterPointDistance(MeterPoint p)
        =>outer.Distance(p,p)-ChordAllowanceMeters-RacingLineGeometry.NumericalAllowanceMeters;
    private sealed class Boundary
    {
        private readonly MeterPoint[] points;
        private readonly Box[] tree;
        private readonly record struct Box(double X0,double Y0,double X1,double Y1,int Start,int Count);
        public Boundary(MeterPoint[] points){this.points=points;tree=new Box[4*points.Length];Build(1,0,points.Length-1);}
        private Box Build(int node,int start,int count){Box box;if(count==1){var a=points[start];var b=points[start+1];box=new(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),start,count);}
            else{var a=Build(node*2,start,count/2);var b=Build(node*2+1,start+count/2,count-count/2);box=new(Math.Min(a.X0,b.X0),Math.Min(a.Y0,b.Y0),Math.Max(a.X1,b.X1),Math.Max(a.Y1,b.Y1),start,count);}return tree[node]=box;}
        public double Distance(MeterPoint a,MeterPoint b)
        {
            var best=double.PositiveInfinity;var x0=Math.Min(a.X,b.X);var x1=Math.Max(a.X,b.X);var y0=Math.Min(a.Y,b.Y);var y1=Math.Max(a.Y,b.Y);
            double Bound(int i){var box=tree[i];var x=Math.Max(0,Math.Max(box.X0-x1,x0-box.X1));var y=Math.Max(0,Math.Max(box.Y0-y1,y0-box.Y1));return x*x+y*y;}
            void Visit(int i){if(Bound(i)>=best)return;var box=tree[i];if(box.Count==1){best=Math.Min(best,SegmentSquared(a,b,points[box.Start],points[box.Start+1]));return;}
                var l=i*2;var r=l+1;if(Bound(l)>Bound(r))(l,r)=(r,l);Visit(l);Visit(r);}
            Visit(1);return Math.Sqrt(best);
        }
        private static double PointSquared(MeterPoint p,MeterPoint a,MeterPoint b){var d=b-a;var square=MeterPoint.Dot(d,d);var delta=p-a-d*(square==0?0:Math.Clamp(MeterPoint.Dot(p-a,d)/square,0,1));return MeterPoint.Dot(delta,delta);}
        private static double SegmentSquared(MeterPoint a,MeterPoint b,MeterPoint c,MeterPoint d){var u=b-a;var v=d-c;var cross=MeterPoint.Cross(u,v);
            if(cross!=0){var t=MeterPoint.Cross(c-a,v)/cross;var s=MeterPoint.Cross(c-a,u)/cross;if(t is >=0 and <=1&&s is >=0 and <=1)return 0;}
            return Math.Min(Math.Min(PointSquared(a,c,d),PointSquared(b,c,d)),Math.Min(PointSquared(c,a,b),PointSquared(d,a,b)));}
    }
}
