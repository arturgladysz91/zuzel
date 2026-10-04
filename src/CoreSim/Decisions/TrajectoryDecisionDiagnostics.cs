namespace CoreSim.Decisions;

public sealed record TrajectoryIntentEvaluation(TrajectoryTraversal Traversal,
    float StylePreferenceCost, float LaneChangeReluctanceCost, float OccupancyCost,
    float SurfaceRiskCost, double RequestedLaneChange, double TotalCost, bool Selected = false)
{
    public TrajectoryIntent Intent => Traversal.Intent;
    public double PredictedTraversalTimeSeconds => Traversal.PredictedTraversalTimeSeconds;
    public double PhysicalDistanceMeters => Traversal.PhysicalDistanceMeters;
}
public sealed record TrajectoryDecisionDiagnostics(RiderDecision Decision,
    IReadOnlyList<TrajectoryIntentEvaluation> Candidates, int CandidateTraversals, int ProductionResolutions)
{
    public TrajectoryIntentEvaluation Selected => Candidates.Single(candidate => candidate.Selected);
}
