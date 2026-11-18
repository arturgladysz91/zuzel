using SharedWorldSpeedway;
namespace SpeedwayHandling;

internal sealed record Calibration198(double BaseMargin,double PositionTolerance,double YawTolerance,
    double SpeedTolerance,double CourseTolerance);
internal sealed record Archive198(Run144 Run,LeanSample[][] Lean,Schedule188 Timing,HandlingSettings Handling);
internal sealed record Seed198(string Arm,Archive198 Archive,int Branch,Start197 Start,double Goal,FreeRacingAction[] Controls);
internal sealed record ModelScore199(string Arm,double ActivationError,double PredictedFinish,int Steps,double Sweep);

