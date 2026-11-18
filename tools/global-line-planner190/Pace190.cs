using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record BudgetKnot190(double Front, double Rear, double Curvature, double Rate);
internal sealed record SpeedProfile190(double[] Mps, double Seconds);
internal sealed record Candidate190(Sector190 First, Sector190 Second, SpeedProfile190 Profile)
{
    internal string Id => $"{First.Shape.Id}/{Second.Shape.Id}";
    internal double Distance => First.Shape.Distance + Second.Shape.Distance;
}

internal sealed class Pace190(TrackFrame frame, TrackStateSnapshot surface, WorldParameters physics,
    LineCatalog190 geometry, RiderSpec spec, float mass, float inertia, HandlingSettings settings)
{
    private readonly TwoContactBikeParameters bike = physics.TwoContactBike ?? TwoContactBikeParameters.Default;
    private readonly RearWheelParameters wheel = physics.RearWheel ?? RearWheelParameters.Default;
    internal const double NominalUse = .94;
    private const double StableRearSlip = .23;
    private const double ResponseReserveSeconds = .19; // shaft unloading and look-ahead beyond activation
    private const double MaxRollingMps = 35;
    private int Count => 2*geometry.HalfSamples;

    internal double Ratio => Engine500Parameters.Default.ShortOverallRatio +
        (Engine500Parameters.Default.LongOverallRatio-Engine500Parameters.Default.ShortOverallRatio)*spec.Setup.Gearing;

    internal ReadOnlySpan<BudgetKnot190> Budgets(Sector190 sector, int bend)
    {
        var result = new BudgetKnot190[sector.Points.Length];
        for (int i=0; i<result.Length; i++)
        {
            double s = geometry.Origin + (bend*geometry.HalfSamples+i)*geometry.Step;
            var here = sector.Points[i];
            var before = sector.Points[Math.Max(0,i-1)];
            var after = sector.Points[Math.Min(sector.Points.Length-1,i+1)];
            var dK = (after.Curvature-before.Curvature)/(after.XY-before.XY).Length;
            var road = frame.RoadAt(s,here.Offset);
            double usable = LaneModel.UsableRacingWidthMeters(frame.Track.Segments[road.Segment].Type,frame.Track.Geometry);
            var grip = surface.SampleSurface(road.Segment,Math.Clamp((float)(here.Offset/usable*4),0,4));
            var bd = LoadBasedTyreGrip.Budget(mass,grip,bike,physics.LoadBasedTyres);
            // Steady-state yaw plus the curvature derivative. Refined in the time-domain solve.
            double frontLat = (mass*here.Curvature*bike.RearAxleFromComMeters+inertia*dK)/
                (bike.FrontAxleFromComMeters+bike.RearAxleFromComMeters);
            result[i] = new(bd.FrontCapacityNewtons*settings.GripScale,bd.RearCapacityNewtons*settings.GripScale,
                frontLat,mass*here.Curvature-frontLat);
        }
        return result;
    }

    internal SpeedProfile190 Profile(Sector190 first, Sector190? second, double use, double coastScale)
    {
        var a = Budgets(first,first.Shape.Id[0]=='1'?0:1);
        var b = second is null ? ReadOnlySpan<BudgetKnot190>.Empty : Budgets(second,1);
        int n = second is null ? geometry.HalfSamples : Count;
        var dist=new double[n];
        var limits=new double[n];
        var speed2=new double[n];
        var stops=new double[n];
        for(int i=0;i<n;i++)
        {
            bool firstSector=i<geometry.HalfSamples;
            int j=firstSector?i:i-geometry.HalfSamples;
            var knots=firstSector?first.Points:second!.Points;
            var loads=firstSector?a[j]:b[j];
            dist[i]=(knots[j+1].XY-knots[j].XY).Length;
            // Preserve enough rear Fx for resistance during lateral load.
            double lat=Math.Min(use*loads.Front/Math.Max(.01,Math.Abs(loads.Curvature)),
                .98*use*loads.Rear/Math.Max(.01,Math.Abs(loads.Rate)));
            limits[i]=Math.Min(MaxRollingMps*MaxRollingMps,lat);
            speed2[i]=limits[i];
        }
        // Cyclic backward/forward reachable envelope. Even the last straight must
        // arrive at the next corner with a feasible speed, including across the seam.
        for(int pass=0;pass<4;pass++)
        {
            for(int i=n-1;i>=0;i--)
            {
                int next=(i+1)%n;
                double current=Math.Sqrt(speed2[next]);
                var load=i<geometry.HalfSamples?a[i]:b[i-geometry.HalfSamples];
                double scrub=160*Math.Min(1,Math.Abs(load.Curvature)*speed2[next]/load.Front);
                double resistance=LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)current);
                double decel=Math.Max(.20,(resistance+scrub-35)/mass)*coastScale;
                speed2[i]=Math.Min(speed2[i],speed2[next]+2*decel*dist[i]);
            }
            for(int i=0;i<n;i++)
            {
                int next=(i+1)%n;
                var load=i<geometry.HalfSamples?a[i]:b[i-geometry.HalfSamples];
                double v=Math.Sqrt(speed2[i]);
                double rpm=(1+StableRearSlip)*v/wheel.WheelRadiusMeters*Ratio*30/Math.PI;
                double torque=Engine500.Torque(rpm)*Ratio*Engine500Parameters.Default.Efficiency;
                double rearSquared=Math.Pow(load.Rear*use,2)-Math.Pow(load.Rate*speed2[i],2);
                double force=Math.Min(torque/wheel.WheelRadiusMeters,Math.Sqrt(Math.Max(0,rearSquared)));
                double drag=LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons((float)v);
                speed2[next]=Math.Min(speed2[next],speed2[i]+2*Math.Max(.05,(force-drag)/mass)*dist[i]);
            }
        }
        var mps = speed2.Select(Math.Sqrt).ToArray();
        double seconds = Enumerable.Range(0,n).Sum(i=>2*dist[i]/(mps[i]+mps[(i+1)%n]));
        for (int i=0;i<n;i++)
        {
            // Shift only impending deceleration upstream. On rising portions
            // the target stays on the local profile to retain exit drive.
            double allowance = ResponseReserveSeconds*mps[i];
            stops[i]=mps[i];
            for(int j=0, k=i; allowance>0 && j<n; j++,k=(k+1)%n)
            {
                double t=Math.Min(1,allowance/dist[k]);
                stops[i]=Math.Min(stops[i],mps[k]+t*(mps[(k+1)%n]-mps[k]));
                allowance-=dist[k];
            }
        }
        return new(stops,seconds);
    }
}

