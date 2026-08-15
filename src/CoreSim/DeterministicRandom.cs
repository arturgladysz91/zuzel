namespace CoreSim;

/// <summary>
/// Stateless deterministic samples. A draw is addressed by domain keys, so
/// collection order cannot reassign randomness to another rider or event.
/// </summary>
public static class DeterministicRandom
{
    public static float Sample01(int seed, params int[] keys)
    {
        var hash = unchecked((uint)seed) ^ 0x9E3779B9u;
        foreach (var key in keys)
        {
            hash ^= unchecked((uint)key) + 0x9E3779B9u + (hash << 6) + (hash >> 2);
            hash *= 0x85EBCA6Bu;
            hash ^= hash >> 13;
        }

        hash ^= hash >> 16;
        return (hash & 0x00FFFFFFu) / 16777216f;
    }

    public static float SampleSigned(int seed, params int[] keys)
        => Sample01(seed, keys) * 2f - 1f;
}
