using CoreSim.Setup;

namespace SharedWorldSpeedway;

/// <summary>PROVISIONAL rolling-start, rigid single-ratio drivetrain; no clutch/crank state.</summary>
public sealed record Engine500Parameters
{
    public static Engine500Parameters Default {get;}=new();
    public double ShortOverallRatio {get;init;}=10;
    public double LongOverallRatio {get;init;}=8;
    public double Efficiency {get;init;}=.94;
    public void Validate()
    {
        if(!double.IsFinite(ShortOverallRatio)||!double.IsFinite(LongOverallRatio)||!double.IsFinite(Efficiency)
            ||ShortOverallRatio<LongOverallRatio||LongOverallRatio<=0||ShortOverallRatio>30||Efficiency is <=0 or >1)
            throw new ArgumentException("Finite positive single ratios and transmission efficiency in (0,1] required.");
    }
}
public readonly record struct EngineTorquePoint(double Rpm,double NewtonMeters);
public sealed record Engine500Command(double EngineRpm,double OverallRatio,double CrankTorqueNewtonMeters,
    double AvailableWheelTorqueNewtonMeters,double DriveWheelTorqueNewtonMeters,double Efficiency)
{
    public double CrankPowerWatts=>CrankTorqueNewtonMeters*EngineRpm*Math.PI/30;
    public double CrankWorkJoules(RearWheelStep step)=>step.EngineWorkJoules/Efficiency;
    public double TransmissionLossJoules(RearWheelStep step)=>CrankWorkJoules(step)-step.EngineWorkJoules;
}
public static class Engine500
{
    public const double CutoffRpm=12000;
    // JRM 889 source anchors 499cc / 48–50kW at8500rpm. Intermediate
    // torque points and cutoff are explicit hypotheses, not a measured dyno map.
    public static IReadOnlyList<EngineTorquePoint> Curve {get;}=Array.AsReadOnly(new[]{
        new EngineTorquePoint(0,0),new(2000,35),new(4000,47),new(6000,54),new(7500,57),
        new(8500,55),new(10000,44),new(11000,35),new(CutoffRpm,0)});
    public static double Torque(double rpm)
    {
        if(!double.IsFinite(rpm)||rpm<0)throw new ArgumentOutOfRangeException(nameof(rpm));
        if(rpm>=CutoffRpm)return 0; // fuel/ignition cut; NEVER clamp wheel or vehicle velocity.
        for(var i=1;i<Curve.Count;i++)if(rpm<Curve[i].Rpm){var a=Curve[i-1];var b=Curve[i];return a.NewtonMeters+(b.NewtonMeters-a.NewtonMeters)*(rpm-a.Rpm)/(b.Rpm-a.Rpm);}
        return 0;
    }
    public static Engine500Command Evaluate(double wheelOmega,BikeSetup setup,double drive,Engine500Parameters? parameters=null)
    {
        var p=parameters??Engine500Parameters.Default;p.Validate();
        if(!double.IsFinite(wheelOmega)||!double.IsFinite(drive)||drive is <0 or >1)throw new ArgumentOutOfRangeException(nameof(drive));
        var ratio=p.ShortOverallRatio+(p.LongOverallRatio-p.ShortOverallRatio)*setup.Gearing;
        var rpm=Math.Max(0,wheelOmega)*ratio*30/Math.PI;var crank=Torque(rpm);var wheel=crank*ratio*p.Efficiency;
        return new(rpm,ratio,crank,wheel,wheel*drive,p.Efficiency);
    }
}
