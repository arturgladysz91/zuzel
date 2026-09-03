namespace CoreSim.Race;

/// <summary>
/// Read-only hook invoked once after a simulation step is resolved and before
/// its state changes are committed.
/// </summary>
public interface ISimulationStepObserver
{
    void OnStepResolved(ResolvedSimulationStep resolvedStep);
}
