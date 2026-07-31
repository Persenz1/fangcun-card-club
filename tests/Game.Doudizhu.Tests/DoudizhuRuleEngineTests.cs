using Game.Core.Random;
using Game.Doudizhu.Commands;
using Game.Doudizhu.Events;
using Game.Doudizhu.Patterns;
using Game.Doudizhu.State;

namespace Game.Doudizhu.Tests;

public sealed class DoudizhuRuleEngineTests
{
    [Fact]
    public void Initial_deal_has_three_hands_and_hidden_bottom_cards()
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(20260801));
        var snapshot = engine.Snapshot;

        Assert.Equal(DoudizhuPhase.Bidding, snapshot.Phase);
        Assert.All(snapshot.Hands, hand => Assert.Equal(17, hand.Count));
        Assert.Equal(3, snapshot.BottomCards.Count);
        Assert.Equal(54, snapshot.Hands.SelectMany(hand => hand).Concat(snapshot.BottomCards).Distinct().Count());
        Assert.Empty(engine.GetObservation(snapshot.CurrentPlayerIndex).VisibleBottomCards);
    }

    [Fact]
    public void All_pass_redeals_and_rotates_first_bidder()
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(7));
        var originalFirstBidder = engine.Snapshot.FirstBidderIndex;
        DoudizhuCommandResult result = null!;

        for (var decision = 0; decision < 3; decision++)
        {
            result = engine.Dispatch(new BidCommand(
                engine.Snapshot.CurrentPlayerIndex,
                DoudizhuBidAction.Pass));
            Assert.True(result.Accepted, result.Error);
        }

        Assert.Equal(DoudizhuPhase.Bidding, result.Snapshot.Phase);
        Assert.Equal(1, result.Snapshot.RedealCount);
        Assert.Equal((originalFirstBidder + 1) % 3, result.Snapshot.FirstBidderIndex);
        Assert.Equal(result.Snapshot.FirstBidderIndex, result.Snapshot.CurrentPlayerIndex);
        Assert.Contains(result.Events, gameEvent => gameEvent is CardsRedealtEvent);
    }

    [Fact]
    public void Caller_can_counter_after_another_player_robs()
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(8));
        var caller = engine.Snapshot.CurrentPlayerIndex;

        AssertAccepted(engine.Dispatch(new BidCommand(caller, DoudizhuBidAction.Call)));
        var robber = engine.Snapshot.CurrentPlayerIndex;
        AssertAccepted(engine.Dispatch(new BidCommand(robber, DoudizhuBidAction.Rob)));
        AssertAccepted(engine.Dispatch(new BidCommand(
            engine.Snapshot.CurrentPlayerIndex,
            DoudizhuBidAction.Pass)));

        Assert.Equal(caller, engine.Snapshot.CurrentPlayerIndex);
        Assert.Equal(DoudizhuBidPrompt.Rob, engine.Snapshot.BidPrompt);

        var result = engine.Dispatch(new BidCommand(caller, DoudizhuBidAction.Rob));

        AssertAccepted(result);
        Assert.Equal(DoudizhuPhase.Playing, result.Snapshot.Phase);
        Assert.Equal(caller, result.Snapshot.LandlordIndex);
        Assert.Equal(caller, result.Snapshot.CurrentPlayerIndex);
        Assert.Equal(4, result.Snapshot.Multiplier);
        Assert.Equal(20, result.Snapshot.Hands[caller].Count);
        Assert.Equal(3, engine.GetObservation(robber).VisibleBottomCards.Count);
    }

    [Fact]
    public void A_player_who_declined_to_call_is_not_offered_a_rob()
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(9));
        var declinedPlayer = engine.Snapshot.CurrentPlayerIndex;
        AssertAccepted(engine.Dispatch(new BidCommand(declinedPlayer, DoudizhuBidAction.Pass)));
        var caller = engine.Snapshot.CurrentPlayerIndex;
        AssertAccepted(engine.Dispatch(new BidCommand(caller, DoudizhuBidAction.Call)));
        var onlyRobber = engine.Snapshot.CurrentPlayerIndex;

        var result = engine.Dispatch(new BidCommand(onlyRobber, DoudizhuBidAction.Pass));

        AssertAccepted(result);
        Assert.Equal(DoudizhuPhase.Playing, result.Snapshot.Phase);
        Assert.Equal(caller, result.Snapshot.LandlordIndex);
        Assert.NotEqual(declinedPlayer, result.Snapshot.CurrentPlayerIndex);
    }

    [Fact]
    public void Two_passes_clear_the_target_and_return_lead_to_last_player()
    {
        var engine = StartPlaying(10);
        var leader = engine.Snapshot.CurrentPlayerIndex;
        var lead = engine.GetLegalMoves(leader)
            .Where(move => move.Pattern.Kind == CardPatternKind.Single)
            .MinBy(move => move.Pattern.MainRank)!;

        AssertAccepted(engine.Dispatch(new PlayCardsCommand(leader, lead.Cards)));
        AssertAccepted(engine.Dispatch(new PassCommand(engine.Snapshot.CurrentPlayerIndex)));
        var result = engine.Dispatch(new PassCommand(engine.Snapshot.CurrentPlayerIndex));

        AssertAccepted(result);
        Assert.Equal(leader, result.Snapshot.CurrentPlayerIndex);
        Assert.Null(result.Snapshot.LastMove);
        Assert.Contains(result.Events, gameEvent => gameEvent is TrickResetEvent reset && reset.LeaderIndex == leader);
        Assert.False(engine.GetObservation(leader).CanPass);
    }

    [Fact]
    public void Playing_a_bomb_doubles_the_multiplier()
    {
        DoudizhuRuleEngine? engine = null;
        for (ulong seed = 1; seed < 200; seed++)
        {
            var candidate = StartPlaying(seed);
            if (candidate.GetLegalMoves(candidate.Snapshot.CurrentPlayerIndex)
                .Any(move => move.Pattern.Kind == CardPatternKind.Bomb))
            {
                engine = candidate;
                break;
            }
        }

        Assert.NotNull(engine);
        var multiplierBefore = engine.Snapshot.Multiplier;
        var bomb = engine.GetLegalMoves(engine.Snapshot.CurrentPlayerIndex)
            .First(move => move.Pattern.Kind == CardPatternKind.Bomb);

        var result = engine.Dispatch(new PlayCardsCommand(engine.Snapshot.CurrentPlayerIndex, bomb.Cards));

        AssertAccepted(result);
        Assert.Equal(multiplierBefore * 2, result.Snapshot.Multiplier);
    }

    [Fact]
    public void Rejected_command_does_not_advance_turn_or_sequence()
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(11));
        var before = engine.Snapshot;

        var result = engine.Dispatch(new BidCommand(before.CurrentPlayerIndex, DoudizhuBidAction.Rob));

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(before.CurrentPlayerIndex, result.Snapshot.CurrentPlayerIndex);
        Assert.Equal(before.Phase, result.Snapshot.Phase);
    }

    private static DoudizhuRuleEngine StartPlaying(ulong seed)
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(seed));
        AssertAccepted(engine.Dispatch(new BidCommand(
            engine.Snapshot.CurrentPlayerIndex,
            DoudizhuBidAction.Call)));

        while (engine.Snapshot.Phase == DoudizhuPhase.Bidding)
        {
            AssertAccepted(engine.Dispatch(new BidCommand(
                engine.Snapshot.CurrentPlayerIndex,
                DoudizhuBidAction.Pass)));
        }

        return engine;
    }

    private static void AssertAccepted(DoudizhuCommandResult result)
    {
        Assert.True(result.Accepted, result.Error);
    }
}
