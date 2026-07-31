using Game.Mahjong.Standard.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Standard.Tests;

public sealed class StandardMahjongScorerTests
{
    [Fact]
    public void Seven_pairs_clean_suit_self_draw_uses_highest_combined_fan_and_zero_sum()
    {
        var hand = Enumerable.Range(1, 7)
            .SelectMany(number => Enumerable.Repeat(
                MahjongTileKinds.FromSuitAndNumber(MahjongTileSuit.Characters, number),
                2));

        var result = StandardMahjongScorer.Calculate(
            hand,
            [],
            MahjongSeat.South,
            discardSource: null,
            baseScore: 10);

        Assert.Equal(13, result.Fan);
        Assert.Contains("七对", result.Patterns);
        Assert.Contains("清一色", result.Patterns);
        Assert.True(result.SelfDraw);
        Assert.Equal(0, result.ScoreChanges.Sum());
        Assert.Equal(-81_920, result.ScoreChanges[(int)MahjongSeat.East]);
        Assert.Equal(163_840, result.ScoreChanges[(int)MahjongSeat.South]);
    }

    [Fact]
    public void Dealer_or_discarder_dealer_doubles_ron_payment()
    {
        var hand = Kinds(
            (MahjongTileKind.Characters1, 1),
            (MahjongTileKind.Characters2, 1),
            (MahjongTileKind.Characters3, 1),
            (MahjongTileKind.Dots1, 1),
            (MahjongTileKind.Dots2, 1),
            (MahjongTileKind.Dots3, 1),
            (MahjongTileKind.Bamboo1, 1),
            (MahjongTileKind.Bamboo2, 1),
            (MahjongTileKind.Bamboo3, 1),
            (MahjongTileKind.Red, 3),
            (MahjongTileKind.North, 2));

        var result = StandardMahjongScorer.Calculate(
            hand,
            [],
            MahjongSeat.South,
            MahjongSeat.East,
            baseScore: 10);

        Assert.False(result.SelfDraw);
        Assert.Equal(0, result.ScoreChanges.Sum());
        Assert.True(result.ScoreChanges[(int)MahjongSeat.South] > 0);
        Assert.Equal(
            -result.ScoreChanges[(int)MahjongSeat.South],
            result.ScoreChanges[(int)MahjongSeat.East]);
    }

    private static IEnumerable<MahjongTileKind> Kinds(params (MahjongTileKind Kind, int Count)[] groups)
    {
        return groups.SelectMany(group => Enumerable.Repeat(group.Kind, group.Count));
    }
}
