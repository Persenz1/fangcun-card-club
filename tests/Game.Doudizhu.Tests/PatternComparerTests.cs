using Game.Doudizhu.Cards;
using Game.Doudizhu.Patterns;

namespace Game.Doudizhu.Tests;

public sealed class PatternComparerTests
{
    [Fact]
    public void Same_structure_compares_main_rank_but_different_lengths_do_not_compare()
    {
        var fiveThroughNine = Detect(TestCards.Of(
            (CardRank.Five, 1),
            (CardRank.Six, 1),
            (CardRank.Seven, 1),
            (CardRank.Eight, 1),
            (CardRank.Nine, 1)));
        var sixThroughTen = Detect(TestCards.Of(
            (CardRank.Six, 1),
            (CardRank.Seven, 1),
            (CardRank.Eight, 1),
            (CardRank.Nine, 1),
            (CardRank.Ten, 1)));
        var fiveThroughTen = Detect(TestCards.Of(
            (CardRank.Five, 1),
            (CardRank.Six, 1),
            (CardRank.Seven, 1),
            (CardRank.Eight, 1),
            (CardRank.Nine, 1),
            (CardRank.Ten, 1)));

        Assert.True(PatternComparer.CanBeat(sixThroughTen, fiveThroughNine));
        Assert.False(PatternComparer.CanBeat(fiveThroughNine, sixThroughTen));
        Assert.False(PatternComparer.CanBeat(fiveThroughTen, fiveThroughNine));
    }

    [Fact]
    public void Bombs_and_rocket_follow_global_priority()
    {
        var pairOfTwos = Detect(TestCards.Of((CardRank.Two, 2)));
        var threesBomb = Detect(TestCards.Of((CardRank.Three, 4)));
        var foursBomb = Detect(TestCards.Of((CardRank.Four, 4)));
        var rocket = Detect(TestCards.Of((CardRank.SmallJoker, 1), (CardRank.BigJoker, 1)));

        Assert.True(PatternComparer.CanBeat(threesBomb, pairOfTwos));
        Assert.True(PatternComparer.CanBeat(foursBomb, threesBomb));
        Assert.True(PatternComparer.CanBeat(rocket, foursBomb));
        Assert.False(PatternComparer.CanBeat(foursBomb, rocket));
        Assert.False(PatternComparer.CanBeat(rocket, rocket));
    }

    private static CardPattern Detect(IReadOnlyCollection<Card> cards)
    {
        return PatternDetector.Detect(cards) ?? throw new InvalidOperationException("Test cards must form a pattern.");
    }
}
