using Godot;

namespace FangcunCardClub.Game.Mahjong;

public static class MahjongTableCalibration
{
    public const int WallStacksPerSide = 17;
    public const int RiverColumns = 6;
    public const int RiverRows = 5;

    public static readonly Vector2 LogicalSize = new(960f, 540f);

    public static readonly Vector2[] FeltOutline =
    [
        new(273f, 75f),
        new(731f, 75f),
        new(860f, 437f),
        new(134f, 437f),
    ];

    public static readonly Vector2[] TileSafeOutline =
    [
        new(290f, 104f),
        new(714f, 104f),
        new(814f, 414f),
        new(180f, 414f),
    ];

    public static readonly (Vector2 Start, Vector2 End)[] WallLines =
    [
        (new Vector2(425f, 132f), new Vector2(605f, 132f)),
        (new Vector2(728f, 138f), new Vector2(780f, 324f)),
        (new Vector2(350f, 334f), new Vector2(650f, 334f)),
        (new Vector2(274f, 138f), new Vector2(222f, 324f)),
    ];

    public static readonly (Vector2 Start, Vector2 End)[] RiverLines =
    [
        (new Vector2(440f, 159f), new Vector2(520f, 159f)),
        (new Vector2(590f, 181f), new Vector2(590f, 241f)),
        (new Vector2(438f, 287f), new Vector2(522f, 287f)),
        (new Vector2(382f, 181f), new Vector2(382f, 241f)),
    ];

    public static readonly Rect2[] RiverZones =
    [
        new(410f, 144f, 140f, 31f),
        new(570f, 165f, 40f, 96f),
        new(410f, 276f, 140f, 34f),
        new(350f, 165f, 40f, 96f),
    ];

    public static readonly (Vector2 Start, Vector2 End) PlayerHandLine =
        (new Vector2(276f, 384f), new Vector2(718f, 384f));

    public static readonly (Vector2 Start, Vector2 End)[] HandLines =
    [
        (new Vector2(398f, 105f), new Vector2(622f, 105f)),
        (new Vector2(808f, 142f), new Vector2(838f, 330f)),
        PlayerHandLine,
        (new Vector2(194f, 142f), new Vector2(164f, 330f)),
    ];

    public static readonly (Vector2 Start, Vector2 End)[] MeldLines =
    [
        (new Vector2(294f, 116f), new Vector2(382f, 116f)),
        (new Vector2(774f, 350f), new Vector2(774f, 270f)),
        (new Vector2(180f, 370f), new Vector2(262f, 370f)),
        (new Vector2(226f, 350f), new Vector2(226f, 270f)),
    ];

    public static readonly (Vector2 Start, Vector2 End) DoraIndicatorLine =
        (new Vector2(430f, 148f), new Vector2(530f, 148f));

    public static readonly Rect2 CenterConsoleZone = new(420f, 180f, 120f, 95f);

    public static Vector2 PointOnLine(
        (Vector2 Start, Vector2 End) line,
        int index,
        int count)
    {
        if (count <= 1)
        {
            return line.Start.Lerp(line.End, 0.5f);
        }

        return line.Start.Lerp(line.End, index / (count - 1f));
    }

    public static Vector2 RiverTilePoint(int side, int index)
    {
        if (side is < 0 or >= 4)
        {
            throw new ArgumentOutOfRangeException(nameof(side));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var zone = RiverZones[side];
        var horizontal = side is 0 or 2;
        var primaryIndex = index % RiverColumns;
        var secondaryIndex = (index / RiverColumns) % RiverRows;
        var primaryProgress = (primaryIndex + 0.5f) / RiverColumns;
        var secondaryProgress = (secondaryIndex + 0.5f) / RiverRows;
        return horizontal
            ? new Vector2(
                zone.Position.X + (zone.Size.X * primaryProgress),
                zone.Position.Y + (zone.Size.Y * secondaryProgress))
            : new Vector2(
                zone.Position.X + (zone.Size.X * secondaryProgress),
                zone.Position.Y + (zone.Size.Y * primaryProgress));
    }
}
