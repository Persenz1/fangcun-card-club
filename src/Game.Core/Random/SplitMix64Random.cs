namespace Game.Core.Random;

/// <summary>
/// Small deterministic generator used for reproducible shuffles and replays.
/// Its algorithm is owned by the project instead of relying on System.Random.
/// </summary>
public sealed class SplitMix64Random : IDeterministicRandom
{
    private const ulong Increment = 0x9E3779B97F4A7C15UL;
    private ulong _state;

    public SplitMix64Random(ulong seed)
    {
        _state = seed;
    }

    public ulong NextUInt64()
    {
        _state += Increment;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    public int NextInt(int exclusiveMax)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMax);

        var bound = (ulong)exclusiveMax;
        var rejectionThreshold = unchecked(0UL - bound) % bound;
        ulong value;

        do
        {
            value = NextUInt64();
        }
        while (value < rejectionThreshold);

        return (int)(value % bound);
    }
}
