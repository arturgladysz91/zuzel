using System;
using System.Collections.Generic;
using SpeedwaySim.Models;
using SpeedwaySim.Physics;

namespace SpeedwaySim.Simulation;

public sealed class SpeedwaySimulator
{
    public SimulationRunResult SimulateLaps(Track track, EnvironmentSettings environment, RiderState initialState, ControlInput controls, int laps)
    {
        if (laps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(laps), "Lap count must be positive");
        }

        var random = new Random(environment.RngSeed);
        var lapResults = new List<LapResult>(capacity: laps);
        var state = initialState;

        for (int lapIndex = 0; lapIndex < laps; lapIndex++)
        {
            var lapResult = SimulateLapInternal(track, environment, state, controls, random);
            lapResults.Add(lapResult);
            state = lapResult.FinalState;
        }

        return new SimulationRunResult(lapResults);
    }

    public LapResult SimulateLap(Track track, EnvironmentSettings environment, RiderState initialState, ControlInput controls)
    {
        var random = new Random(environment.RngSeed);
        return SimulateLapInternal(track, environment, initialState, controls, random);
    }

    private LapResult SimulateLapInternal(Track track, EnvironmentSettings environment, RiderState startState, ControlInput controls, Random random)
    {
        var segments = new List<SegmentResult>(track.Segments.Count);
        var telemetry = new List<TelemetryPoint>(track.Segments.Count);
        double totalTime = 0.0;
        double distanceCursor = 0.0;
        var state = startState;

        foreach (var segment in track.Segments)
        {
            var (result, telemetryPoint) = segment.Type switch
            {
                TrackSegmentType.Straight => SimulateStraight(segment, track, environment, state, telemetry.Count, distanceCursor),
                TrackSegmentType.Turn => SimulateTurn(segment, track, environment, state, controls, random, distanceCursor),
                _ => throw new ArgumentOutOfRangeException()
            };

            segments.Add(result);
            telemetry.Add(telemetryPoint);

            totalTime += result.TimeSeconds;
            distanceCursor = telemetryPoint.DistanceEnd;
            state = result.ResultingState;
        }

        return new LapResult(totalTime, state, segments, telemetry);
    }

    private static (SegmentResult Result, TelemetryPoint Telemetry) SimulateStraight(TrackSegment segment, Track track, EnvironmentSettings environment, RiderState state, int segmentIndex, double distanceCursor)
    {
        double speedIn = state.Speed;
        double dragFactor = environment.Drag * SimulationConstants.StraightDragCoefficient * speedIn;
        double speedOut = speedIn + SimulationConstants.StraightAcceleration - dragFactor;
        if (speedOut < 0.0)
        {
            speedOut = 0.0;
        }

        if (speedOut > SimulationConstants.StraightMaxSpeed)
        {
            speedOut = SimulationConstants.StraightMaxSpeed;
        }

        var line = track.GetLineOrThrow(state.Line);
        double relativeLength = segment.Length / track.LapLength;
        double distanceAdjustment = line.DistanceModifier * relativeLength;
        double segmentDistance = Math.Max(1e-3, segment.Length + distanceAdjustment);

        double averageSpeed = Math.Max(1.0, (speedIn + speedOut) / 2.0);
        double time = segmentDistance / averageSpeed;

        var newState = state.With(speed: speedOut);
        var events = Array.Empty<SimulationEvent>();

        double distanceStart = distanceCursor;
        double distanceEnd = distanceStart + segmentDistance;

        var telemetryPoint = new TelemetryPoint(segment.Id, segment.Type, distanceStart, distanceEnd, speedIn, speedOut, state.Line, newState.Line);
        var result = new SegmentResult(segment, time, newState, events);
        return (result, telemetryPoint);
    }

    private static (SegmentResult Result, TelemetryPoint Telemetry) SimulateTurn(TrackSegment segment, Track track, EnvironmentSettings environment, RiderState state, ControlInput controls, Random random, double distanceCursor)
    {
        int entryLine = AdjustLineForEntry(state.Line, controls);
        var entryLineData = track.GetLineOrThrow(entryLine);

        double stabilityNoise = (random.NextDouble() - 0.5) * 0.4; // ±0.2 m/s
        double gripFactor = 0.6 + 0.4 * environment.Grip;
        double stableSpeed = Math.Max(5.0, (entryLineData.BaseStableSpeed + stabilityNoise) * gripFactor);

        double speedIn = state.Speed;
        double drift = SimulationConstants.TurnDriftCoefficient * Math.Max(0.0, speedIn - stableSpeed);

        double tolerance = Math.Max(0.1, entryLineData.Tolerance + GetStrategyToleranceModifier(controls.Strategy, entryLine, controls.TargetLineOnTurnEntry));

        var events = new List<SimulationEvent>();
        int resultingLine = entryLine;
        double penalty = SimulationConstants.TurnPenaltyCoefficient * drift;

        if (drift > tolerance)
        {
            resultingLine = Math.Min(SimulationConstants.MaxLineIndex, entryLine + 1);
            events.Add(new SimulationEvent(SimulationEventType.PushedOut, segment.Id, entryLine, resultingLine, drift));
            penalty *= 1.2;
        }
        else
        {
            double bonus = SimulationConstants.TurnHoldBonus * HoldBonusMultiplier(entryLine, controls.Strategy);
            penalty = Math.Max(0.0, penalty - bonus);
            events.Add(new SimulationEvent(SimulationEventType.LineHeld, segment.Id, entryLine, resultingLine, Math.Abs(stableSpeed - speedIn)));
        }

        double speedOut = Math.Max(0.0, speedIn - penalty);

        if (drift > SimulationConstants.SlipThreshold)
        {
            speedOut *= 0.92;
            resultingLine = Math.Min(SimulationConstants.MaxLineIndex, resultingLine + 1);
            events.Add(new SimulationEvent(SimulationEventType.Slip, segment.Id, entryLine, resultingLine, drift));
        }

        var exitLineData = track.GetLineOrThrow(resultingLine);
        double relativeLength = segment.Length / track.LapLength;
        double distanceAdjustment = (entryLineData.DistanceModifier + exitLineData.DistanceModifier) / 2.0 * relativeLength;
        double segmentDistance = Math.Max(1e-3, segment.Length + distanceAdjustment);

        double averageSpeed = Math.Max(1.0, (speedIn + speedOut) / 2.0);
        double time = segmentDistance / averageSpeed;

        var newState = new RiderState(speedOut, resultingLine);

        double distanceStart = distanceCursor;
        double distanceEnd = distanceStart + segmentDistance;

        var telemetryPoint = new TelemetryPoint(segment.Id, segment.Type, distanceStart, distanceEnd, speedIn, speedOut, state.Line, newState.Line);
        var result = new SegmentResult(segment, time, newState, events);
        return (result, telemetryPoint);
    }

    private static int AdjustLineForEntry(int currentLine, ControlInput controls)
    {
        int target = controls.TargetLineOnTurnEntry;
        if (target < currentLine)
        {
            return Math.Max(target, currentLine - 1);
        }

        if (target > currentLine && controls.Strategy == ControlStrategy.AllowDrift)
        {
            return Math.Min(target, currentLine + 1);
        }

        return currentLine;
    }

    private static double GetStrategyToleranceModifier(ControlStrategy strategy, int entryLine, int targetLine)
    {
        return strategy switch
        {
            ControlStrategy.HoldInside when targetLine <= entryLine => 0.4,
            ControlStrategy.AllowDrift when targetLine > entryLine => -0.2,
            _ => 0.0
        };
    }

    private static double HoldBonusMultiplier(int line, ControlStrategy strategy)
    {
        double baseMultiplier = (SimulationConstants.MaxLineIndex - line + 1) / (double)SimulationConstants.MaxLineIndex;
        return strategy == ControlStrategy.AllowDrift ? baseMultiplier * 0.5 : baseMultiplier;
    }
}
