using System;
using SpeedwaySim.Physics;

namespace SpeedwaySim.Models;

public sealed class ControlInput
{
    public ControlInput(int targetLineOnTurnEntry, ControlStrategy strategy)
    {
        TargetLineOnTurnEntry = ClampLine(targetLineOnTurnEntry);
        Strategy = strategy;
    }

    /// <summary>
    /// Linia, na którą zawodnik celuje wchodząc w łuk.
    /// </summary>
    public int TargetLineOnTurnEntry { get; }

    public ControlStrategy Strategy { get; }

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
