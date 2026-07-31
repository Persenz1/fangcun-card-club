using Game.Doudizhu.Cards;
using Game.Doudizhu.Patterns;

namespace Game.Doudizhu.Tests;

public sealed class PatternDetectorTests
{
    public static TheoryData<IReadOnlyCollection<Card>, CardPatternKind, CardRank, int> ValidPatterns => new()
    {
        { TestCards.Of((CardRank.Three, 1)), CardPatternKind.Single, CardRank.Three, 1 },
        { TestCards.Of((CardRank.Four, 2)), CardPatternKind.Pair, CardRank.Four, 1 },
        { TestCards.Of((CardRank.Five, 3)), CardPatternKind.Triple, CardRank.Five, 1 },
        { TestCards.Of((CardRank.Six, 3), (CardRank.Ace, 1)), CardPatternKind.TripleWithSingle, CardRank.Six, 1 },
        { TestCards.Of((CardRank.Seven, 3), (CardRank.Two, 2)), CardPatternKind.TripleWithPair, CardRank.Seven, 1 },
        {
            TestCards.Of(
                (CardRank.Three, 1),
                (CardRank.Four, 1),
                (CardRank.Five, 1),
                (CardRank.Six, 1),
                (CardRank.Seven, 1)),
            CardPatternKind.Straight,
            CardRank.Seven,
            5
        },
        {
            TestCards.Of((CardRank.Nine, 2), (CardRank.Ten, 2), (CardRank.Jack, 2)),
            CardPatternKind.PairStraight,
            CardRank.Jack,
            3
        },
        {
            TestCards.Of((CardRank.Queen, 3), (CardRank.King, 3)),
            CardPatternKind.Airplane,
            CardRank.King,
            2
        },
        {
            TestCards.Of((CardRank.Three, 3), (CardRank.Four, 3), (CardRank.Ace, 2)),
            CardPatternKind.AirplaneWithSingles,
            CardRank.Four,
            2
        },
        {
            TestCards.Of((CardRank.Three, 3), (CardRank.Four, 3), (CardRank.Six, 2), (CardRank.Seven, 2)),
            CardPatternKind.AirplaneWithPairs,
            CardRank.Four,
            2
        },
        {
            TestCards.Of((CardRank.Eight, 4), (CardRank.Ace, 2)),
            CardPatternKind.FourWithSingles,
            CardRank.Eight,
            1
        },
        {
            TestCards.Of((CardRank.Eight, 4), (CardRank.Nine, 2), (CardRank.Ten, 2)),
            CardPatternKind.FourWithPairs,
            CardRank.Eight,
            1
        },
        { TestCards.Of((CardRank.Two, 4)), CardPatternKind.Bomb, CardRank.Two, 1 },
        {
            TestCards.Of((CardRank.SmallJoker, 1), (CardRank.BigJoker, 1)),
            CardPatternKind.Rocket,
            CardRank.BigJoker,
            1
        },
    };

    [Theory]
    [MemberData(nameof(ValidPatterns))]
    public void Detects_each_frozen_pattern(
        IReadOnlyCollection<Card> cards,
        CardPatternKind expectedKind,
        CardRank expectedMainRank,
        int expectedSequenceLength)
    {
        var pattern = PatternDetector.Detect(cards);

        Assert.NotNull(pattern);
        Assert.Equal(expectedKind, pattern.Value.Kind);
        Assert.Equal(expectedMainRank, pattern.Value.MainRank);
        Assert.Equal(expectedSequenceLength, pattern.Value.SequenceLength);
        Assert.Equal(cards.Count, pattern.Value.CardCount);
    }

    [Fact]
    public void Sequence_bodies_cannot_include_two_or_jokers()
    {
        var straightThroughTwo = TestCards.Of(
            (CardRank.Ten, 1),
            (CardRank.Jack, 1),
            (CardRank.Queen, 1),
            (CardRank.King, 1),
            (CardRank.Ace, 1),
            (CardRank.Two, 1));
        var pairSequenceThroughTwo = TestCards.Of(
            (CardRank.Queen, 2),
            (CardRank.King, 2),
            (CardRank.Ace, 2),
            (CardRank.Two, 2));

        Assert.Null(PatternDetector.Detect(straightThroughTwo));
        Assert.Null(PatternDetector.Detect(pairSequenceThroughTwo));
    }

    [Fact]
    public void Single_wings_can_split_a_pair_and_use_body_fourth_cards()
    {
        var splitPair = TestCards.Of((CardRank.Three, 3), (CardRank.Four, 3), (CardRank.Seven, 2));
        var bodyFourthCards = TestCards.Of((CardRank.Three, 4), (CardRank.Four, 4));

        Assert.Equal(CardPatternKind.AirplaneWithSingles, PatternDetector.Detect(splitPair)?.Kind);
        Assert.Equal(CardPatternKind.AirplaneWithSingles, PatternDetector.Detect(bodyFourthCards)?.Kind);
    }

    [Fact]
    public void Single_wings_do_not_split_an_unrelated_triple_or_complete_bomb()
    {
        var tripleAsWings = TestCards.Of(
            (CardRank.Three, 3),
            (CardRank.Four, 3),
            (CardRank.Five, 3),
            (CardRank.Seven, 3));
        var bombAsWings = TestCards.Of(
            (CardRank.Three, 3),
            (CardRank.Four, 3),
            (CardRank.Five, 3),
            (CardRank.Six, 3),
            (CardRank.Nine, 4));

        Assert.Null(PatternDetector.Detect(tripleAsWings));
        Assert.Null(PatternDetector.Detect(bombAsWings));
    }

    [Fact]
    public void Two_nonconsecutive_bombs_are_not_misread_as_four_with_pairs()
    {
        var cards = TestCards.Of((CardRank.Three, 4), (CardRank.Five, 4));

        Assert.Null(PatternDetector.Detect(cards));
    }
}
