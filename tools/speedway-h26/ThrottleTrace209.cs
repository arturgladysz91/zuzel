using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record ThrottleStage209(string Name,double Before,double After)
{
    public double Reduction=>Math.Max(0,Before-After);
}

// Optional command-local observer. Thread-local capture keeps parallel MODEL arms independent.
// No physical state, action, reference or floating-point expression is changed by capture.
internal sealed class ThrottleTrace209
{
    [ThreadStatic] internal static ThrottleTrace209? Current;
    public double FeedForward {get;private set;}
    public double DriveBias {get;private set;}
    public double SpeedFeedback {get;private set;}
    public double ReferenceSpeed {get;private set;}
    public double PowerReferenceSpeed {get;private set;}
    public double? Ceiling {get;private set;}
    public double RawRequested {get;private set;}
    public int? Visit {get;private set;}
    public double[]? PaceTerms {get;private set;}
    public double Ramp {get;private set;}
    public double RecoveryGas {get;private set;}
    public List<ThrottleStage209> Stages {get;}=[];
    public FreeRacingAction Selected {get;private set;}
    public string Dominant=>Stages.Where(x=>x.Reduction>1e-10).OrderByDescending(x=>x.Reduction)
        .Select(x=>x.Name).FirstOrDefault()??(DriveBias<0?"207 drive bias":SpeedFeedback<0?"207 speed feedback":"none");

    // Each value is the local selected-throttle change with only that restriction
    // removed, while every other clamp remains. These values are not additive.
    // In particular, a negative ramp masked by a zero pace cap has zero effective loss.
    public Dictionary<string,double> EffectiveRemovalDeltaAt208Output
    {
        get
        {
            string[] families=["feed-forward","drive bias","speed feedback","ceiling","coast","mid pace","exit pace","ramp","recovery gas"];
            double original=Reconstruct(null);
            return families.ToDictionary(x=>x,x=>Reconstruct(x)-original);
        }
    }
    public double Reconstructed208Requested=>Reconstruct(null);
    public string DominantEffective208Restriction=>EffectiveRemovalDeltaAt208Output.Where(x=>x.Value>1e-7)
        .OrderByDescending(x=>x.Value).Select(x=>x.Key).FirstOrDefault()??"none or jointly masked";
    private double Reconstruct(string? omit)
    {
        double q=(omit=="feed-forward"?1:FeedForward)+(omit=="drive bias"?Math.Max(0,DriveBias):DriveBias)+
            (omit=="speed feedback"?Math.Max(0,SpeedFeedback):SpeedFeedback);
        if(Ceiling is {} c&&omit!="ceiling")q=Math.Min(q,c);
        q=(float)Math.Clamp(q,0,1);
        if(PaceTerms is not {} p)return q;
        double coast=omit=="coast"?0:p[1],mid=omit=="mid pace"?0:p[2],exit=omit=="exit pace"?0:p[3];
        double speed=p[0]-coast-mid-exit;
        double v=Math.Max(3,ReferenceSpeed-SpeedFeedback/.11);
        double cap=Math.Clamp(.23+.17*(speed-v),0,1);
        double blend=ExitPolicy208.Gate((coast+mid+exit)/1.4);
        double drive=ExitPolicy208.Lerp(q,Math.Min(q,cap),blend);
        drive+=(omit=="ramp"?Math.Max(0,Ramp):Ramp)+(omit=="recovery gas"?Math.Max(0,RecoveryGas):RecoveryGas);
        return (float)Math.Clamp(ExitPolicy208.Lerp(q,drive,p[7]),0,1);
    }
    internal void ExitAdjustments(double ramp,double recovery) {Ramp=ramp;RecoveryGas=recovery;}

    internal void Base(double feed,double bias,double feedback,double reference,double power,double? ceiling,double raw)
    {
        FeedForward=feed;DriveBias=bias;SpeedFeedback=feedback;ReferenceSpeed=reference;
        PowerReferenceSpeed=power;Ceiling=ceiling;RawRequested=raw;
        Add("207 drive bias",feed,feed+bias);
        Add("207 speed feedback",feed+bias,raw);
    }
    internal void Add(string name,double before,double after)=>Stages.Add(new(name,before,after));
    internal void Pace(int visit,double original,double coast,double mid,double exit,double speed,double cap,
        double blend,double boundary,double before,double after)
    {
        Visit=visit;PaceTerms=[original,coast,mid,exit,speed,cap,blend,boundary];
        Add("208 pace cap",before,after);
    }
    internal static (FreeRacingAction Action,ThrottleTrace209 Trace) Capture(Func<FreeRacingAction> action)
    {
        var previous=Current;var trace=new ThrottleTrace209();Current=trace;
        try {trace.Selected=action();return(trace.Selected,trace);}
        finally {Current=previous;}
    }
}
