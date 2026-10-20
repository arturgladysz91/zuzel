using CoreSim;
using CoreSim.PhysicalSpace;

namespace SharedWorldSpeedway;

/// <summary>Conservative local target envelope, not a wall or a body correction.
/// The complete mechanical footprint fits in its native circumcircle, for ANY yaw.
/// A certified constant cap per reference cell also covers every point between table samples.</summary>
public sealed class LocalOffsetLimit
{
    public const double RequiredOuterClearanceMeters=.5;
    public const double MaximumReferenceSpacingMeters=.05;
    public const double FloatPositionAllowanceMeters=.0001;
    private const double FrozenShapeCurvatureBoundPerMeter=.1;
    private const int RootIterations=28;
    public double FootprintRadiusMeters {get;}=SpeedwayBikeDimensions.Reference.BoundingRadiusMeters;
    public double CellMotionAllowanceMeters {get;}
    public double ReferenceSpacingMeters {get;}
    public float MaximumAcrossTrackMeters {get;}
    private readonly float[] caps;
    private readonly double lapMeters;
    public LocalOffsetLimit(TrackFrame frame)
    {
        lapMeters=frame.LapMeters;caps=new float[(int)Math.Ceiling(lapMeters/MaximumReferenceSpacingMeters)];
        ReferenceSpacingMeters=lapMeters/caps.Length;
        var maximumWidth=Math.Max(frame.Track.Geometry.StraightWidthMeters,frame.Track.Geometry.TurnWidthMeters);
        // #101 accepts only the frozen shapes whose curvature is bounded by 0.1/m.
        // Native circular geometry uses its exact maximum curvature. For a constant
        // offset d, |dP/ds|=1+k*d. Distance to ANY boundary is 1-Lipschitz in XY.
        var maximumCurvature=frame.Shape is null?1d/frame.Track.Geometry.InnerRadiusMeters:FrozenShapeCurvatureBoundPerMeter;
        CellMotionAllowanceMeters=(1+maximumCurvature*(maximumWidth-1))*ReferenceSpacingMeters/2;
        var required=FootprintRadiusMeters+RequiredOuterClearanceMeters+CellMotionAllowanceMeters+FloatPositionAllowanceMeters;
        var edges=new RacingLineGeometry(frame);
        for(var i=0;i<caps.Length;i++) {
            var s=(i+.5)*ReferenceSpacingMeters;var road=frame.RoadAt(s,0);var at=frame.At(s,0).Position;
            var origin=new MeterPoint(at.X,at.Y);var normal=new MeterPoint(Math.Sin(road.Heading),-Math.Cos(road.Heading));
            double Gap(double offset)=>edges.OuterPointDistance(origin+normal*offset);
            var low=.5;var high=frame.PhysicalWidthMeters(road)-1;
            if(Gap(low)<required)throw new ArgumentException("No safe outer target in this track cell.",nameof(frame));
            for(var j=0;j<RootIterations;j++){var middle=(low+high)/2;if(Gap(middle)>=required)low=middle;else high=middle;}
            caps[i]=MathF.BitDecrement((float)low); // never round a safe bound outward
        }
        MaximumAcrossTrackMeters=caps.Max();
    }
    public float At(double referenceMeters)
    {
        if(!double.IsFinite(referenceMeters))throw new ArgumentOutOfRangeException(nameof(referenceMeters));
        var s=((referenceMeters%lapMeters)+lapMeters)%lapMeters;
        return caps[Math.Min(caps.Length-1,(int)(s/ReferenceSpacingMeters))];
    }
}
