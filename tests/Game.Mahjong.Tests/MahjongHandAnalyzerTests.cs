using Game.Mahjong.Analysis;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Tests;

public sealed class MahjongHandAnalyzerTests
{
    [Fact]
    public void Finds_standard_closed_and_open_shapes()
    {
        var closed = Kinds(
            (MahjongTileKind.Characters1, 1),
            (MahjongTileKind.Characters2, 1),
            (MahjongTileKind.Characters3, 1),
            (MahjongTileKind.Characters4, 1),
            (MahjongTileKind.Characters5, 1),
            (MahjongTileKind.Characters6, 1),
            (MahjongTileKind.Characters7, 1),
            (MahjongTileKind.Characters8, 1),
            (MahjongTileKind.Characters9, 1),
            (MahjongTileKind.Dots1, 3),
            (MahjongTileKind.Bamboo2, 2));
        var openRemainder = Kinds(
            (MahjongTileKind.Characters1, 1),
            (MahjongTileKind.Characters2, 1),
            (MahjongTileKind.Characters3, 1),
            (MahjongTileKind.Dots1, 3),
            (MahjongTileKind.Bamboo2, 2));

        Assert.Contains(
            MahjongHandAnalyzer.Analyze(closed),
            shape => shape.Kind == MahjongWinningShapeKind.Standard && shape.ConcealedGroups.Count == 4);
        Assert.True(MahjongHandAnalyzer.IsWinning(openRemainder, openMeldCount: 2));
    }

    [Fact]
    public void Ambiguous_hand_exposes_standard_and_seven_pairs_shapes()
    {
        var hand = Kinds(
            (MahjongTileKind.Characters1, 2),
            (MahjongTileKind.Characters2, 2),
            (MahjongTileKind.Characters3, 2),
            (MahjongTileKind.Characters4, 2),
            (MahjongTileKind.Characters5, 2),
            (MahjongTileKind.Characters6, 2),
            (MahjongTileKind.Characters7, 2));
        var shapes = MahjongHandAnalyzer.Analyze(
            hand,
            options: new MahjongWinningOptions(AllowSevenPairs: true));

        Assert.Contains(shapes, shape => shape.Kind == MahjongWinningShapeKind.Standard);
        Assert.Contains(shapes, shape => shape.Kind == MahjongWinningShapeKind.SevenPairs);
    }

    [Fact]
    public void Detects_thirteen_orphans_and_rejects_it_when_disabled()
    {
        var hand = MahjongTileKinds.All
            .Where(kind => kind.IsTerminalOrHonor())
            .Append(MahjongTileKind.East)
            .ToArray();

        Assert.False(MahjongHandAnalyzer.IsWinning(hand));
        Assert.Contains(
            MahjongHandAnalyzer.Analyze(
                hand,
                options: new MahjongWinningOptions(AllowThirteenOrphans: true)),
            shape => shape.Kind == MahjongWinningShapeKind.ThirteenOrphans);
    }

    [Fact]
    public void Enumerates_exact_winning_tile_kinds()
    {
        var readyHand = Kinds(
            (MahjongTileKind.Characters1, 1),
            (MahjongTileKind.Characters2, 1),
            (MahjongTileKind.Characters3, 1),
            (MahjongTileKind.Dots1, 1),
            (MahjongTileKind.Dots2, 1),
            (MahjongTileKind.Dots3, 1),
            (MahjongTileKind.Bamboo1, 1),
            (MahjongTileKind.Bamboo2, 1),
            (MahjongTileKind.Bamboo3, 1),
            (MahjongTileKind.East, 3),
            (MahjongTileKind.Red, 1));

        Assert.Equal([MahjongTileKind.Red], MahjongHandAnalyzer.GetWinningKinds(readyHand));
    }

    [Fact]
    public void Four_identical_tiles_are_not_two_distinct_seven_pairs()
    {
        var hand = Kinds(
            (MahjongTileKind.Characters1, 4),
            (MahjongTileKind.Characters2, 2),
            (MahjongTileKind.Characters3, 2),
            (MahjongTileKind.Characters4, 2),
            (MahjongTileKind.Characters5, 2),
            (MahjongTileKind.Characters6, 2));

        Assert.DoesNotContain(
            MahjongHandAnalyzer.Analyze(
                hand,
                options: new MahjongWinningOptions(AllowSevenPairs: true)),
            shape => shape.Kind == MahjongWinningShapeKind.SevenPairs);
        Assert.Contains(
            MahjongHandAnalyzer.Analyze(
                hand,
                options: new MahjongWinningOptions(
                    AllowSevenPairs: true,
                    SevenPairsRequireDistinctKinds: false)),
            shape => shape.Kind == MahjongWinningShapeKind.SevenPairs);
    }

    private static IReadOnlyList<MahjongTileKind> Kinds(params (MahjongTileKind Kind, int Count)[] groups)
    {
        return groups.SelectMany(group => Enumerable.Repeat(group.Kind, group.Count)).ToArray();
    }
}
