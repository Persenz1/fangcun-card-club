using Game.Core.Simulation;
using Game.Doudizhu.Cards;

namespace Game.Doudizhu.Commands;

public enum DoudizhuBidAction
{
    Call,
    Rob,
    Pass,
}

public sealed record BidCommand(int PlayerIndex, DoudizhuBidAction Action) : IGameCommand;

public sealed class PlayCardsCommand : IGameCommand
{
    public PlayCardsCommand(int playerIndex, IEnumerable<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        PlayerIndex = playerIndex;
        Cards = Array.AsReadOnly(cards.ToArray());
    }

    public int PlayerIndex { get; }

    public IReadOnlyList<Card> Cards { get; }
}

public sealed record PassCommand(int PlayerIndex) : IGameCommand;
