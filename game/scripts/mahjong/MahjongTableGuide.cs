using Godot;

namespace FangcunCardClub.Game.Mahjong;

public partial class MahjongTableGuide : Control
{
    private static readonly Color FeltColor = new("4adbd0b8");
    private static readonly Color SafeColor = new("f3d36ca8");
    private static readonly Color WallColor = new("f09b55c8");
    private static readonly Color RiverColor = new("c58affc8");
    private static readonly Color HandColor = new("63e69bd8");
    private static readonly Color ConsoleColor = new("7ea9ffc0");

    public override void _Draw()
    {
        DrawClosedPolyline(MahjongTableCalibration.FeltOutline, FeltColor, 2f);
        DrawClosedPolyline(MahjongTableCalibration.TileSafeOutline, SafeColor, 1.5f);

        foreach (var wall in MahjongTableCalibration.WallLines)
        {
            DrawLine(wall.Start, wall.End, WallColor, 2f, true);
        }

        foreach (var riverZone in MahjongTableCalibration.RiverZones)
        {
            DrawRect(riverZone, RiverColor, false, 1.5f, true);
        }

        foreach (var hand in MahjongTableCalibration.HandLines)
        {
            DrawLine(hand.Start, hand.End, HandColor, 2.5f, true);
        }

        foreach (var meld in MahjongTableCalibration.MeldLines)
        {
            DrawLine(meld.Start, meld.End, HandColor.Darkened(0.32f), 1.5f, true);
        }

        var dora = MahjongTableCalibration.DoraIndicatorLine;
        DrawLine(dora.Start, dora.End, ConsoleColor, 2f, true);
        DrawRect(MahjongTableCalibration.CenterConsoleZone, ConsoleColor, false, 1.5f, true);
    }

    private void DrawClosedPolyline(Vector2[] points, Color color, float width)
    {
        for (var index = 0; index < points.Length; index++)
        {
            DrawLine(points[index], points[(index + 1) % points.Length], color, width, true);
        }
    }
}
