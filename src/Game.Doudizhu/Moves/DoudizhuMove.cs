using Game.Doudizhu.Cards;
using Game.Doudizhu.Patterns;

namespace Game.Doudizhu.Moves;

public sealed class DoudizhuMove
{
    public DoudizhuMove(IEnumerable<Card> cards, CardPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(cards);

        Cards = Array.AsReadOnly(cards
            .OrderBy(card => card.Rank)
            .ThenBy(card => card.Suit)
            .ToArray());
        Pattern = pattern;
    }

    public IReadOnlyList<Card> Cards { get; }

    public CardPattern Pattern { get; }
}
