using Game.Doudizhu.Cards;
using Game.Doudizhu.Moves;
using Game.Doudizhu.Settlement;

namespace Game.Doudizhu.State;

public sealed class DoudizhuSnapshot
{
    public DoudizhuSnapshot(
        DoudizhuPhase phase,
        int currentPlayerIndex,
        int firstBidderIndex,
        DoudizhuBidPrompt? bidPrompt,
        int? landlordIndex,
        IEnumerable<IEnumerable<Card>> hands,
        IEnumerable<Card> bottomCards,
        DoudizhuMove? lastMove,
        int? lastMovePlayerIndex,
        int multiplier,
        int redealCount,
        IEnumerable<int> successfulPlayCounts,
        DoudizhuSettlement? settlement)
    {
        Phase = phase;
        CurrentPlayerIndex = currentPlayerIndex;
        FirstBidderIndex = firstBidderIndex;
        BidPrompt = bidPrompt;
        LandlordIndex = landlordIndex;
        Hands = Array.AsReadOnly(hands
            .Select(hand => (IReadOnlyList<Card>)Array.AsReadOnly(hand.ToArray()))
            .ToArray());
        BottomCards = Array.AsReadOnly(bottomCards.ToArray());
        LastMove = lastMove;
        LastMovePlayerIndex = lastMovePlayerIndex;
        Multiplier = multiplier;
        RedealCount = redealCount;
        SuccessfulPlayCounts = Array.AsReadOnly(successfulPlayCounts.ToArray());
        Settlement = settlement;
    }

    public DoudizhuPhase Phase { get; }

    public int CurrentPlayerIndex { get; }

    public int FirstBidderIndex { get; }

    public DoudizhuBidPrompt? BidPrompt { get; }

    public int? LandlordIndex { get; }

    public IReadOnlyList<IReadOnlyList<Card>> Hands { get; }

    public IReadOnlyList<Card> BottomCards { get; }

    public DoudizhuMove? LastMove { get; }

    public int? LastMovePlayerIndex { get; }

    public int Multiplier { get; }

    public int RedealCount { get; }

    public IReadOnlyList<int> SuccessfulPlayCounts { get; }

    public DoudizhuSettlement? Settlement { get; }
}
