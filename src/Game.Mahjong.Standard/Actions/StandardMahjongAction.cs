using Game.Mahjong.Hands;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Standard.Actions;

public enum StandardMahjongActionKind
{
    Discard,
    SelfDrawWin,
    DiscardWin,
    Chow,
    Pong,
    OpenKong,
    ConcealedKong,
    AddedKong,
    Pass,
}

public sealed class StandardMahjongAction
{
    public StandardMahjongAction(
        StandardMahjongActionKind kind,
        MahjongTile? tile = null,
        MahjongMeldType? meldType = null,
        IEnumerable<MahjongTile>? concealedTiles = null)
    {
        Kind = kind;
        Tile = tile;
        MeldType = meldType;
        ConcealedTiles = Array.AsReadOnly((concealedTiles ?? []).ToArray());
    }

    public StandardMahjongActionKind Kind { get; }

    public MahjongTile? Tile { get; }

    public MahjongMeldType? MeldType { get; }

    public IReadOnlyList<MahjongTile> ConcealedTiles { get; }
}
