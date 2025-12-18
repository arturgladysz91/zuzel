// Najprostszy model decyzji do MVP: losowa zmiana linii o -1/0/+1 w granicach 0..4.
namespace CoreSim.Decisions;

public sealed class SimpleDecisionModel : IRiderDecisionModel
{
    private readonly Random _rng;
    public SimpleDecisionModel(int seed = 1234) => _rng = new Random(seed);

    public RiderDecision Decide(CoreSim.TrackSegment segment, CoreSim.RiderState rider)
    {
        var delta = _rng.Next(-1, 2); // -1,0,1
        var target = Math.Clamp(rider.Lane + delta, 0, 4);
        return new RiderDecision(target, 0f);
    }
}
