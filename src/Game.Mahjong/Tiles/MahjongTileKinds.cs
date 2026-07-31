namespace Game.Mahjong.Tiles;

public static class MahjongTileKinds
{
    public const int KindCount = 34;

    public static IReadOnlyList<MahjongTileKind> All { get; } = Enum.GetValues<MahjongTileKind>();

    public static MahjongTileSuit GetSuit(this MahjongTileKind kind)
    {
        Validate(kind);
        var value = (int)kind;
        return value switch
        {
            < 9 => MahjongTileSuit.Characters,
            < 18 => MahjongTileSuit.Dots,
            < 27 => MahjongTileSuit.Bamboo,
            _ => MahjongTileSuit.Honors,
        };
    }

    public static int GetNumber(this MahjongTileKind kind)
    {
        var suit = kind.GetSuit();
        return suit == MahjongTileSuit.Honors ? 0 : ((int)kind % 9) + 1;
    }

    public static bool IsSuited(this MahjongTileKind kind)
    {
        return kind.GetSuit() != MahjongTileSuit.Honors;
    }

    public static bool IsHonor(this MahjongTileKind kind)
    {
        return kind.GetSuit() == MahjongTileSuit.Honors;
    }

    public static bool IsTerminal(this MahjongTileKind kind)
    {
        return kind.IsSuited() && kind.GetNumber() is 1 or 9;
    }

    public static bool IsTerminalOrHonor(this MahjongTileKind kind)
    {
        return kind.IsHonor() || kind.IsTerminal();
    }

    public static MahjongTileKind FromSuitAndNumber(MahjongTileSuit suit, int number)
    {
        if (suit == MahjongTileSuit.Honors || number is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(number));
        }

        var offset = suit switch
        {
            MahjongTileSuit.Characters => 0,
            MahjongTileSuit.Dots => 9,
            MahjongTileSuit.Bamboo => 18,
            _ => throw new ArgumentOutOfRangeException(nameof(suit)),
        };
        return (MahjongTileKind)(offset + number - 1);
    }

    private static void Validate(MahjongTileKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }
}
