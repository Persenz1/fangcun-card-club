using Game.Core.Simulation;
using Game.Mahjong.Hands;
using Game.Mahjong.Standard.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Standard.Events;

public sealed record StandardTileDrawnEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongTile Tile,
    bool IsReplacement) : IGameEvent;

public sealed record StandardTileDiscardedEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongRiverTile RiverTile) : IGameEvent;

public sealed record StandardMeldDeclaredEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongMeld Meld) : IGameEvent;

public sealed record StandardReactionPassedEvent(
    long Sequence,
    MahjongSeat Seat) : IGameEvent;

public sealed record StandardMahjongFinishedEvent(
    long Sequence,
    StandardMahjongSettlement Settlement) : IGameEvent;
