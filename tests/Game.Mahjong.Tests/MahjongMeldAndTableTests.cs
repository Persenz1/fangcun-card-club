using Game.Core.Random;
using Game.Mahjong.Hands;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Tests;

public sealed class MahjongMeldAndTableTests
{
    [Fact]
    public void Meld_rejects_honored_chow_and_mismatched_pong()
    {
        var tiles = MahjongTileSet.CreateOrdered();

        Assert.Throws<ArgumentException>(() => new MahjongMeld(
            MahjongMeldType.Chow,
            [
                tiles.First(tile => tile.Kind == MahjongTileKind.East && tile.CopyIndex == 0),
                tiles.First(tile => tile.Kind == MahjongTileKind.South && tile.CopyIndex == 0),
                tiles.First(tile => tile.Kind == MahjongTileKind.West && tile.CopyIndex == 0),
            ],
            MahjongSeat.East));
        Assert.Throws<ArgumentException>(() => new MahjongMeld(
            MahjongMeldType.Pong,
            [
                tiles.First(tile => tile.Kind == MahjongTileKind.Characters1 && tile.CopyIndex == 0),
                tiles.First(tile => tile.Kind == MahjongTileKind.Characters1 && tile.CopyIndex == 1),
                tiles.First(tile => tile.Kind == MahjongTileKind.Characters2 && tile.CopyIndex == 0),
            ],
            MahjongSeat.East));
    }

    [Fact]
    public void Claiming_pong_consumes_two_tiles_and_marks_source_river()
    {
        MahjongTableState? table = null;
        MahjongSeat caller = default;
        MahjongTile discard = default;

        for (ulong seed = 1; seed <= 100 && table is null; seed++)
        {
            var candidate = new MahjongTableState(new SplitMix64Random(seed));
            foreach (var tile in candidate.GetConcealedTiles(MahjongSeat.East))
            {
                var matchingCaller = Enum.GetValues<MahjongSeat>()
                    .Where(seat => seat != MahjongSeat.East)
                    .FirstOrDefault(seat => candidate.GetConcealedTiles(seat)
                        .Count(held => held.Kind == tile.Kind) >= 2);
                if (matchingCaller != MahjongSeat.East)
                {
                    table = candidate;
                    caller = matchingCaller;
                    discard = tile;
                    break;
                }
            }
        }

        Assert.NotNull(table);
        var used = table.GetConcealedTiles(caller)
            .Where(tile => tile.Kind == discard.Kind)
            .Take(2)
            .ToArray();
        var countBefore = table.GetConcealedTiles(caller).Count;
        table.Discard(MahjongSeat.East, discard);

        var meld = table.ClaimDiscard(caller, MahjongMeldType.Pong, used);

        Assert.Equal(MahjongMeldType.Pong, meld.Type);
        Assert.Equal(countBefore - 2, table.GetConcealedTiles(caller).Count);
        Assert.True(table.GetRiver(MahjongSeat.East).Single().IsClaimed);
        Assert.Equal(caller, table.CurrentSeat);
    }
}
