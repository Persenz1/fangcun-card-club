using Game.Mahjong.Hands;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.Actions;

public enum SichuanMahjongActionKind
{
    ExchangeThree,
    DeclareVoidSuit,
    Discard,
    SelfDrawWin,
    DiscardWin,
    Pong,
    OpenKong,
    ConcealedKong,
    AddedKong,
    Pass,
}

public sealed class SichuanMahjongAction
{
    public SichuanMahjongAction(
        SichuanMahjongActionKind kind,
        MahjongTile? tile = null,
        MahjongTileSuit? suit = null,
        MahjongMeldType? meldType = null,
        IEnumerable<MahjongTile>? concealedTiles = null)
    {
        Kind = kind;
        Tile = tile;
        Suit = suit;
        MeldType = meldType;
        ConcealedTiles = Array.AsReadOnly((concealedTiles ?? []).ToArray());
    }

    public SichuanMahjongActionKind Kind { get; }

    public MahjongTile? Tile { get; }

    public MahjongTileSuit? Suit { get; }

    public MahjongMeldType? MeldType { get; }

    public IReadOnlyList<MahjongTile> ConcealedTiles { get; }
}
