using Game.Core.Random;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Tests;

public sealed class MahjongTileAndWallTests
{
    [Fact]
    public void Standard_set_has_136_unique_physical_tiles_and_four_of_each_kind()
    {
        var tiles = MahjongTileSet.CreateOrdered();

        Assert.Equal(136, tiles.Count);
        Assert.Equal(136, tiles.Distinct().Count());
        Assert.Equal(34, tiles.GroupBy(tile => tile.Kind).Count());
        Assert.All(tiles.GroupBy(tile => tile.Kind), group => Assert.Equal(4, group.Count()));
    }

    [Fact]
    public void Suited_set_has_108_unique_physical_tiles_and_no_honors()
    {
        var tiles = MahjongTileSet.CreateSuitedOrdered();

        Assert.Equal(108, tiles.Count);
        Assert.Equal(108, tiles.Distinct().Count());
        Assert.Equal(27, tiles.GroupBy(tile => tile.Kind).Count());
        Assert.DoesNotContain(tiles, tile => tile.Kind.IsHonor());
        Assert.All(tiles.GroupBy(tile => tile.Kind), group => Assert.Equal(4, group.Count()));
    }

    [Fact]
    public void Shuffle_and_wall_draws_are_deterministic()
    {
        var first = new MahjongWall(new SplitMix64Random(20260801), deadWallSize: 14);
        var second = new MahjongWall(new SplitMix64Random(20260801), deadWallSize: 14);

        var firstDraws = Enumerable.Range(0, 8).Select(_ => first.DrawLive()).ToArray();
        var secondDraws = Enumerable.Range(0, 8).Select(_ => second.DrawLive()).ToArray();

        Assert.Equal(firstDraws, secondDraws);
        Assert.Equal(114, first.LiveTilesRemaining);
        Assert.Equal(14, first.DeadWall.Count);
        var replacement = first.DrawReplacement();
        Assert.Contains(replacement, first.DeadWall);
        Assert.Equal(113, first.LiveTilesRemaining);
        Assert.Equal(3, first.ReplacementTilesRemaining);
    }

    [Fact]
    public void Initial_table_deals_four_hands_and_dealer_draw()
    {
        var table = new MahjongTableState(new SplitMix64Random(7));
        var snapshot = table.Snapshot;

        Assert.Equal(MahjongSeat.East, snapshot.CurrentSeat);
        Assert.Equal(14, snapshot.Hands[(int)MahjongSeat.East].Count);
        Assert.All(snapshot.Hands.Skip(1), hand => Assert.Equal(13, hand.Count));
        Assert.Equal(83, snapshot.LiveTilesRemaining);
        Assert.Equal(53, snapshot.Hands.SelectMany(hand => hand).Distinct().Count());
    }

    [Fact]
    public void Suited_table_can_pause_after_deal_for_opening_exchange()
    {
        var wall = new MahjongWall(MahjongTileSet.CreateSuitedShuffled(new SplitMix64Random(9)));
        var table = new MahjongTableState(wall, drawDealerOpeningTile: false);

        Assert.All(table.Snapshot.Hands, hand => Assert.Equal(13, hand.Count));
        Assert.Null(table.Snapshot.LastDrawnTile);
        Assert.Equal(56, table.Wall.LiveTilesRemaining);
    }

    [Fact]
    public void Discard_then_draw_tracks_river_and_tsumogiri()
    {
        var table = new MahjongTableState(new SplitMix64Random(8));
        var dealerDraw = table.Snapshot.LastDrawnTile!.Value;

        var firstDiscard = table.Discard(MahjongSeat.East, dealerDraw);
        var southDraw = table.DrawCurrent();
        var secondDiscard = table.Discard(MahjongSeat.South, southDraw);

        Assert.True(firstDiscard.IsTsumogiri);
        Assert.True(secondDiscard.IsTsumogiri);
        Assert.Equal(MahjongSeat.West, table.CurrentSeat);
        Assert.Single(table.GetRiver(MahjongSeat.East));
        Assert.Single(table.GetRiver(MahjongSeat.South));
    }
}
