using Game.Core.Simulation;
using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.Scoring;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Events;

public sealed record RiichiHandStartedEvent(
    long Sequence,
    RiichiRoundWind RoundWind,
    int HandNumber,
    MahjongSeat Dealer,
    int Honba,
    int RiichiSticks,
    MahjongTileKind DoraIndicator) : IGameEvent;

public sealed record RiichiTileDrawnEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongTile Tile,
    bool IsReplacement) : IGameEvent;

public sealed record RiichiTileDiscardedEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongRiverTile RiverTile) : IGameEvent;

public sealed record RiichiDeclaredEvent(
    long Sequence,
    MahjongSeat Seat,
    bool IsDoubleRiichi) : IGameEvent;

public sealed record RiichiMeldDeclaredEvent(
    long Sequence,
    MahjongSeat Seat,
    MahjongMeld Meld) : IGameEvent;

public sealed record RiichiDoraRevealedEvent(
    long Sequence,
    MahjongTileKind Indicator) : IGameEvent;

public sealed record RiichiReactionPassedEvent(
    long Sequence,
    MahjongSeat Seat,
    bool PassedWin) : IGameEvent;

public sealed record RiichiWinSettledEvent(
    long Sequence,
    RiichiWinResult Win) : IGameEvent;

public sealed record RiichiHandFinishedEvent(
    long Sequence,
    RiichiHandResult Result) : IGameEvent;

public sealed record RiichiMatchFinishedEvent(
    long Sequence,
    RiichiMatchResult Result) : IGameEvent;
