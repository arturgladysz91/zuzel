using System;

namespace SpeedwaySim.Models;

public sealed class EnvironmentSettings
{
    public EnvironmentSettings(double grip, double drag, int rngSeed)
    {
        if (grip < 0 || grip > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(grip), "Grip must be in range 0..1");
        }

        Grip = grip;
        Drag = drag;
        RngSeed = rngSeed;
    }

    public double Grip { get; }

    public double Drag { get; }

    public int RngSeed { get; }
}
