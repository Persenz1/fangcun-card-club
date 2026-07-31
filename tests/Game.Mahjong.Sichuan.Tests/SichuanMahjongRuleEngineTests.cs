using Game.Core.Random;
using Game.Mahjong.Sichuan.Actions;
using Game.Mahjong.Sichuan.Commands;
using Game.Mahjong.Sichuan.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.Tests;

public sealed class SichuanMahjongRuleEngineTests
{
    [Fact]
    public void Round_starts_with_four_thirteen_tile_hands_and_same_suit_exchange_actions()
    {
        var engine = new SichuanMahjongRuleEngine(new SplitMix64Random(1));

        Assert.Equal(SichuanMahjongPhase.ExchangeThree, engine.Snapshot.Phase);
        Assert.All(engine.Snapshot.Table.Hands, hand => Assert.Equal(13, hand.Count));
        Assert.Equal(56, engine.Snapshot.Table.LiveTilesRemaining);
        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            var actions = engine.GetLegalActions(seat);
            Assert.NotEmpty(actions);
            Assert.All(actions, action =>
            {
                Assert.Equal(SichuanMahjongActionKind.ExchangeThree, action.Kind);
                Assert.Equal(3, action.ConcealedTiles.Count);
                Assert.Single(action.ConcealedTiles.Select(tile => tile.Kind.GetSuit()).Distinct());
            });
        }
    }

    [Fact]
    public void Mixed_suit_exchange_is_rejected_without_consuming_submission()
    {
        var engine = new SichuanMahjongRuleEngine(new SplitMix64Random(2));
        var hand = engine.Snapshot.Table.Hands[(int)MahjongSeat.East];
        var mixed = hand
            .GroupBy(tile => tile.Kind.GetSuit())
            .Take(2)
            .SelectMany(group => group.Take(2))
            .Take(3)
            .ToArray();

        var result = engine.Dispatch(new ExchangeThreeTilesCommand((int)MahjongSeat.East, mixed));

        Assert.False(result.Accepted);
        Assert.False(result.Snapshot.ExchangeSubmitted[(int)MahjongSeat.East]);
        Assert.Equal(SichuanMahjongPhase.ExchangeThree, result.Snapshot.Phase);
    }

    [Fact]
    public void Four_exchanges_then_four_void_declarations_start_dealer_turn()
    {
        var engine = new SichuanMahjongRuleEngine(new SplitMix64Random(3));
        SubmitFirstExchangeForEverySeat(engine);

        Assert.Equal(SichuanMahjongPhase.DeclareVoidSuit, engine.Snapshot.Phase);
        Assert.All(engine.Snapshot.Table.Hands, hand => Assert.Equal(13, hand.Count));

        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            var hand = engine.Snapshot.Table.Hands[(int)seat];
            var suit = Enum.GetValues<MahjongTileSuit>()
                .Where(candidate => candidate != MahjongTileSuit.Honors)
                .OrderBy(candidate => hand.Count(tile => tile.Kind.GetSuit() == candidate))
                .First();
            var result = engine.Dispatch(new DeclareVoidSuitCommand((int)seat, suit));
            Assert.True(result.Accepted, result.Error);
        }

        var snapshot = engine.Snapshot;
        Assert.Equal(SichuanMahjongPhase.AwaitingDiscard, snapshot.Phase);
        Assert.Equal(snapshot.Table.Dealer, snapshot.Table.CurrentSeat);
        Assert.Equal(14, snapshot.Table.Hands[(int)snapshot.Table.Dealer].Count);
        Assert.Equal(55, snapshot.Table.LiveTilesRemaining);
    }

    [Fact]
    public void Void_suit_tiles_are_the_only_legal_discards_until_cleared()
    {
        var engine = new SichuanMahjongRuleEngine(new SplitMix64Random(4));
        SubmitFirstExchangeForEverySeat(engine);
        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            var chosen = engine.Snapshot.Table.Hands[(int)seat][0].Kind.GetSuit();
            Assert.True(engine.Dispatch(new DeclareVoidSuitCommand((int)seat, chosen)).Accepted);
        }

        var current = engine.Snapshot.Table.CurrentSeat;
        var voidSuit = engine.Snapshot.VoidSuits[(int)current]!.Value;
        var actions = engine.GetLegalActions(current);

        Assert.NotEmpty(actions);
        Assert.All(actions, action =>
        {
            Assert.Equal(SichuanMahjongActionKind.Discard, action.Kind);
            Assert.Equal(voidSuit, action.Tile!.Value.Kind.GetSuit());
        });
    }

    private static void SubmitFirstExchangeForEverySeat(SichuanMahjongRuleEngine engine)
    {
        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            var action = engine.GetLegalActions(seat)[0];
            var result = engine.Dispatch(new ExchangeThreeTilesCommand((int)seat, action.ConcealedTiles));
            Assert.True(result.Accepted, result.Error);
        }
    }
}
