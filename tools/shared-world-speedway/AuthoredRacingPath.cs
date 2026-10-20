using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>#104 frozen geometric hypotheses, not a target-step plan or time optimization.</summary>
public sealed class AuthoredRacingPath
{
    public const double OutsideMeters=8.5, ApexMeters=2, PreparationMeters=31;
    public string Name {get;}
    public double ApexFraction {get;}
    public TrackFrame Frame {get;}
    public double? MatchedEntryOffset {get;}
    public double TighteningBeforeEntryMeters {get;}
    private readonly MeterPoint[][]? c3Pieces;
    private readonly double c3Spacing;
    private readonly bool crossoverWide;
    public bool IsCrossover=>c3Pieces is not null;
    public double C3KnotSpacingMeters=>c3Spacing;
    public static AuthoredRacingPath Crossover(bool wide)=>new(wide);
    private AuthoredRacingPath(bool wide):this("inner")
    {
        Name=wide?"crossover-wide-straight":"crossover-inner";crossoverWide=wide;
        // Same AuthoredRacingPath, full XY rather than only a smoother d(s).
        // Shared P/P'/P''/P''' at every periodic septic-Hermite knot guarantee C3.
        var count=(int)Math.Ceiling(Frame.LapMeters/1.0);c3Spacing=Frame.LapMeters/count;
        var jets=new (MeterPoint P,MeterPoint D,MeterPoint DD,MeterPoint DDD)[count];
        const double epsilon=.25;
        for(var i=0;i<count;i++){
            var s=i*c3Spacing;var a=RawCrossover(s-2*epsilon);var b=RawCrossover(s-epsilon);var c=RawCrossover(s);var d=RawCrossover(s+epsilon);var e=RawCrossover(s+2*epsilon);
            jets[i]=(c,(a-b*8+d*8-e)*(1/(12*epsilon)),(a*(-1)+b*16-c*30+d*16-e)*(1/(12*epsilon*epsilon)),(e-d*2+b*2-a)*(1/(2*epsilon*epsilon*epsilon)));
        }
        c3Pieces=new MeterPoint[count][];
        for(var i=0;i<count;i++){
            var a=jets[i];var b=jets[(i+1)%count];var h=c3Spacing;
            var coefficients=new MeterPoint[8];coefficients[0]=a.P;coefficients[1]=a.D*h;coefficients[2]=a.DD*(h*h/2);coefficients[3]=a.DDD*(h*h*h/6);
            var p=b.P-coefficients[0]-coefficients[1]-coefficients[2]-coefficients[3];var d=b.D*h-coefficients[1]-coefficients[2]*2-coefficients[3]*3;
            var dd=b.DD*(h*h)-coefficients[2]*2-coefficients[3]*6;var ddd=b.DDD*(h*h*h)-coefficients[3]*6;
            coefficients[4]=p*35-d*15+dd*2.5-ddd*(1d/6);coefficients[5]=p*(-84)+d*39-dd*7+ddd*.5;
            coefficients[6]=p*70-d*34+dd*6.5-ddd*.5;coefficients[7]=p*(-20)+d*10-dd*2+ddd*(1d/6);c3Pieces[i]=coefficients;
        }
    }
    private MeterPoint RawCrossover(double s)
    {
        var reference=Frame.Shape!.At(s);var offset=1d;
        if(crossoverWide){
            var half=Frame.LapMeters/2;var entry=Frame.SegmentStartMeters(1);var bend=Frame.SegmentStartMeters(4)-entry;
            var x=((s-entry)%half+half)%half;var apex=bend/2;var wide=bend+10;var returning=bend+25;var inner=half-5;
            static double S(double u){u=Math.Clamp(u,0,1);return u*u*u*u*(35+u*(-84+u*(70-20*u)));}
            if(x>=apex&&x<wide)offset=1+4*S((x-apex)/(wide-apex));
            else if(x>=wide&&x<returning)offset=5;
            else if(x>=returning&&x<inner)offset=5-4*S((x-returning)/(inner-returning));
        }
        return reference.Position+new MeterPoint(Math.Sin(reference.Heading),-Math.Cos(reference.Heading))*offset;
    }
    public (MeterPoint Position,MeterPoint First,MeterPoint Second,MeterPoint Third) C3Jet(double s)
    {
        if(c3Pieces is null||!double.IsFinite(s))throw new ArgumentException("C3 crossover path required.");
        var z=((s%Frame.LapMeters)+Frame.LapMeters)%Frame.LapMeters/c3Spacing;var i=Math.Min((int)z,c3Pieces.Length-1);var u=z-i;var c=c3Pieces[i];
        MeterPoint Derivative(int order){var value=new MeterPoint(0,0);for(var j=7;j>=order;j--){double factor=1;for(var k=0;k<order;k++)factor*=j-k;value=value*u+c[j]*factor;}return value*Math.Pow(c3Spacing,-order);}
        return(Derivative(0),Derivative(1),Derivative(2),Derivative(3));
    }
    public double CurvatureDerivative(double s)
    {
        var(_,d,dd,ddd)=C3Jet(s);var square=MeterPoint.Dot(d,d);var cross=MeterPoint.Cross(d,dd);
        return MeterPoint.Cross(d,ddd)/(square*square)-3*cross*MeterPoint.Dot(d,dd)/(square*square*square);
    }
    public static AuthoredRacingPath Matched(double entryOffset,double beforeEntry=0)
    {
        if(entryOffset is not (1 or 5 or 8.5)||beforeEntry is not (0 or 15))throw new ArgumentOutOfRangeException(nameof(entryOffset));
        return new(entryOffset,beforeEntry);
    }
    private AuthoredRacingPath(double entryOffset,double beforeEntry):this("inner")
    {
        Name=$"matched-{(entryOffset==1?"inner":entryOffset==5?"mid":"wide")}-{(beforeEntry==0?"entry":"early")}";
        MatchedEntryOffset=entryOffset;TighteningBeforeEntryMeters=beforeEntry;
    }
    public static string[] Names()=>new[]{"inner","outside-inside","outside-inside-outside","late-apex"};
    public AuthoredRacingPath(string name,TrackFrame? frame=null)
    {
        if(!Names().Contains(name))throw new ArgumentException("Use a frozen #104 path.",nameof(name));
        Name=name;ApexFraction=name=="late-apex"?.65:.5;
        Frame=frame??TrackGeometryAudit.Frame(TrackShapeVariant.ProgressiveWidths()[0]);
        if(Frame.Shape?.Variant!=TrackShapeVariant.ProgressiveWidths()[0])throw new ArgumentException("#104 freezes progressive-b.");
    }
    public (double D,double First,double Second) Offset(double s)
    {
        if(!double.IsFinite(s))throw new ArgumentOutOfRangeException(nameof(s));
        if(c3Pieces is not null){var q=At(s);return(q.OffsetMeters,q.OffsetFirst,q.OffsetSecond);}
        if(MatchedEntryOffset is { } entry){
            if(entry==1)return(1,0,0);
            var h=Frame.LapMeters/2;var begin=Frame.SegmentStartMeters(1);var bend=Frame.SegmentStartMeters(4)-begin;
            var matchedX=((s-(begin-TighteningBeforeEntryMeters))%h+h)%h;
            var matchedApex=bend/2+TighteningBeforeEntryMeters;var reset= bend+TighteningBeforeEntryMeters+15;
            if(matchedX<matchedApex)return Blend(entry,1,matchedX,matchedApex);
            if(matchedX<reset)return(1,0,0);
            var resetEnd=h-15+TighteningBeforeEntryMeters;
            if(matchedX<resetEnd)return Blend(1,entry,matchedX-reset,resetEnd-reset);
            return(entry,0,0);
        }
        if(Name=="inner")return(1,0,0);
        var half=Frame.LapMeters/2;var start=Frame.SegmentStartMeters(1);
        var x=((s-start)%half+half)%half;var length=Frame.SegmentStartMeters(4)-start;
        var apex=length*ApexFraction;
        if(x<apex)return Blend(OutsideMeters,ApexMeters,x,apex);
        if(Name=="outside-inside"){
            if(x<length)return(ApexMeters,0,0);
            if(x<length+PreparationMeters)return Blend(ApexMeters,OutsideMeters,x-length,PreparationMeters);
        }else if(x<length)return Blend(ApexMeters,OutsideMeters,x-apex,length-apex);
        return(OutsideMeters,0,0);
    }
    private static (double,double,double) Blend(double a,double b,double x,double length){var u=Math.Clamp(x/length,0,1);var delta=b-a;
        return(a+delta*u*u*u*(10+u*(-15+6*u)),delta*30*u*u*(1-u)*(1-u)/length,delta*60*u*(1-u)*(1-2*u)/(length*length));}
    public AuthoredPathSample At(double s)
    {
        if(c3Pieces is not null){
            var(position,d1,d2,_)=C3Jet(s);var reference=Frame.Shape!.At(s);var n=new MeterPoint(Math.Sin(reference.Heading),-Math.Cos(reference.Heading));var t=new MeterPoint(Math.Cos(reference.Heading),Math.Sin(reference.Heading));
            var delta=position-reference.Position;var k=reference.Curvature;var c3Kp=(Frame.Shape.At(s+.001).Curvature-Frame.Shape.At(s-.001).Curvature)/.002;
            var offset=MeterPoint.Dot(delta,n);var first=MeterPoint.Dot(d1,n)+k*MeterPoint.Dot(delta,t);
            var second=MeterPoint.Dot(d2,n)+2*k*MeterPoint.Dot(d1,t)-k+c3Kp*MeterPoint.Dot(delta,t)-k*k*offset;
            return new(s,position,offset,first,second,d1.Length,Math.Atan2(d1.Y,d1.X),MeterPoint.Cross(d1,d2)/Math.Pow(d1.Length,3));
        }
        var q=Frame.Shape!.At(s);var(d,dp,dpp)=Offset(s);
        var kp=(Frame.Shape.At(s+.001).Curvature-Frame.Shape.At(s-.001).Curvature)/.002;
        var a=1+q.Curvature*d;var metric=Math.Sqrt(a*a+dp*dp);
        var heading=q.Heading-Math.Atan2(dp,a);
        var curvature=(a*(q.Curvature*a-dpp)+dp*(kp*d+2*q.Curvature*dp))/(metric*metric*metric);
        var outward=new MeterPoint(Math.Sin(q.Heading),-Math.Cos(q.Heading));
        return new(s,q.Position+outward*d,d,dp,dpp,metric,heading,curvature);
    }
    public PathGuidanceSample Guidance(RiderView rider,WorldParameters parameters,bool preview)
    {
        var current=At(rider.TotalReferenceMeters);var curvature=current.Curvature;
        if(preview){
            // Keep exactly the historical reference lookahead, replacing only its
            // heading/arc length by the authored XY curve. Fixed Simpson quadrature.
            var ahead=Math.Max(.1,Frame.ReferenceSpeed(rider.Road,rider.Speed)*2*parameters.YawResponseSeconds);
            var next=At(rider.TotalReferenceMeters+ahead);double length=0;
            for(var i=0;i<8;i++){var a=rider.TotalReferenceMeters+i*ahead/8;var h=ahead/8;
                length+=h/6*(At(a).Metric+4*At(a+h/2).Metric+At(a+h).Metric);}
            curvature=BikeAngles.Wrap(next.Heading-current.Heading)/length;
        }
        return new(current.Position,current.Heading,curvature);
    }
}
public readonly record struct PathGuidanceSample(MeterPoint Position,double Heading,double FeedForwardCurvature)
{
    public MeterPoint Tangent=>new(Math.Cos(Heading),Math.Sin(Heading));
    public MeterPoint Outward=>new(Math.Sin(Heading),-Math.Cos(Heading));
}
public readonly record struct AuthoredPathSample(double ReferenceMeters,MeterPoint Position,double OffsetMeters,double OffsetFirst,
    double OffsetSecond,double Metric,double Heading,double Curvature)
{
    public MeterPoint Tangent=>new(Math.Cos(Heading),Math.Sin(Heading));
    public MeterPoint Outward=>new(Math.Sin(Heading),-Math.Cos(Heading));
}
