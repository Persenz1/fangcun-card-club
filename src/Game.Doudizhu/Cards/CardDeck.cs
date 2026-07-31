using Game.Core.Random;

namespace Game.Doudizhu.Cards;

public static class CardDeck
{
    private static readonly CardSuit[] StandardSuits =
    [
        CardSuit.Clubs,
        CardSuit.Diamonds,
        CardSuit.Hearts,
        CardSuit.Spades,
    ];

    public static IReadOnlyList<Card> CreateOrdered()
    {
        var cards = new List<Card>(54);

        foreach (var rank in Enum.GetValues<CardRank>())
        {
            if (rank is CardRank.SmallJoker or CardRank.BigJoker)
            {
                continue;
            }

            foreach (var suit in StandardSuits)
            {
                cards.Add(new Card(suit, rank));
            }
        }

        cards.Add(new Card(CardSuit.Joker, CardRank.SmallJoker));
        cards.Add(new Card(CardSuit.Joker, CardRank.BigJoker));
        return cards;
    }

    public static IReadOnlyList<Card> CreateShuffled(IDeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var cards = CreateOrdered().ToArray();
        for (var index = cards.Length - 1; index > 0; index--)
        {
            var otherIndex = random.NextInt(index + 1);
            (cards[index], cards[otherIndex]) = (cards[otherIndex], cards[index]);
        }

        return cards;
    }
}
