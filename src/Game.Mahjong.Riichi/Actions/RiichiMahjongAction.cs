using Game.Mahjong.Hands;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Actions;

public enum RiichiMahjongActionKind
{
    Discard,
    RiichiDiscard,
    SelfDrawWin,
    DiscardWin,
    Chow,
    Pong,
    OpenKong,
    ConcealedKong,
    AddedKong,
    NineTerminalsDraw,
    Pass,
}

public sealed class RiichiMahjongAction
{
    public RiichiMahjongAction(
        RiichiMahjongActionKind kind,
        MahjongTile? tile = null,
        MahjongMeldType? meldType = null,
        IEnumerable<MahjongTile>? concealedTiles = null)
    {
        Kind = kind;
        Tile = tile;
        MeldType = meldType;
        ConcealedTiles = Array.AsReadOnly((concealedTiles ?? []).ToArray());
    }

    public RiichiMahjongActionKind Kind { get; }

    public MahjongTile? Tile { get; }

    public MahjongMeldType? MeldType { get; }

    public IReadOnlyList<MahjongTile> ConcealedTiles { get; }
}
