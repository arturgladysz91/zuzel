using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record CachedAct42(double Station, State215 State, Activation215 Command, Activation215[] Pending,
        Forces215? Contact);
    private sealed record VerifiedCache42(Lab38 Lab, Geometry215 Geometry, Policy42 Policy, CachedAct42[] Acts,
        double[] Stations);

    private static VerifiedCache42 MakeCache42(Lab38 lab, Geometry215 g, Policy42 policy, Run40 reference)
    {
        var issued = reference.Commands.Where(x => x.Requested >= reference.Fork.State.Time - 1e-10).ToArray();
        var acts = new CachedAct42[issued.Length];
        int stepIndex = 0;
        for (int i = 0; i < issued.Length; i++)
        {
            var time = issued[i].Requested;
            while (stepIndex + 1 < reference.Flight.Steps.Length &&
                   reference.Flight.Steps[stepIndex].After.Time < time - 1e-10) stepIndex++;
            var r = reference.Flight.Steps[stepIndex];
            var state = r.After;
            var pending = reference.Commands[state.Next..].TakeWhile(x => x.Requested < time - 1e-10).ToArray();
            acts[i] = new(reference.Flight.Distances[stepIndex], state, issued[i], pending, r.Forces);
        }
        return new(lab, g, policy, acts, acts.Select(x => x.Station).ToArray());
    }

    private static (Command215?, Decision40)? MatchCache42(Lab38 lab, Geometry215 g, Checkpoint38 at,
        Forces215? contact, Activation215[] queue, Policy42 policy, VerifiedCache42? cache)
    {
        const double LookupMeters = .00001;
        if (cache is null || !ReferenceEquals(cache.Lab, lab) || !ReferenceEquals(cache.Geometry, g) ||
            cache.Policy != policy || cache.Acts.Length == 0) return null;
        int index = Array.BinarySearch(cache.Stations, at.Distance - LookupMeters);
        if (index < 0) index = ~index;
        if (index >= cache.Acts.Length) return null;
        var act = cache.Acts[index];
        var s = at.State;
        // State215 equality includes attitude, all momenta, active command and activation cursor.
        // Earlier consumed inputs are irrelevant. Future payloads and activation times must all agree.
        if (Math.Abs(at.Distance - act.Station) > LookupMeters || s != act.State || contact != act.Contact ||
            !queue.AsSpan(s.Next).SequenceEqual(act.Pending)) return null;
        var command = act.Command.Command;
        return (command, new(s.Time, at.Distance, command.Bike.Drive, s.Pose.Roll,
            contact?.Front.Normal ?? 0, contact?.Front.Utilization ?? 0, contact?.Rear.Utilization ?? 0,
            RollRate36(s, g.Motion(s).BodyRate), ContactSlip39(g, s), 0, 0, 0, 0, null));
    }
}
