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

    [Fact]
    public void ContactLostRhythmUsesPhysicalMeters()
    {
        var oneMeterSpacing = Resolve(ContactRiders(), seed: 3, laneSpacingMeters: 1f);
        var twoMeterSpacing = Resolve(ContactRiders(), seed: 3, laneSpacingMeters: 2f);

        var oneMeterChange = LostRhythmChange(oneMeterSpacing);
        var twoMeterChange = LostRhythmChange(twoMeterSpacing);
        var oneMeterDeltaLaneUnits = oneMeterChange.LateralPosition - 2f;
        var twoMeterDeltaLaneUnits = twoMeterChange.LateralPosition - 2f;

        Assert.Equal(0.50f, oneMeterDeltaLaneUnits, 5);
        Assert.Equal(0.25f, twoMeterDeltaLaneUnits, 5);
        Assert.Equal(0.50f, oneMeterDeltaLaneUnits * 1f, 5);
        Assert.Equal(0.50f, twoMeterDeltaLaneUnits * 2f, 5);
    }

    [Fact]
    public void DefaultGeometryPreservesLostRhythmLateralEffect()
    {
        var resolved = Resolve(ContactRiders(), seed: 3, geometry: TrackGeometry.Default);

        var change = LostRhythmChange(resolved);

        Assert.Equal(3, change.Lane);
        Assert.Equal(2.5f, change.LateralPosition, 5);
    }

    [Fact]
    public void StraightContactLostRhythmDoesNotPushLaterally()
    {
        var resolved = Resolve(
            ContactRiders(),
            seed: 173,
            segmentType: SegmentType.Straight,
            geometry: TrackGeometry.Default);

        var change = LostRhythmChange(resolved);

        Assert.Equal(2, change.Lane);
        Assert.Equal(2f, change.LateralPosition);
    }

    [Fact]
    public void MaxLaneContactDoesNotLeaveTrack()
    {
        var resolved = Resolve(
            ContactRiders(lane: LaneModel.MaxLane, lateralPosition: LaneModel.MaxLane),
            seed: 4,
            geometry: TrackGeometry.Default);

        var change = LostRhythmChange(resolved);

        Assert.Equal(LaneModel.MaxLane, change.Lane);
        Assert.Equal(LaneModel.MaxLane, change.LateralPosition);
        Assert.InRange(change.LateralPosition, LaneModel.MinLane, LaneModel.MaxLane);
    }

    [Fact]
    public void ContactDisplacementIsIndependentOfRiderCollectionOrder()
    {
        var forward = Resolve(ContactRiders(), seed: 3);
        var reversed = Resolve(ContactRiders().Reverse().ToArray(), seed: 3);

        Assert.Equal(forward.Changes, reversed.Changes);
        Assert.Equal(forward.Events, reversed.Events);
        Assert.Equal(2.5f, LostRhythmChange(forward).LateralPosition, 5);
        Assert.Equal(2.5f, LostRhythmChange(reversed).LateralPosition, 5);
    }

    [Theory]
    [InlineData(SegmentType.Straight, 10f, 14f, true)]
    [InlineData(SegmentType.TurnEntry, 10f, 14f, false)]
    [InlineData(SegmentType.TurnMiddle, 10f, 14f, false)]
    [InlineData(SegmentType.TurnExit, 10f, 14f, false)]
    [InlineData(SegmentType.Straight, 14f, 10f, false)]
    [InlineData(SegmentType.TurnEntry, 14f, 10f, true)]
    [InlineData(SegmentType.TurnMiddle, 14f, 10f, true)]
    [InlineData(SegmentType.TurnExit, 14f, 10f, true)]
    public void ContactIntegrationUsesCurrentSegmentWidth(
        SegmentType type, float straightWidth, float turnWidth, bool expectsContact)
    {
        // Short segments preserve the deliberately chosen 0.25 normalized gap
        // through movement: about 0.5 m in 10 m width, 0.75 m in 14 m width.
        var geometry = new TrackGeometry(0.03f, 24f, straightWidth, turnWidth, 0.001f);
        var resolved = Resolve(
            new[] { Rider(1, 2, 2f), Rider(2, 2, 2.25f) },
            seed: 173,
            segmentType: type,
            geometry: geometry);

        Assert.Equal(expectsContact ? 1 : 0, ContactEvents(resolved).Length);
    }

    [Fact]
    public void LostRhythmIntegrationUsesTurnWidthWhenStraightWidthDiffers()
    {
        var geometry = new TrackGeometry(60f, 24f, 10f, 14f, 0.001f);
        var change = LostRhythmChange(Resolve(ContactRiders(), seed: 3, geometry: geometry));

        Assert.Equal(2f + 0.5f / 3f, change.LateralPosition, 6);
        Assert.Equal(3, change.Lane);
    }

    private static ResolvedSimulationStep Resolve(
        IReadOnlyList<RiderState> riders,
        int seed,
        float laneSpacingMeters = 1f,
        SegmentType segmentType = SegmentType.TurnMiddle,
        TrackGeometry? geometry = null)
    {
        geometry ??= new TrackGeometry(60f, 24f, laneSpacingMeters, 0.001f);
        var track = new Track(
            new[] { new TrackSegment(0, segmentType) },
            geometry);
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

    private static RiderState[] ContactRiders(int lane = 2, float lateralPosition = 2f)
        =>
        [
            Rider(1, lane, lateralPosition),
            Rider(2, lane, lateralPosition),
        ];

    private static RiderStateChange LostRhythmChange(ResolvedSimulationStep resolved)
    {
        var contact = Assert.Single(ContactEvents(resolved));
        Assert.Equal(SimulationEventType.ContactLostRhythm, contact.Type);
        return Assert.Single(resolved.Changes.Where(change => change.RiderId == 2));
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
