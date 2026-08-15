// Najprostszy model decyzji do MVP: losowa zmiana linii o -1/0/+1 w granicach 0..4.
namespace CoreSim.Decisions;

public sealed class SimpleDecisionModel : IRiderDecisionModel
{
    private readonly int _seed;
    private int _legacySequence;

    public SimpleDecisionModel(int seed = 1234) => _seed = seed;

    public RiderDecision Decide(CoreSim.TrackSegment segment, CoreSim.RiderState rider)
    {
        var sample = DeterministicRandom.Sample01(
            _seed,
            rider.RiderId,
            segment.Id,
            _legacySequence++,
            201);
        var delta = Math.Min(2, (int)(sample * 3f)) - 1;
        var target = LaneModel.ClampLane(rider.Lane + delta);
        return new RiderDecision(target, 0f);
    }

    public RiderDecision Decide(RiderDecisionContext context)
    {
        var sample = DeterministicRandom.Sample01(
            _seed,
            context.HeatId,
            context.Lap,
            context.SegmentIndex,
            context.Rider.RiderId,
            202);
        var delta = Math.Min(2, (int)(sample * 3f)) - 1;
        return new RiderDecision(LaneModel.ClampLane(context.Rider.Lane + delta), 0f);
    }
}
