namespace CoreSim;

public enum RandomChannel
{
    LaneDecision = 100,
    TrackObservation = 110,
    IncidentOccurrence = 200,
    IncidentSeverity = 210,
    ContactOccurrence = 300,
    ContactSeverity = 310,
}

/// <summary>
/// Stateless samples addressed by stable domain keys. No shared generator or
/// process-randomized string hash participates in a race result.
/// </summary>
public static class DeterministicRandom
{
    public static double Sample01(
        int seed,
        int heatId,
        int stepNumber,
        int riderId,
        RandomChannel channel,
        params int[] discriminators)
    {
        var state = 14695981039346656037UL;
        Mix(ref state, seed);
        Mix(ref state, heatId);
        Mix(ref state, stepNumber);
        Mix(ref state, riderId);
        Mix(ref state, (int)channel);
        foreach (var discriminator in discriminators)
            Mix(ref state, discriminator);

        state += 0x9E3779B97F4A7C15UL;
        state = (state ^ (state >> 30)) * 0xBF58476D1CE4E5B9UL;
        state = (state ^ (state >> 27)) * 0x94D049BB133111EBUL;
        state ^= state >> 31;
        return (state >> 11) * (1d / 9007199254740992d);
    }

    public static float SampleSigned(
        int seed,
        int heatId,
        int stepNumber,
        int riderId,
        RandomChannel channel,
        params int[] discriminators)
        => (float)(Sample01(seed, heatId, stepNumber, riderId, channel, discriminators) * 2d - 1d);

    private static void Mix(ref ulong state, int value)
    {
        var data = unchecked((uint)value);
        for (var index = 0; index < sizeof(uint); index++)
        {
            state ^= (byte)(data >> (index * 8));
            state *= 1099511628211UL;
        }
    }
}
