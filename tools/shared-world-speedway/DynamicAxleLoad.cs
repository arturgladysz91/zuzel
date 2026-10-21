namespace SharedWorldSpeedway;

/// <summary>Quasi-static flat-contact hypothesis, not measured mu*N or wheel-lift dynamics.</summary>
public readonly record struct AxleLoad(double LongitudinalAcceleration,double RearFraction)
{
    public double FrontFraction=>1-RearFraction;
    public bool WithinAssumptions=>RearFraction is >=0 and <=1;
}
public static class DynamicAxleLoad
{
    public const double GravityMetersPerSecondSquared=9.81,ProvisionalHeightMeters=.55;
    public static AxleLoad AtAcceleration(double ax,TwoContactBikeParameters geometry,double heightMeters=ProvisionalHeightMeters)
    {
        if(!double.IsFinite(ax)||!double.IsFinite(heightMeters)||heightMeters<=0)throw new ArgumentException("Finite acceleration and positive provisional height required.");
        var wheelbase=(double)geometry.FrontAxleFromComMeters+geometry.RearAxleFromComMeters;
        return new(ax,geometry.FrontAxleFromComMeters/wheelbase+ax*heightMeters/(GravityMetersPerSecondSquared*wheelbase));
    }
    public static AxleLoad Accepted(RiderView before,RiderView after,float seconds,TwoContactBikeParameters geometry,double heightMeters=ProvisionalHeightMeters)
    {
        if(!float.IsFinite(seconds)||seconds<=0)throw new ArgumentException("Positive actual accepted interval required.");
        var(sin,cos)=Math.SinCos(after.Heading);
        var ax=(((double)after.Velocity.X-before.Velocity.X)*cos+((double)after.Velocity.Y-before.Velocity.Y)*sin)/seconds;
        return AtAcceleration(ax,geometry,heightMeters);
    }
}
