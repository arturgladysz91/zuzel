namespace SpeedwaySim.Physics;

/// <summary>
/// Globalne parametry fizyki – łatwe do kalibracji w kolejnych fazach.
/// </summary>
public static class SimulationConstants
{
    public const double StraightAcceleration = 3.8; // m/s^2
    public const double StraightDragCoefficient = 0.045; // wpływ env.drag * v
    public const double StraightMaxSpeed = 38.0; // ograniczenie bezpieczeństwa

    public const double TurnDriftCoefficient = 0.65;
    public const double TurnPenaltyCoefficient = 0.9;
    public const double TurnHoldBonus = 0.35;

    public const double SlipThreshold = 5.5; // gdy dryf jest bardzo duży

    public const int MinLineIndex = 1;
    public const int MaxLineIndex = 10;
}
