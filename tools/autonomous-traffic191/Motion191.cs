using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal interface IMotionReference191
{
    string Id { get; }
    double DriveLimit { get; }
    RoutePoint At(double reference);
    double TargetSpeed(HandlingForecast activation);
}

internal readonly record struct Lateral191(double Offset, double Slope, double Second)
{
    internal static Lateral191 Rest(double offset) => new(offset,0,0);
}

/// <summary>Any continuous terminal offset in the usable corridor. Both the
/// incumbent's slope and second derivative carry into the replacement.</summary>
internal sealed class Curve191
{
    internal double Origin { get; }
    internal double Target { get; }
    internal double Length { get; }
    private readonly double a,b,c,d,e,f;
    internal Curve191(double start,Lateral191 initial,double target,double length)
    {
        Origin=start; Target=target; Length=length;
        a=initial.Offset; b=initial.Slope*length; c=.5*initial.Second*length*length;
        double delta=target-a-b-c, slope=-b-2*c, second=-2*c;
        d=10*delta-4*slope+.5*second;
        e=-15*delta+7*slope-second;
        f=6*delta-3*slope+.5*second;
    }
    internal Lateral191 At(double s)
    {
        double u=Math.Clamp((s-Origin)/Length,0,1);
        if(u>=1) return Lateral191.Rest(Target);
        return new(a+u*(b+u*(c+u*(d+u*(e+u*f)))),
            (b+u*(2*c+u*(3*d+u*(4*e+u*5*f))))/Length,
            (2*c+u*(6*d+u*(12*e+u*20*f)))/(Length*Length));
    }
}

internal sealed class Track191(TrackFrame frame)
{
    internal readonly TrackFrame Frame=frame;
    private const double DifferenceMeters=.25;
    public (double Minimum,double Maximum) Bounds(double s)
        =>(.20,Frame.Shape!.Width(s)-1.70);
    internal MeterPoint XY(double s,double offset)
    {
        var road=Frame.Shape!.At(s);
        return road.Position+new MeterPoint(Math.Sin(road.Heading),-Math.Cos(road.Heading))*offset;
    }
    internal RoutePoint At(Curve191 curve,double s,double speed=0) => At(curve.At(s),s,speed);

    internal RoutePoint At(Lateral191 here,double s,double speed=0)
    {
        // Offset curve X=C+d*N_out. Its derivatives use the frozen
        // centerline derivatives and the exact interpolated lateral state.
        var q=Frame.Shape!.At(s);
        double k=q.Curvature;
        double dk=(Frame.Shape.At(s+DifferenceMeters).Curvature-
            Frame.Shape.At(s-DifferenceMeters).Curvature)/(2*DifferenceMeters);
        double stretch=1+k*here.Offset;
        double tangentAcceleration=dk*here.Offset+2*k*here.Slope;
        double normalAcceleration=-k*stretch+here.Second;
        double normSquared=stretch*stretch+here.Slope*here.Slope;
        double curvature=(here.Slope*tangentAcceleration-stretch*normalAcceleration)/
            (normSquared*Math.Sqrt(normSquared));
        return new(here.Offset,BikeAngles.Wrap(q.Heading-Math.Atan2(here.Slope,stretch)),curvature,speed);
    }
}

internal sealed class Motion191 : IMotionReference191
{
    internal const double CoastLookaheadMeters=74;
    private const double SpeedGridMeters=2;
    private readonly Track191 track;
    private readonly Curve191? curve;
    private readonly Func<double,Lateral191> path;
    private readonly double start,limit,approach,ratio;
    private readonly double[] speeds;
    public string Id {get;}
    public double DriveLimit {get;}
    internal double Target => Curve.Target;
    internal Curve191 Curve => curve ?? throw new InvalidOperationException("A single-curve reference was required.");

