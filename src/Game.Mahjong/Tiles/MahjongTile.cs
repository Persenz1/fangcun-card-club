namespace Game.Mahjong.Tiles;

public readonly record struct MahjongTile
{
    public MahjongTile(MahjongTileKind kind, byte copyIndex)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (copyIndex > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(copyIndex));
        }

        Kind = kind;
        CopyIndex = copyIndex;
    }

    public MahjongTileKind Kind { get; }

    public byte CopyIndex { get; }
}
