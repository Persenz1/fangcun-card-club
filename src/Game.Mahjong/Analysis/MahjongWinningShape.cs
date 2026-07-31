using Game.Mahjong.Tiles;

namespace Game.Mahjong.Analysis;

public enum MahjongWinningShapeKind
{
    Standard,
    SevenPairs,
    ThirteenOrphans,
}

public enum MahjongGroupType
{
    Sequence,
    Triplet,
}

public sealed record MahjongGroup(MahjongGroupType Type, MahjongTileKind FirstKind);

public sealed class MahjongWinningShape
{
    public MahjongWinningShape(
        MahjongWinningShapeKind kind,
        MahjongTileKind? pairKind = null,
        IEnumerable<MahjongGroup>? concealedGroups = null)
    {
        Kind = kind;
        PairKind = pairKind;
        ConcealedGroups = Array.AsReadOnly((concealedGroups ?? []).ToArray());
    }

    public MahjongWinningShapeKind Kind { get; }

    public MahjongTileKind? PairKind { get; }

    public IReadOnlyList<MahjongGroup> ConcealedGroups { get; }
}

public sealed record MahjongWinningOptions(
    bool AllowSevenPairs = false,
    bool AllowThirteenOrphans = false,
    bool SevenPairsRequireDistinctKinds = true);
