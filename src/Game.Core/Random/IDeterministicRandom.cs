namespace Game.Core.Random;

public interface IDeterministicRandom
{
    ulong NextUInt64();

    int NextInt(int exclusiveMax);
}
