using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Frozen #101 inputs. No fitted racing results or production geometry changes.</summary>
public enum BendWidthProfile { Existing10Meters, ProgressiveSymmetric, ProgressiveMotoarena }
public sealed record TrackShapeVariant(string Name, double FirstCompression, double SecondCompression, bool Circular = false,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    BendWidthProfile WidthProfile = BendWidthProfile.Existing10Meters)
{
    public static TrackShapeVariant[] All() => new[] {
        new TrackShapeVariant("smooth-width-stadium",0,0,true), new("round-smooth",0,0),
        new("egg-mild",.2,.2), new("egg-medium",.35,.35), new("egg-strong",.5,.5), new("egg-asymmetric",.2,.4) };
    public static TrackShapeVariant[] ProgressiveWidths()=>new[]{
        new TrackShapeVariant("progressive-b",.2,.2,false,BendWidthProfile.ProgressiveSymmetric),
        new("progressive-c",.2,.2,false,BendWidthProfile.ProgressiveMotoarena)};
}

/// <summary>Immutable C2 arc-length curve, bounded nearest-point search. Motoarena experiment only.
/// k(t) = pi / (L I) * smoothRamp(t) * (1+c cos(2 pi t)).
/// The first/last 5% ramps have zero value and slope at the straight join.
/// Quintic Hermite pieces match position, unit tangent and curvature at both ends.</summary>
public sealed class ExperimentalTrackShape
{
    public const int BendIntervals = 4096;
    public const double RampFraction = .05, WidthTransitionMeters = 10;
    public TrackShapeVariant Variant { get; }
    public IReadOnlyList<double> Starts { get; }
    public double LapMeters => Starts[^1];
    public double HeightMeters { get; }
    public double FirstExtentMeters => first.Extent;
    public double SecondExtentMeters => second.Extent;
    private readonly Bend first, second;
    private readonly double[] lengths;
    public readonly record struct Sample(MeterPoint Position, double Heading, double Curvature);
    public ExperimentalTrackShape(TrackShapeVariant variant, double baselineLapMeters)
    {
        if (!TrackShapeVariant.All().Concat(TrackShapeVariant.ProgressiveWidths()).Contains(variant) || !double.IsFinite(baselineLapMeters) || Math.Abs(baselineLapMeters-318.77875)>.001)
            throw new ArgumentException("Use a frozen Motoarena geometry variant.");
        Variant=variant;
        var one=Nodes(variant.FirstCompression,variant.Circular);var two=Nodes(variant.SecondCompression,variant.Circular);
        var total=baselineLapMeters-124;
        HeightMeters=total/(1/one[^1].P.Y+1/two[^1].P.Y);
        var l1=HeightMeters/one[^1].P.Y;var l2=total-l1;
        first=new(one,l1,new(35,0),1);second=new(two,l2,new(-27,HeightMeters),-1);
        lengths=new[]{35d,l1/3,l1/3,l1/3,62,l2/3,l2/3,l2/3,27};
        var starts=new double[10];for(var i=0;i<9;i++)starts[i+1]=starts[i]+lengths[i];Starts=Array.AsReadOnly(starts);
    }
    private static double Ramp(double t) {var z=Math.Min(t,1-t)/RampFraction;return z>=1?1:z*z*z*(10+z*(-15+6*z));}
    private static double Profile(double t,double c,bool circular)=>circular?1:Ramp(t)*(1+c*Math.Cos(2*Math.PI*t));
    private readonly record struct Node(MeterPoint P, double Heading, double Curvature);
    private static Node[] Nodes(double c,bool circular)
    {
        const int n=BendIntervals;var h=1d/n;double integral=0;
        for(var i=0;i<n;i++)integral+=h/6*(Profile(i*h,c,circular)+4*Profile((i+.5)*h,c,circular)+Profile((i+1)*h,c,circular));
        double K(double t)=>Math.PI*Profile(t,c,circular)/integral;
        var nodes=new Node[n+1];double x=0,y=0,heading=0;nodes[0]=new(new(0,0),0,K(0));
        for(var i=0;i<n/2;i++) {
            var t=i*h;var middle=heading+h/12*(K(t)+4*K(t+h/4)+K(t+h/2));
            var end=heading+h/6*(K(t)+4*K(t+h/2)+K(t+h));
            x+=h/6*(Math.Cos(heading)+4*Math.Cos(middle)+Math.Cos(end));
            y+=h/6*(Math.Sin(heading)+4*Math.Sin(middle)+Math.Sin(end));
            heading=end;nodes[i+1]=new(new(x,y),heading,K(t+h));
        }
        var height=2*y;nodes[n/2]=nodes[n/2] with{Heading=Math.PI/2};
        for(var i=n/2+1;i<=n;i++){var mirror=nodes[n-i];nodes[i]=new(new(mirror.P.X,height-mirror.P.Y),Math.PI-mirror.Heading,mirror.Curvature);}
        return nodes;
    }
    private sealed class Piece
    {
        private readonly MeterPoint a,b,c,d,e,f;
        public MeterPoint Start=>a;
        public MeterPoint End { get; }
        public Piece(Node p,Node q,double h)
        {
            static MeterPoint Tangent(double heading)=>new(Math.Cos(heading),Math.Sin(heading));
            static MeterPoint Acceleration(Node v)=>new(-Math.Sin(v.Heading)*v.Curvature,Math.Cos(v.Heading)*v.Curvature);
            a=p.P;b=Tangent(p.Heading)*h;c=Acceleration(p)*(h*h/2);
            var delta=q.P-a-b-c;var velocity=Tangent(q.Heading)*h-b-c*2;var acceleration=Acceleration(q)*(h*h)-c*2;
            d=delta*10-velocity*4+acceleration*.5;e=delta*(-15)+velocity*7-acceleration;f=delta*6-velocity*3+acceleration*.5;End=q.P;
        }
        public (MeterPoint P,MeterPoint D,MeterPoint DD) Evaluate(double u)
            =>(a+(b+(c+(d+(e+f*u)*u)*u)*u)*u,b+(c*2+(d*3+(e*4+f*(5*u))*u)*u)*u,c*2+(d*6+(e*12+f*(20*u))*u)*u);
    }
    private sealed class Bend
    {
        private readonly Piece[] pieces;
        private readonly Box[] boxes;
        private readonly double h;
        public double Extent { get; }
        private readonly record struct Box(double MinX,double MinY,double MaxX,double MaxY,int First,int Count)
        {
            public double DistanceSquared(MeterPoint p){var x=Math.Max(0,Math.Max(MinX-p.X,p.X-MaxX));var y=Math.Max(0,Math.Max(MinY-p.Y,p.Y-MaxY));return x*x+y*y;}
        }
        public Bend(Node[] unit,double length,MeterPoint origin,int direction)
        {
            h=length/BendIntervals;Extent=unit[BendIntervals/2].P.X*length;
            var nodes=unit.Select(p=>new Node(origin+p.P*(length*direction),p.Heading+(direction<0?Math.PI:0),p.Curvature/length)).ToArray();
            pieces=new Piece[BendIntervals];for(var i=0;i<pieces.Length;i++)pieces[i]=new(nodes[i],nodes[i+1],h);
            boxes=new Box[BendIntervals*2];Build(1,0,pieces.Length);
        }
        private Box Build(int node,int start,int count)
        {
            // Unit-speed |P''| <= 0.1/m for all frozen variants; sag <= k*h²/8.
            const double maximumCurvature=.1;var pad=maximumCurvature*h*h/8+1e-8;
            Box box;
            if(count==1){var a=pieces[start].Start;var b=pieces[start].End;box=new(Math.Min(a.X,b.X)-pad,Math.Min(a.Y,b.Y)-pad,Math.Max(a.X,b.X)+pad,Math.Max(a.Y,b.Y)+pad,start,count);}
            else {var a=Build(node*2,start,count/2);var b=Build(node*2+1,start+count/2,count/2);box=new(Math.Min(a.MinX,b.MinX),Math.Min(a.MinY,b.MinY),Math.Max(a.MaxX,b.MaxX),Math.Max(a.MaxY,b.MaxY),start,count);}
            return boxes[node]=box;
        }
        public Sample At(double s)
        {
            var z=Math.Clamp(s/h,0,pieces.Length);var i=Math.Min((int)z,pieces.Length-1);var(p,d,dd)=pieces[i].Evaluate(z-i);
            var square=MeterPoint.Dot(d,d);return new(p,Math.Atan2(d.Y,d.X),MeterPoint.Cross(d,dd)/(square*Math.Sqrt(square)));
        }
        public double Project(MeterPoint p)
        {
            var best=double.PositiveInfinity;var bestS=0d;
            void Visit(int node){var box=boxes[node];if(box.DistanceSquared(p)>best)return;
                if(box.Count==1){var piece=pieces[box.First];var chord=piece.End-piece.Start;
                    var u=Math.Clamp(MeterPoint.Dot(p-piece.Start,chord)/MeterPoint.Dot(chord,chord),0,1);
                    for(var it=0;it<5;it++){var(q,d,dd)=piece.Evaluate(u);var delta=q-p;var denominator=MeterPoint.Dot(d,d)+MeterPoint.Dot(delta,dd);
                        if(Math.Abs(denominator)<1e-15)break;var next=Math.Clamp(u-MeterPoint.Dot(delta,d)/denominator,0,1);if(Math.Abs(next-u)<1e-12)break;u=next;}
                    var diff=piece.Evaluate(u).P-p;var score=MeterPoint.Dot(diff,diff);if(score<best){best=score;bestS=(box.First+u)*h;}return;}
                var left=node*2;var right=left+1;if(boxes[left].DistanceSquared(p)>boxes[right].DistanceSquared(p))(left,right)=(right,left);Visit(left);Visit(right);}
            Visit(1);return bestS;
        }
    }
    public Sample At(double s)
    {
        s=((s%LapMeters)+LapMeters)%LapMeters;
        if(s<Starts[1])return new(new(s,0),0,0);
        if(s<Starts[4])return first.At(s-Starts[1]);
        if(s<Starts[5])return new(new(35-(s-Starts[4]),HeightMeters),Math.PI,0);
        if(s<Starts[8])return second.At(s-Starts[5]);
        return new(new(-27+s-Starts[8],0),0,0);
    }
    public double Project(MeterPoint p)
    {
        var best=double.PositiveInfinity;var result=0d;
        void Candidate(double s){var delta=p-At(s).Position;var square=MeterPoint.Dot(delta,delta);if(square<best){best=square;result=s;}}
        Candidate(p.X>=0?Math.Clamp(p.X,0,35):Starts[8]+Math.Clamp(p.X+27,0,27));
        Candidate(Starts[4]+Math.Clamp(35-p.X,0,62));
        Candidate(Starts[1]+first.Project(p));Candidate(Starts[5]+second.Project(p));return result;
    }
    public double Width(double s)
    {
        if(Variant.WidthProfile!=BendWidthProfile.Existing10Meters)return ProgressiveWidth(s).Width;
        s=((s%LapMeters)+LapMeters)%LapMeters;double d;
        if(s>=Starts[1]&&s<=Starts[4])d=Math.Min(s-Starts[1],Starts[4]-s);
        else if(s>=Starts[5]&&s<=Starts[8])d=Math.Min(s-Starts[5],Starts[8]-s);else return 12;
        var t=Math.Clamp(d/WidthTransitionMeters,0,1);return 12+4.6*t*t*t*(10+t*(-15+6*t));
    }
    public double MaximumPhysicalWidthMeters=>Variant.WidthProfile==BendWidthProfile.ProgressiveMotoarena?17:16.6;
    /// <summary>Two quintic smoothsteps. Derivatives are with respect to reference metres.
    /// Width, first and second derivatives match at both joins and the midpoint.</summary>
    public (double Width,double First,double Second) ProgressiveWidth(double s)
    {
        if(!double.IsFinite(s))throw new ArgumentOutOfRangeException(nameof(s));
        s=((s%LapMeters)+LapMeters)%LapMeters;
        var bend=s>=Starts[1]&&s<=Starts[4]?0:s>=Starts[5]&&s<=Starts[8]?1:-1;
        if(bend<0)return(12,0,0);
        var start=Starts[bend==0?1:5];var end=Starts[bend==0?4:8];var length=end-start;
        var progress=(s-start)/length;var rising=progress<=.5;var u=2*(rising?progress:1-progress);
        var amplitude=Variant.WidthProfile==BendWidthProfile.ProgressiveMotoarena?(bend==0?5:4.2):4.6;
        var rate=2/length;
        return(12+amplitude*u*u*u*(10+u*(-15+6*u)),
            amplitude*30*u*u*(1-u)*(1-u)*rate*(rising?1:-1),
            amplitude*60*u*(1-u)*(1-2*u)*rate*rate);
    }
}
