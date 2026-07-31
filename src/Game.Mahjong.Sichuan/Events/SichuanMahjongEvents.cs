using Game.Core.Simulation;
using Game.Mahjong.Hands;
using Game.Mahjong.Sichuan.Scoring;
using Game.Mahjong.Sichuan.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.Events;

public sealed record SichuanExchangeSubmittedEvent(
    long Sequence,
    MahjongSeat Seat) : IGameEvent;

public sealed record SichuanTilesExchangedEvent(
    long Sequence,
    SichuanExchangeDirection Direction) : IGameEvent;

public sealed record SichuanVoidSuitDeclaredEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongTileSuit Suit) : IGameEvent;

public sealed record SichuanTileDrawnEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongTile Tile,
    bool IsReplacement) : IGameEvent;

public sealed record SichuanTileDiscardedEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongRiverTile RiverTile) : IGameEvent;

public sealed record SichuanMeldDeclaredEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongMeld Meld,
    IReadOnlyList<long> ScoreChanges) : IGameEvent;

public sealed record SichuanReactionPassedEvent(
    long Sequence,
    MahjongSeat Seat) : IGameEvent;

public sealed record SichuanWinSettledEvent(
    long Sequence,
    SichuanWinResult Win) : IGameEvent;

public sealed record SichuanMahjongFinishedEvent(
    long Sequence,
    SichuanMahjongSettlement Settlement) : IGameEvent;
