using Game.Mahjong.Tiles;

namespace Game.Mahjong.Table;

public sealed record MahjongRiverTile(
    MahjongTile Tile,
    bool IsTsumogiri,
    bool IsClaimed,
    long Sequence);
