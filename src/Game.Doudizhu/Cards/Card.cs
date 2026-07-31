namespace Game.Doudizhu.Cards;

public readonly record struct Card
{
    public Card(CardSuit suit, CardRank rank)
    {
        var isJokerSuit = suit == CardSuit.Joker;
        var isJokerRank = rank is CardRank.SmallJoker or CardRank.BigJoker;

        if (isJokerSuit != isJokerRank)
        {
            throw new ArgumentException("Joker suit and rank must be used together.");
        }

        Suit = suit;
        Rank = rank;
    }

    public CardSuit Suit { get; }

    public CardRank Rank { get; }

    public bool IsJoker => Suit == CardSuit.Joker;
}