    internal Motion191(Track191 track,Curve191 curve,RiderSpec spec,HandlingForecast startState,double mass,
        double grip,string id,double limit=100,double driveLimit=1,double approach=1)
        : this(track,curve.At,spec,startState,mass,grip,id,limit,driveLimit,approach)
    {
        this.curve=curve;
    }

    internal Motion191(Track191 track,Func<double,Lateral191> path,RiderSpec spec,HandlingForecast startState,double mass,
        double grip,string id,double limit=100,double driveLimit=1,double approach=1)
    {
        this.track=track; this.path=path; Id=id; DriveLimit=driveLimit; this.limit=limit;
        this.approach=approach;
        start=startState.Rider.TotalReferenceMeters;
        double gear=spec.Setup.Gearing;
        ratio=Engine500Parameters.Default.ShortOverallRatio -
            (Engine500Parameters.Default.ShortOverallRatio-Engine500Parameters.Default.LongOverallRatio)*gear;
        speeds=new double[(int)(CoastLookaheadMeters/SpeedGridMeters)+1];
        double rearSpin=Math.Clamp((startState.Wheel*.33-startState.Rider.ForwardSpeed)/
            Math.Max(6,startState.Rider.Speed),0,.4);
        double stored=.5*LaunchParameters144.Default.CrankInertiaKgMetersSquared*Math.Max(0,
            startState.Shaft*startState.Shaft-Math.Pow(ratio*startState.Rider.ForwardSpeed/.33,2));
        double brakeScale=Math.Clamp(1-stored/26000,.70,1);
        double top=.33*Engine500.CutoffRpm*Math.Tau/60/ratio;
        for(int i=speeds.Length-1;i>=0;i--)
        {
            var point=track.At(path(start+i*SpeedGridMeters),start+i*SpeedGridMeters);
            double k=Math.Abs(point.Curvature);
            double envelope=Math.Min(top/(1+.11*rearSpin),
                Math.Sqrt(approach*.92*grip/Math.Max(.0001,k)));
            if(i+1<speeds.Length)
            {
                double v=speeds[i+1];
                double drag=LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)v);
                double lateralScrub=115*Math.Min(1,k*v*v/Math.Max(1,grip));
                var p=track.Frame.Shape!.At(start+i*SpeedGridMeters);
                double ds=SpeedGridMeters*(1+p.Curvature*point.Offset);
                envelope=Math.Min(envelope,Math.Sqrt(v*v+2*brakeScale*(drag+lateralScrub)/mass*ds));
            }
            speeds[i]=envelope;
        }
    }

    public RoutePoint At(double s)
    {
        double n=Math.Clamp((s-start)/SpeedGridMeters,0,speeds.Length-1);
        int i=Math.Min((int)n,speeds.Length-2);
        return track.At(path(s),s,speeds[i]+(n-i)*(speeds[i+1]-speeds[i]));
    }

    public double TargetSpeed(HandlingForecast state)
    {
        // Predict at activation, then compare the next 190 ms of progress
        // to the reachable envelope. Use the bike's actual wheel/shaft load.
        var v=state.Rider;
        double s=v.TotalReferenceMeters+v.Speed*.19;
        double rim=state.Wheel*.33;
        double synchronous=Math.Max(0,v.ForwardSpeed)/.33*ratio;
        double stored=.5*LaunchParameters144.Default.CrankInertiaKgMetersSquared*
            Math.Max(0,state.Shaft*state.Shaft-synchronous*synchronous);
        double unload=Math.Clamp(stored/21000+Math.Max(0,rim-v.Speed)/100,0,.24);
        double pace=Math.Min(At(s).Speed,At(s+v.Speed*unload).Speed);
        double tracking=Math.Max(0,Math.Abs(v.Road.OffsetMeters-At(v.TotalReferenceMeters).Offset)-.65);
        double beta=Math.Max(0,Math.Abs(v.SlideRadians)-.25);
        return Math.Min(limit,pace-Math.Min(4,.7*tracking+9*beta));
    }
}

