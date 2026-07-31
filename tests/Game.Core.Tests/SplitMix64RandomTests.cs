using Game.Core.Random;

namespace Game.Core.Tests;

public sealed class SplitMix64RandomTests
{
    [Fact]
    public void Same_seed_produces_same_sequence()
    {
        var first = new SplitMix64Random(20260801);
        var second = new SplitMix64Random(20260801);

        var firstSequence = Enumerable.Range(0, 32).Select(_ => first.NextUInt64());
        var secondSequence = Enumerable.Range(0, 32).Select(_ => second.NextUInt64());

        Assert.Equal(firstSequence, secondSequence);
    }

    [Fact]
    public void NextInt_stays_inside_requested_range()
    {
        var random = new SplitMix64Random(42);

        var values = Enumerable.Range(0, 1_000).Select(_ => random.NextInt(7));

        Assert.All(values, value => Assert.InRange(value, 0, 6));
    }
}
