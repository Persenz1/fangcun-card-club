using Game.Core.Random;
using Game.Mahjong.Commands;
using Game.Mahjong.Standard.Actions;
using Game.Mahjong.Standard.State;
using Game.Mahjong.Table;

namespace Game.Mahjong.Standard.Tests;

public sealed class StandardMahjongRuleEngineTests
{
    [Fact]
    public void Dealer_starts_with_every_physical_discard_as_a_legal_action()
    {
        var engine = new StandardMahjongRuleEngine(new SplitMix64Random(1));
        var snapshot = engine.Snapshot;
        var actions = engine.GetLegalActions(snapshot.Table.CurrentSeat);

        Assert.Equal(StandardMahjongPhase.AwaitingDiscard, snapshot.Phase);
        Assert.Equal(
            snapshot.Table.Hands[(int)snapshot.Table.CurrentSeat].Count,
            actions.Count(action => action.Kind == StandardMahjongActionKind.Discard));
        Assert.Empty(engine.GetLegalActions(snapshot.Table.CurrentSeat.Next()));
    }

    [Fact]
    public void Wrong_seat_and_nonoffered_reaction_are_rejected_without_state_change()
    {
        var engine = new StandardMahjongRuleEngine(new SplitMix64Random(2));
        var current = engine.Snapshot.Table.CurrentSeat;
        var wrongSeat = current.Next();
        var tile = engine.Snapshot.Table.Hands[(int)current][0];

        var result = engine.Dispatch(new DiscardMahjongTileCommand((int)wrongSeat, tile));

        Assert.False(result.Accepted);
        Assert.Equal(current, result.Snapshot.Table.CurrentSeat);
        Assert.Equal(StandardMahjongPhase.AwaitingDiscard, result.Snapshot.Phase);
    }

    [Fact]
    public void Offered_reactions_have_one_priority_class_plus_pass()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var engine = new StandardMahjongRuleEngine(new SplitMix64Random(seed));
            for (var turn = 0; turn < 30 && engine.Snapshot.Phase != StandardMahjongPhase.Finished; turn++)
            {
                if (engine.Snapshot.Phase == StandardMahjongPhase.AwaitingReaction)
                {
                    var seat = engine.Snapshot.OfferedReactionSeat!.Value;
                    var classes = engine.GetLegalActions(seat)
                        .Where(action => action.Kind != StandardMahjongActionKind.Pass)
                        .Select(PriorityClass)
                        .Distinct()
                        .ToArray();
                    Assert.Single(classes);
                    Assert.Contains(
                        engine.GetLegalActions(seat),
                        action => action.Kind == StandardMahjongActionKind.Pass);
                    return;
                }

                var current = engine.Snapshot.Table.CurrentSeat;
                var discard = engine.GetLegalActions(current)
                    .First(action => action.Kind == StandardMahjongActionKind.Discard);
                var result = engine.Dispatch(new DiscardMahjongTileCommand((int)current, discard.Tile!.Value));
                Assert.True(result.Accepted, result.Error);
            }
        }

        Assert.Fail("Expected at least one deterministic seed to offer a reaction.");
    }

    private static int PriorityClass(StandardMahjongAction action)
    {
        return action.Kind switch
        {
            StandardMahjongActionKind.DiscardWin => 3,
            StandardMahjongActionKind.Pong or StandardMahjongActionKind.OpenKong => 2,
            StandardMahjongActionKind.Chow => 1,
            _ => 0,
        };
    }
}
