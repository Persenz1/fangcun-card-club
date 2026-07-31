using Game.Doudizhu.Cards;
using Game.Doudizhu.Commands;
using Game.Doudizhu.Moves;

namespace Game.Doudizhu.State;

public sealed class DoudizhuObservation
{
    public DoudizhuObservation(
        int playerIndex,
        DoudizhuPhase phase,
        int currentPlayerIndex,
        DoudizhuBidPrompt? bidPrompt,
        int? landlordIndex,
        IEnumerable<Card> hand,
        IEnumerable<int> remainingCardCounts,
        IEnumerable<Card> visibleBottomCards,
        DoudizhuMove? lastMove,
        int? lastMovePlayerIndex,
        int multiplier,
        int redealCount,
        bool canPass)
    {
        PlayerIndex = playerIndex;
        Phase = phase;
        CurrentPlayerIndex = currentPlayerIndex;
        BidPrompt = bidPrompt;
        LandlordIndex = landlordIndex;
        Hand = Array.AsReadOnly(hand.ToArray());
        RemainingCardCounts = Array.AsReadOnly(remainingCardCounts.ToArray());
        VisibleBottomCards = Array.AsReadOnly(visibleBottomCards.ToArray());
        LastMove = lastMove;
        LastMovePlayerIndex = lastMovePlayerIndex;
        Multiplier = multiplier;
        RedealCount = redealCount;
        CanPass = canPass;
    }

    public int PlayerIndex { get; }

    public DoudizhuPhase Phase { get; }

    public int CurrentPlayerIndex { get; }

    public DoudizhuBidPrompt? BidPrompt { get; }

    public int? LandlordIndex { get; }

    public IReadOnlyList<Card> Hand { get; }

    public IReadOnlyList<int> RemainingCardCounts { get; }

    public IReadOnlyList<Card> VisibleBottomCards { get; }

    public DoudizhuMove? LastMove { get; }

    public int? LastMovePlayerIndex { get; }

    public int Multiplier { get; }

    public int RedealCount { get; }

    public bool CanPass { get; }

    public IReadOnlyList<DoudizhuBidAction> LegalBidActions => BidPrompt switch
    {
        DoudizhuBidPrompt.Call => [DoudizhuBidAction.Call, DoudizhuBidAction.Pass],
        DoudizhuBidPrompt.Rob => [DoudizhuBidAction.Rob, DoudizhuBidAction.Pass],
        _ => [],
    };
}
