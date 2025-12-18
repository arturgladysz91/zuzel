// Kontrakt dla logiki decyzji zawodnika: wybór linii na danym segmencie.
namespace CoreSim.Decisions;

public interface IRiderDecisionModel
{
    RiderDecision Decide(CoreSim.TrackSegment segment, CoreSim.RiderState rider);
}
