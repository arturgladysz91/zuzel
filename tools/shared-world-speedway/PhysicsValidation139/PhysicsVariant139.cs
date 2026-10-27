namespace SharedWorldSpeedway;

/// <summary>Sensitivity hypotheses ONLY. No empirical calibration or production default change.</summary>
public sealed record PhysicsVariant139(string Name,double FrontScaleSquared=1,double RearLongScale=1,double? FrontFraction=null,double InertiaScale=1,
    double FrontArm=.7,double RearArm=.6,double ContactRegularization=1)
{
    public static PhysicsVariant139 Original {get;}=new("original",FrontArm:TwoContactBikeParameters.Default.FrontAxleFromComMeters,RearArm:TwoContactBikeParameters.Default.RearAxleFromComMeters);
    public static PhysicsVariant139[] NativeSensitivity=>new[]{new PhysicsVariant139("front-slope-half",4,FrontArm:Original.FrontArm,RearArm:Original.RearArm),
        new("rear-long-scale2",RearLongScale:2,FrontArm:Original.FrontArm,RearArm:Original.RearArm),
        new("front-load40",FrontFraction:.4,FrontArm:Original.FrontArm,RearArm:Original.RearArm),
        new("yaw-inertia125",InertiaScale:1.25,FrontArm:Original.FrontArm,RearArm:Original.RearArm)};
    public void Validate(){if(string.IsNullOrWhiteSpace(Name)||!double.IsFinite(FrontScaleSquared)||FrontScaleSquared<=0||!double.IsFinite(RearLongScale)||RearLongScale<=0
        ||FrontFraction is{} f&&(!double.IsFinite(f)||f is <=0 or >=1)||!double.IsFinite(InertiaScale)||InertiaScale<=0||!double.IsFinite(FrontArm)||!double.IsFinite(RearArm)
        ||FrontArm<=0||RearArm<=0||ContactRegularization<1||!double.IsFinite(ContactRegularization))throw new ArgumentException("Finite diagnostic hypotheses required.");}
}
