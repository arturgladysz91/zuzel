using System;
using SpeedwaySim.Physics;

namespace SpeedwaySim.Models;

public sealed class RiderState
{
    public RiderState(double speed, int line)
    {
        Speed = Math.Max(0, speed);
        Line = ClampLine(line);
    }

    public double Speed { get; }

    public int Line { get; }

    public RiderState With(double? speed = null, int? line = null)
    {
        return new RiderState(speed ?? Speed, line.HasValue ? ClampLine(line.Value) : Line);
    }

    private static int ClampLine(int value)
    {
        if (value < SimulationConstants.MinLineIndex)
        {
            return SimulationConstants.MinLineIndex;
        }

        if (value > SimulationConstants.MaxLineIndex)
        {
            return SimulationConstants.MaxLineIndex;
        }

        return value;
    }
}
