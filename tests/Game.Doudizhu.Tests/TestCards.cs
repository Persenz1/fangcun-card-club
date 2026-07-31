using Game.Doudizhu.Cards;

namespace Game.Doudizhu.Tests;

internal static class TestCards
{
    private static readonly CardSuit[] Suits =
    [
        CardSuit.Clubs,
        CardSuit.Diamonds,
        CardSuit.Hearts,
        CardSuit.Spades,
    ];

    public static IReadOnlyList<Card> Of(params (CardRank Rank, int Count)[] groups)
    {
        var cards = new List<Card>();
        foreach (var (rank, count) in groups)
        {
            if (rank is CardRank.SmallJoker or CardRank.BigJoker)
            {
                if (count != 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(groups), "A joker can appear only once.");
                }

                cards.Add(new Card(CardSuit.Joker, rank));
                continue;
            }

            if (count is < 1 or > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(groups), "A normal rank must contain one to four cards.");
            }

            cards.AddRange(Suits.Take(count).Select(suit => new Card(suit, rank)));
        }

        return cards;
    }

    public static string Key(IEnumerable<Card> cards)
    {
        return string.Join(",", cards
            .OrderBy(card => card.Rank)
            .ThenBy(card => card.Suit)
            .Select(card => $"{(int)card.Rank}:{(int)card.Suit}"));
    }
}
