using System.Text;
using System.Text.Json;
using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;

namespace CoreSim.Tests;

// The same fixture is captured on unmodified main and by the #56A platform audit.
internal static class RiderCompatibilityFixture
{
    internal const string BaseMainSha = "ffa7196027f031f947c844da04f5706cb2655f41";
    internal const string ConvergenceSha256 = "22BF12C89FA3C2A5B3617DE1A996D7F7A0E495100B8F3DC32B2345CD17EDE6D5";
    internal const string AdaptiveSha256 = "DBFAA42000A782BA948706E7BF47966733F2F994037CD54A1D6D3317C2AC6622";

    internal static RiderGameplayProfile ExampleGameplay(string change = "None")
        => new(new RiderAbilities(change == "Reaction" ? 1 : 91, change == "Start" ? 1 : 84,
            change == "Technique" ? 1 : 88, change == "TrackReading" ? 1 : 76,
            change == "Attack" ? 1 : 93, change == "Defense" ? 1 : 67,
            change == "PairRiding" ? 1 : 55, change == "Strength" ? 1 : 72),
            new RiderPhysicalProfile(change == "MassKg" ? 120 : 68),
            new RiderInteractionStyle(change == "Combativeness" ? 0 : .82f,
                change == "PreferredLine" ? PreferredLine.Outside : PreferredLine.Inside,
                change == "SetupIndependence" ? 0 : .60f));

    internal static RiderProfile Modern(RiderProfile legacy, string change = "None")
        => RiderProfile.CreateCanonical(legacy.Id, legacy.Name, ExampleGameplay(change), legacy.Skills, legacy.Style);

    internal static byte[] Capture(bool adaptive = false,
        Func<RiderProfile, RiderProfile>? profileChange = null, Action<RiderState>? stateChange = null)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var surface = TrackState.CreateDefault(track);
        var riders = Enumerable.Range(1, 4).Select(id =>
        {
            var profile = RiderProfile.CreateDefault(id);
            var rider = new RiderState(profileChange?.Invoke(profile) ?? profile,
                (StartingGate)(id - 1), track);
            stateChange?.Invoke(rider);
            return rider;
        }).ToList();
        var result = new HeatSimulator(adaptive ? new AdaptiveDecisionModel() : new Converge())
            .SimulateHeat(track, surface, riders, new HeatSimulationOptions
            {
                Laps = 4, Seed = 7, Weather = WeatherState.LightRain, IncidentFrequency = 1,
            }, heatId: 56);
        // Preserve all historical public state and log fields, including legacy contacts,
        // classification, time, position, speed, morale and every raw surface cell.
        var json = JsonSerializer.Serialize(new
        {
            Result = result,
            Riders = riders,
            Surface = Enumerable.Range(0, surface.SegmentCount).Select(segment =>
                Enumerable.Range(0, surface.LinesCount).Select(lane => surface.GetSurface(segment, lane)).ToArray()).ToArray(),
        });
        return Encoding.UTF8.GetBytes(json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }

    private sealed class Converge : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
            => new(2, .1f);
    }
}
