using CoreSim.Logging;

namespace CoreSim;

public enum TrackWorkType
{
    Grade,
    Water,
    Pack,
}

public sealed record TrackWorkAction(
    TrackWorkType Type,
    float Intensity,
    IReadOnlyCollection<int>? SegmentIndexes = null,
    IReadOnlyCollection<int>? Lanes = null);

/// <summary>Weather and manager-triggered work on the track.</summary>
public static class TrackEvolution
{
    public static void ApplyWeather(
        Track track,
        TrackState state,
        WeatherState weather,
        int heatId,
        int tick,
        SimLog log)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(weather);
        ArgumentNullException.ThrowIfNull(log);

        for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
        for (var lane = LaneModel.MinLane; lane <= LaneModel.MaxLane; lane++)
        {
            // Outer lanes and exposed straights take slightly more rain. This
            // prevents the whole grid from changing uniformly.
            var exposure = 1f + lane * 0.025f
                + (track.Segments[segmentIndex].Type == SegmentType.Straight ? 0.04f : 0f);
            var moistureDelta = weather.RainIntensity * 0.025f * exposure - weather.DryingRate;
            if (MathF.Abs(moistureDelta) < 0.00001f)
                continue;

            state.ApplySurfaceDelta(
                segmentIndex,
                lane,
                0f,
                0f,
                moistureDelta,
                $"weather:{weather.Condition}",
                heatId,
                tick,
                log);
        }
    }

    public static void ApplyTrackWork(
        Track track,
        TrackState state,
        TrackWorkAction action,
        int heatId,
        int tick,
        SimLog log)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(log);

        var intensity = Math.Clamp(action.Intensity, 0f, 1f);
        for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
        {
            if (action.SegmentIndexes is not null && !action.SegmentIndexes.Contains(segmentIndex))
                continue;

            for (var lane = LaneModel.MinLane; lane <= LaneModel.MaxLane; lane++)
            {
                if (action.Lanes is not null && !action.Lanes.Contains(lane))
                    continue;

                var (grip, ruts, moisture) = action.Type switch
                {
                    TrackWorkType.Grade => (-0.015f * intensity, -0.30f * intensity, 0f),
                    TrackWorkType.Water => (0f, 0f, 0.20f * intensity),
                    TrackWorkType.Pack => (0.06f * intensity, -0.12f * intensity, -0.025f * intensity),
                    _ => throw new ArgumentOutOfRangeException(nameof(action)),
                };

                state.ApplySurfaceDelta(
                    segmentIndex,
                    lane,
                    grip,
                    ruts,
                    moisture,
                    $"track-work:{action.Type}",
                    heatId,
                    tick,
                    log);
            }
        }
    }
}