internal sealed class LapPlan190
{
    private readonly LaneKnot190[] points;
    private readonly double[] speeds;
    private readonly double period, origin, step, start, startOffset, join, ratio;
    private readonly TrackFrame frame;
    private double recoveryMps;
    internal string Id { get; }
    internal double MaxAbsOffsetError { get; private set; }
    internal LapPlan190(TrackFrame frame, LineCatalog190 catalog, Candidate190 candidate,
        SpeedProfile190 profile, RiderSpec spec, double ratio, string pace)
    {
        this.frame=frame;
        Id=candidate.Id+"/"+pace;
        points=[..candidate.First.Points.SkipLast(1),..candidate.Second.Points.SkipLast(1)];
        speeds=profile.Mps;
        period=frame.LapMeters;
        origin=catalog.Origin;
        step=catalog.Step;
        start=spec.ReferenceMeters;
        startOffset=spec.OffsetMeters;
        join=origin-start;
        this.ratio=ratio;
    }

    internal RoutePoint At(double s)
    {
        if (s<origin)
        {
            double endOffset=points[0].Offset;
            MeterPoint Pos(double x)
            {
                var sample=frame.Shape!.At(x);
                double offset=startOffset+(endOffset-startOffset)*LineCatalog190.Smooth((x-start)/join);
                return sample.Position+new MeterPoint(Math.Sin(sample.Heading),-Math.Cos(sample.Heading))*offset;
            }
            var a=Pos(s)-Pos(s-.20); var b=Pos(s+.20)-Pos(s);
            // Clamp the launch evaluation only, not the periodic race references.
            double k=2*MeterPoint.Cross(a,b)/(a.Length*b.Length*(a+b).Length);
            return new(startOffset+(endOffset-startOffset)*LineCatalog190.Smooth((s-start)/join),
                Math.Atan2(a.Y+b.Y,a.X+b.X),k,speeds[0]);
        }
        double u=(((s-origin)%period+period)%period)/step;
        int i=Math.Min((int)u,points.Length-1);
        int next=(i+1)%points.Length;
        var p=points[i]; var q=points[next];
        double Mix(double a,double b)=>a+(u-i)*(b-a);
        return new(Mix(p.Offset,q.Offset),p.Course+(u-i)*BikeAngles.Wrap(q.Course-p.Course),
            Mix(p.Curvature,q.Curvature),Mix(speeds[i],speeds[next]));
    }

    /// <summary>The nominal path stays continuous. Adapt only the local speed
    /// target when the predicted tracking state can no longer meet it.</summary>
    internal double TargetSpeed(HandlingForecast activation)
    {
        var body=activation.Rider;
        double s=body.TotalReferenceMeters;
        double speed=body.Speed;
        var line=At(s);
        double offsetError=Math.Abs(body.Road.OffsetMeters-line.Offset);
        MaxAbsOffsetError=Math.Max(MaxAbsOffsetError,offsetError);
        double betaExcess=Math.Max(0,Math.Abs(body.SlideRadians)-.24);
        double gain=Math.Clamp(.55*Math.Max(0,offsetError-1.15)+4*betaExcess,0,3.5);
        recoveryMps += Math.Clamp(gain-recoveryMps,-.05,.18);
        // A loaded crank and spun-up rear wheel continue to feed Fx after a
        // roll-off. Advance the reading along a falling envelope as this energy grows.
        double synchronous=Math.Max(0,speed*Math.Cos(body.SlideRadians))/.33;
        double stored = .5*.01*Math.Max(0,activation.Shaft*activation.Shaft-Math.Pow(ratio*synchronous,2));
        double additionalLookahead=Math.Clamp(stored/14000,0,.16);
        return Math.Min(At(s+speed*.13).Speed,At(s+speed*(.13+additionalLookahead)).Speed)-recoveryMps;
    }
}

