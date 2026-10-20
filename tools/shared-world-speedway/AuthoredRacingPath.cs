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
