namespace CoreSim.Decisions;

/// <summary>
/// Decision contract kept backward-compatible with the early MVP. Context-aware
/// models override the second method; fixed test models only need the first one.
/// </summary>
public interface IRiderDecisionModel
{
    RiderDecision Decide(TrackSegment segment, RiderState rider);

    RiderDecision Decide(RiderDecisionContext context)
        => Decide(context.Segment, context.Rider);
}
