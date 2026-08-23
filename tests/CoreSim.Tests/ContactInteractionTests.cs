using CoreSim;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class ContactInteractionTests
{
    private sealed class HoldLaneDecisionModel : IRiderDecisionModel
    {
        public RiderDecision Decide(TrackSegment segment, RiderState rider) => new(rider.Lane);
    }

    [Fact]
    public void DifferentDiscreteLanesCanBeContactCandidatesWhenLaterallyClose()
    {
        var resolved = Resolve(
            new[]
            {
                Rider(1, lane: 1, lateralPosition: 1.45f),
                Rider(2, lane: 2, lateralPosition: 1.55f),
            },
            seed: 3);

        var contact = Assert.Single(ContactEvents(resolved));
        Assert.Equal(2, contact.RiderId);
        Assert.Equal(1, contact.OtherRiderId);
    }

    [Fact]
    public void SameDiscreteLaneIsNotEnoughWhenLaterallySeparated()
    {
        var resolved = Resolve(
            new[]
            {
                Rider(1, lane: 2, lateralPosition: 1.2f),
                Rider(2, lane: 2, lateralPosition: 2f),
            },
            seed: 3);

        Assert.Empty(ContactEvents(resolved));
    }

    [Fact]
    public void NearestLaterallyOverlappingLeaderIsSelected()
    {
        var resolved = Resolve(
            new[]
            {
                Rider(1, lane: 2, lateralPosition: 1.5f, elapsedTimeSeconds: 0f),
                Rider(2, lane: 2, lateralPosition: 3f, elapsedTimeSeconds: 0.01f),
                Rider(3, lane: 2, lateralPosition: 1.55f, elapsedTimeSeconds: 0.02f),
            },
            seed: 4);

        var contact = Assert.Single(ContactEvents(resolved));
        Assert.Equal(3, contact.RiderId);
        Assert.Equal(1, contact.OtherRiderId);
    }

    [Fact]
    public void ContactPairSelectionIsIndependentOfRiderCollectionOrder()
    {
        RiderState[] Riders() =>
        [
            Rider(1, lane: 2, lateralPosition: 1.5f, elapsedTimeSeconds: 0f),
            Rider(2, lane: 2, lateralPosition: 3f, elapsedTimeSeconds: 0.01f),
            Rider(3, lane: 2, lateralPosition: 1.55f, elapsedTimeSeconds: 0.02f),
        ];

        var forward = Resolve(Riders(), seed: 4);
        var reversed = Resolve(Riders().Reverse().ToArray(), seed: 4);

        Assert.Equal(ContactEvents(forward), ContactEvents(reversed));
        Assert.Equal(forward.Changes, reversed.Changes);
        Assert.Equal(forward.Events, reversed.Events);
    }

    [Fact]
    public void DefaultAlignedLanesPreserveBasicCandidateTopology()
    {
        var sameLane = Resolve(
            new[]
            {
                Rider(1, lane: 2, lateralPosition: 2f),
                Rider(2, lane: 2, lateralPosition: 2f),
            },
            seed: 3);
        var adjacentLanes = Resolve(
            new[]
            {
                Rider(1, lane: 1, lateralPosition: 1f),
                Rider(2, lane: 2, lateralPosition: 2f),
            },
            seed: 3);

        Assert.Single(ContactEvents(sameLane));
        Assert.Empty(ContactEvents(adjacentLanes));
    }

    [Fact]
    public void ContactCandidatesAreFixedBeforeContactEffects()
    {
        var resolved = Resolve(
            new[]
            {
                Rider(1, lane: 2, lateralPosition: 1.8f, elapsedTimeSeconds: 0f),
                Rider(2, lane: 2, lateralPosition: 1.8f, elapsedTimeSeconds: 0.01f),
                Rider(3, lane: 2, lateralPosition: 1.5f, elapsedTimeSeconds: 0.02f),
            },
            seed: 3);

        var contacts = ContactEvents(resolved);

        Assert.Equal(2, contacts.Length);
        Assert.Equal(1, contacts[0].OtherRiderId);
        Assert.Equal(2, contacts[1].OtherRiderId);
    }

    private static ResolvedSimulationStep Resolve(
        IReadOnlyList<RiderState> riders,
        int seed,
        float laneSpacingMeters = 1f)
    {
        var track = new Track(
            new[] { new TrackSegment(0, SegmentType.TurnMiddle) },
            new TrackGeometry(60f, 24f, laneSpacingMeters, 0.001f));
        var options = new HeatSimulationOptions
        {
            Laps = 1,
            Seed = seed,
            Weather = new WeatherState(WeatherCondition.Cloudy, 0f, 0f),
            IncidentFrequency = 0f,
        };
        var engine = new SimulationEngine(new HoldLaneDecisionModel());
        var snapshot = engine.CaptureSnapshot(
            track,
            TrackState.CreateDefault(track, new TrackSurfaceState(1f, 0f, 0.35f)),
            riders,
            new SimulationStepContext(1, 0, 0, 0, seed, options.Laps));

        return engine.Resolve(snapshot, engine.Decide(snapshot), options);
    }

    private static RiderState Rider(
        int riderId,
        int lane,
        float lateralPosition,
        float elapsedTimeSeconds = 0f)
        => new(riderId, lane)
        {
            LateralPosition = lateralPosition,
            Speed = 10f,
            ElapsedTimeSeconds = elapsedTimeSeconds,
        };

    private static SimulationStepEvent[] ContactEvents(ResolvedSimulationStep resolved)
        => resolved.Events
            .Where(item => item.Type is SimulationEventType.ContactCrash or SimulationEventType.ContactLostRhythm)
            .ToArray();
}
