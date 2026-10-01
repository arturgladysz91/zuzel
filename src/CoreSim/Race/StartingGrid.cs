namespace CoreSim.Race;

public sealed record StartingGateAssignment(RiderProfile Profile, StartingGate Gate);

/// <summary>Explicit rider-to-field input. Neither rider id nor collection order assigns a gate.</summary>
public static class StartingGrid
{
    public static IReadOnlyList<RiderState> Create(Track track, IReadOnlyList<StartingGateAssignment> assignments)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(assignments);
        if (assignments.Count != StartingGateGeometry.FieldCount
            || assignments.Any(assignment => assignment is null || assignment.Profile is null)
            || assignments.Select(assignment => assignment.Gate).Distinct().Count() != StartingGateGeometry.FieldCount
            || assignments.Select(assignment => assignment.Profile.Id).Distinct().Count() != StartingGateGeometry.FieldCount)
            throw new ArgumentException("A starting grid requires four unique riders with four distinct explicit gates.", nameof(assignments));

        return Array.AsReadOnly(assignments.OrderBy(assignment => assignment.Profile.Id)
            .Select(assignment => new RiderState(assignment.Profile, assignment.Gate, track)).ToArray());
    }
}
