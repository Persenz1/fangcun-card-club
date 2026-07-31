using Game.Core.Random;
using Game.Doudizhu.Cards;

namespace Game.Doudizhu.Tests;

public sealed class CardDeckTests
{
    [Fact]
    public void Ordered_deck_contains_54_unique_cards()
    {
        var cards = CardDeck.CreateOrdered();

        Assert.Equal(54, cards.Count);
        Assert.Equal(54, cards.Distinct().Count());
        Assert.Single(cards, card => card.Rank == CardRank.SmallJoker);
        Assert.Single(cards, card => card.Rank == CardRank.BigJoker);
    }

    [Fact]
    public void Shuffle_is_reproducible_from_seed()
    {
        var first = CardDeck.CreateShuffled(new SplitMix64Random(20260801));
        var second = CardDeck.CreateShuffled(new SplitMix64Random(20260801));

        Assert.Equal(first, second);
        Assert.NotEqual(CardDeck.CreateOrdered(), first);
    }

    [Fact]
    public void Joker_suit_and_rank_must_match()
    {
        Assert.Throws<ArgumentException>(() => new Card(CardSuit.Spades, CardRank.BigJoker));
        Assert.Throws<ArgumentException>(() => new Card(CardSuit.Joker, CardRank.Ace));
    }
}
