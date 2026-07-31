using Game.Core.Random;
using Game.Doudizhu.Cards;
using Game.Doudizhu.Moves;
using Game.Doudizhu.Patterns;

namespace Game.Doudizhu.Tests;

public sealed class LegalMoveGeneratorTests
{
    [Fact]
    public void Lead_generation_covers_every_frozen_pattern_without_duplicates()
    {
        var hand = TestCards.Of(
            (CardRank.Three, 4),
            (CardRank.Four, 4),
            (CardRank.Five, 2),
            (CardRank.Six, 2),
            (CardRank.Seven, 1),
            (CardRank.Eight, 1),
            (CardRank.Nine, 1),
            (CardRank.Ten, 1),
            (CardRank.Jack, 1),
            (CardRank.SmallJoker, 1),
            (CardRank.BigJoker, 1));

        var moves = LegalMoveGenerator.Generate(hand);

        Assert.Equal(Enum.GetValues<CardPatternKind>(), moves.Select(move => move.Pattern.Kind).Distinct());
        Assert.Equal(moves.Count, moves.Select(move => TestCards.Key(move.Cards)).Distinct().Count());
        Assert.All(moves, move => Assert.Equal(move.Pattern, PatternDetector.Detect(move.Cards)));
    }

    [Fact]
    public void Follow_generation_returns_only_beating_pairs_bombs_and_rocket()
    {
        var hand = TestCards.Of(
            (CardRank.Eight, 3),
            (CardRank.Nine, 4),
            (CardRank.SmallJoker, 1),
            (CardRank.BigJoker, 1));
        var previous = PatternDetector.Detect(TestCards.Of((CardRank.Seven, 2)))!.Value;

        var moves = LegalMoveGenerator.Generate(hand, previous);

        Assert.Equal(11, moves.Count);
        Assert.Equal(9, moves.Count(move => move.Pattern.Kind == CardPatternKind.Pair));
        Assert.Single(moves, move => move.Pattern.Kind == CardPatternKind.Bomb);
        Assert.Single(moves, move => move.Pattern.Kind == CardPatternKind.Rocket);
        Assert.All(moves, move => Assert.True(PatternComparer.CanBeat(move.Pattern, previous)));
    }

    [Fact]
    public void Nothing_can_follow_a_rocket()
    {
        var hand = CardDeck.CreateOrdered();
        var rocket = PatternDetector.Detect(TestCards.Of(
            (CardRank.SmallJoker, 1),
            (CardRank.BigJoker, 1)))!.Value;

        Assert.Empty(LegalMoveGenerator.Generate(hand, rocket));
    }

    [Fact]
    public void Structural_generation_matches_exhaustive_subsets_for_small_hands()
    {
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var hand = CardDeck.CreateShuffled(new SplitMix64Random(seed)).Take(12).ToArray();
            var expected = EnumerateDetectableSubsets(hand);
            var actual = LegalMoveGenerator.Generate(hand)
                .Select(move => TestCards.Key(move.Cards))
                .ToHashSet();

            Assert.Equal(expected, actual);
        }
    }

    private static HashSet<string> EnumerateDetectableSubsets(IReadOnlyList<Card> hand)
    {
        var moves = new HashSet<string>();
        for (var mask = 1; mask < 1 << hand.Count; mask++)
        {
            var cards = Enumerable.Range(0, hand.Count)
                .Where(index => (mask & (1 << index)) != 0)
                .Select(index => hand[index])
                .ToArray();
            if (PatternDetector.Detect(cards) is not null)
            {
                moves.Add(TestCards.Key(cards));
            }
        }

        return moves;
    }
}
