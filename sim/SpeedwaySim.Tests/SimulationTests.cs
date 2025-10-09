using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SpeedwaySim.Models;
using SpeedwaySim.Simulation;
using Xunit;

namespace SpeedwaySim.Tests;

public sealed class SimulationTests
{
    [Fact]
    public void Simulation_Is_Deterministic_For_Given_Seed()
    {
        var track = LoadTrack();
        var env = new EnvironmentSettings(grip: 0.8, drag: 0.3, rngSeed: 12345);
        var state = new RiderState(speed: 18.0, line: 3);
        var controls = new ControlInput(targetLineOnTurnEntry: 2, ControlStrategy.HoldInside);
        var simulator = new SpeedwaySimulator();

        var resultA = simulator.SimulateLap(track, env, state, controls);
        var resultB = simulator.SimulateLap(track, env, state, controls);

        Assert.Equal(resultA.LapTimeSeconds, resultB.LapTimeSeconds, precision: 10);
        Assert.Equal(resultA.FinalState.Speed, resultB.FinalState.Speed, precision: 10);
        Assert.Equal(resultA.FinalState.Line, resultB.FinalState.Line);

        Assert.Equal(resultA.Telemetry.Count, resultB.Telemetry.Count);
        for (int i = 0; i < resultA.Telemetry.Count; i++)
        {
            Assert.Equal(resultA.Telemetry[i].SpeedOut, resultB.Telemetry[i].SpeedOut, precision: 10);
            Assert.Equal(resultA.Telemetry[i].LineOut, resultB.Telemetry[i].LineOut);
        }
    }

    [Fact]
    public void HigherGrip_Produces_Faster_Lap()
    {
        var track = LoadTrack();
        var lowGrip = new EnvironmentSettings(grip: 0.5, drag: 0.3, rngSeed: 999);
        var highGrip = new EnvironmentSettings(grip: 0.9, drag: 0.3, rngSeed: 999);
        var state = new RiderState(speed: 19.0, line: 4);
        var controls = new ControlInput(targetLineOnTurnEntry: 4, ControlStrategy.AllowDrift);
        var simulator = new SpeedwaySimulator();

        var slowResult = simulator.SimulateLap(track, lowGrip, state, controls);
        var fastResult = simulator.SimulateLap(track, highGrip, state, controls);

        Assert.True(fastResult.LapTimeSeconds < slowResult.LapTimeSeconds);
        Assert.True(fastResult.FinalState.Speed >= slowResult.FinalState.Speed);
    }

    private static Track LoadTrack()
    {
        var json = File.ReadAllText(Path.Combine("..", "data", "sample_track.json"));
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var name = root.GetProperty("trackName").GetString() ?? "Unknown";
        var lapLength = root.GetProperty("lapLength").GetDouble();

        var segments = root.GetProperty("segments").EnumerateArray().Select(element =>
        {
            var id = element.GetProperty("id").GetString() ?? string.Empty;
            var typeString = element.GetProperty("type").GetString() ?? "STRAIGHT";
            var type = typeString.Equals("TURN", StringComparison.OrdinalIgnoreCase) ? TrackSegmentType.Turn : TrackSegmentType.Straight;
            var length = element.GetProperty("length").GetDouble();
            return new TrackSegment(id, type, length);
        }).ToList();

        var lines = root.GetProperty("lines").EnumerateArray().Select(element =>
        {
            var index = element.GetProperty("index").GetInt32();
            var distanceModifier = element.GetProperty("distanceModifier").GetDouble();
            var baseStableSpeed = element.GetProperty("baseStableSpeed").GetDouble();
            var tolerance = element.GetProperty("tolerance").GetDouble();
            return new RacingLine(index, distanceModifier, baseStableSpeed, tolerance);
        }).ToList();

        return new Track(name, lapLength, segments, lines);
    }
}
