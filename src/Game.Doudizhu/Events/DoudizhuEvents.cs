using Game.Core.Simulation;
using Game.Doudizhu.Cards;
using Game.Doudizhu.Commands;
using Game.Doudizhu.Moves;
using Game.Doudizhu.Settlement;

namespace Game.Doudizhu.Events;

public sealed record BidMadeEvent(
    long Sequence,
    int PlayerIndex,
    DoudizhuBidAction Action,
    int Multiplier) : IGameEvent;

public sealed record CardsRedealtEvent(
    long Sequence,
    int RedealCount,
    int FirstBidderIndex) : IGameEvent;

public sealed record LandlordDeterminedEvent(
    long Sequence,
    int LandlordIndex,
    IReadOnlyList<Card> BottomCards,
    int Multiplier) : IGameEvent;

public sealed record CardsPlayedEvent(
    long Sequence,
    int PlayerIndex,
    DoudizhuMove Move,
    int Multiplier) : IGameEvent;

public sealed record PlayerPassedEvent(
    long Sequence,
    int PlayerIndex) : IGameEvent;

public sealed record TrickResetEvent(
    long Sequence,
    int LeaderIndex) : IGameEvent;

public sealed record DoudizhuFinishedEvent(
    long Sequence,
    DoudizhuSettlement Settlement) : IGameEvent;
