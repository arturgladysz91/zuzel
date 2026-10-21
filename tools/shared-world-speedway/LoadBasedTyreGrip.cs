using CoreSim;

namespace SharedWorldSpeedway;

public sealed record LoadBasedTyreParameters
{
    public static LoadBasedTyreParameters Default {get;}=new();
    // Effective soil+tread capacity, NOT a measured rubber-on-shale coefficient.
    // Uncalibrated starting hypothesis; never fitted to a speed or lap time.
    public double ReferenceFrictionCoefficient {get;init;}=1.4;
    public void Validate(){if(!double.IsFinite(ReferenceFrictionCoefficient)||ReferenceFrictionCoefficient is <=0 or >3)throw new ArgumentException("Positive finite effective tyre/soil friction required.");}
}
public sealed record LoadBasedTyreBudget(double EffectiveFrictionCoefficient,double FrontNormalNewtons,double RearNormalNewtons)
{
    public double FrontCapacityNewtons=>EffectiveFrictionCoefficient*FrontNormalNewtons;
    public double RearCapacityNewtons=>EffectiveFrictionCoefficient*RearNormalNewtons;
    public double TotalCapacityNewtons=>FrontCapacityNewtons+RearCapacityNewtons;
}
/// <summary>Static planar contact capacities µ(surface) Fz; no reference corner speed or skill factor.</summary>
public static class LoadBasedTyreGrip
{
    public const double GravityMetersPerSecondSquared=9.81;
    public static LoadBasedTyreBudget Budget(float mass,TrackSurfaceState surface,TwoContactBikeParameters bike,LoadBasedTyreParameters? parameters=null)
    {
        var p=parameters??LoadBasedTyreParameters.Default;p.Validate();
        if(!float.IsFinite(mass)||mass<=0||!float.IsFinite(surface.EffectiveGrip))throw new ArgumentOutOfRangeException(nameof(mass));
        var normal=mass*GravityMetersPerSecondSquared;var wheelbase=(double)bike.FrontAxleFromComMeters+bike.RearAxleFromComMeters;
        var rear=normal*bike.FrontAxleFromComMeters/wheelbase;
        return new(p.ReferenceFrictionCoefficient*surface.EffectiveGrip,normal-rear,rear);
    }
}
