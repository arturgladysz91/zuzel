// Wynik decyzji zawodnika na segment: docelowa linia i ryzyko.
namespace CoreSim.Decisions;

public sealed record RiderDecision(int TargetLane, float Risk = 0f);
