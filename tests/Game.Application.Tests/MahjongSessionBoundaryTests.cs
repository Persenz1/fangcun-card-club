using Game.Application.Mahjong;
using Game.Application.Mahjong.Riichi;
using Game.Application.Mahjong.Sichuan;
using Game.Application.Mahjong.Standard;
using Game.Mahjong.Sichuan.State;
using Game.Mahjong.Table;

namespace Game.Application.Tests;

public sealed class MahjongSessionBoundaryTests
{
    [Theory]
    [InlineData(MahjongMode.Standard)]
    [InlineData(MahjongMode.Sichuan)]
    [InlineData(MahjongMode.Riichi)]
    public void Factory_keeps_the_human_hand_visible_and_ai_hands_concealed(MahjongMode mode)
    {
        var session = MahjongSessionFactory.Start(mode, 20260801, MahjongSeat.East);

        var view = session.Snapshot;

        Assert.Equal(mode, view.Mode);
        Assert.All(view.Table.Seats[(int)MahjongSeat.East].Hand, tile =>
        {
            Assert.True(tile.FaceUp);
            Assert.NotNull(tile.Tile);
        });
        Assert.All(view.Table.Seats[(int)MahjongSeat.South].Hand, tile =>
        {
            Assert.False(tile.FaceUp);
            Assert.Null(tile.Tile);
        });
        Assert.Contains(view.SuggestedActionId, view.LegalActions.Select(action => (int?)action.Id));
    }

    [Fact]
    public void Standard_session_dispatches_an_exact_legal_option_and_emits_animation_data()
    {
        var session = StandardMahjongGameSession.Start(7);
        var discard = session.Snapshot.LegalActions.First(action =>
            action.Kind == MahjongActionViewKind.Discard);
        var changed = 0;
        session.StateChanged += () => changed++;

        var result = session.Dispatch(discard.Id);

        Assert.True(result.Accepted, result.Error);
        Assert.Equal(1, changed);
        var animation = Assert.Single(result.Events, @event =>
            @event.Kind == MahjongAnimationEventKind.Discard);
        Assert.Equal(MahjongSeat.East, animation.Seat);
        Assert.Equal(discard.PrimaryTile, animation.Tile);
        Assert.True(animation.DurationMilliseconds > 0);
    }

    [Fact]
    public void Sichuan_session_exposes_exchange_and_void_suit_without_ui_rule_logic()
    {
        var session = SichuanMahjongGameSession.Start(42);
        Assert.All(session.Snapshot.LegalActions, action =>
            Assert.Equal(MahjongActionViewKind.ExchangeThree, action.Kind));

        var exchange = session.DispatchSuggestedAction();
        Assert.True(exchange.Accepted, exchange.Error);
        while (session.RuleSnapshot.Phase == SichuanMahjongPhase.ExchangeThree)
        {
            var ai = session.AdvanceAiTurn();
            Assert.True(ai.Accepted, ai.Error);
        }

        Assert.Equal(SichuanMahjongPhase.DeclareVoidSuit, session.RuleSnapshot.Phase);
        Assert.Equal(3, session.Snapshot.LegalActions.Count);
        Assert.All(session.Snapshot.LegalActions, action =>
            Assert.Equal(MahjongActionViewKind.DeclareVoidSuit, action.Kind));
        Assert.True(session.DispatchSuggestedAction().Accepted);
    }

    [Fact]
    public void Riichi_session_projects_match_hud_and_rule_owned_options()
    {
        var session = RiichiMahjongGameSession.Start(99);
        var view = session.Snapshot;

        Assert.Equal(4, view.Table.Seats.Count);
        Assert.All(view.Table.Seats, seat => Assert.Equal(25000, seat.Score));
        Assert.Single(view.DoraIndicators);
        Assert.Contains(view.HudItems, item => item.Label == "宝牌指示");
        Assert.All(view.LegalActions, action => Assert.Contains(
            action.Kind,
            new[]
            {
                MahjongActionViewKind.Discard,
                MahjongActionViewKind.RiichiDiscard,
                MahjongActionViewKind.Win,
                MahjongActionViewKind.ConcealedKong,
                MahjongActionViewKind.NineTerminalsDraw,
            }));
        Assert.True(session.DispatchSuggestedAction().Accepted);
    }

    [Fact]
    public void Rejected_option_does_not_publish_a_save_notification()
    {
        var session = MahjongSessionFactory.Start(MahjongMode.Standard, 1);
        var changed = 0;
        session.StateChanged += () => changed++;

        var result = session.Dispatch(int.MaxValue);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Equal(0, changed);
        Assert.Empty(session.CreateRecoveryState().AcceptedCommands);
    }

    [Theory]
    [InlineData(MahjongMode.Standard, 2026080118UL)]
    [InlineData(MahjongMode.Sichuan, 2026080119UL)]
    [InlineData(MahjongMode.Riichi, 2026080120UL)]
    public void Unfinished_session_restores_by_seed_and_accepted_command_replay(
        MahjongMode mode,
        ulong seed)
    {
        var original = MahjongSessionFactory.Start(mode, seed);
        Advance(original, 25);
        Assert.False(original.Snapshot.IsFinished);

        var recovery = original.CreateRecoveryState();
        var restored = MahjongSessionFactory.Restore(recovery);

        AssertEquivalent(original, restored);
        var commands = recovery.AcceptedCommands.Count;
        while (!original.Snapshot.IsFinished && commands < 5000)
        {
            var originalResult = Advance(original);
            var restoredResult = Advance(restored);
            Assert.True(originalResult.Accepted, originalResult.Error);
            Assert.True(restoredResult.Accepted, restoredResult.Error);
            Assert.Equal(
                originalResult.Events.Select(@event => @event.Message),
                restoredResult.Events.Select(@event => @event.Message));
            commands++;
        }

        Assert.True(original.Snapshot.IsFinished);
        AssertEquivalent(original, restored);
        Assert.Equal(
            original.CreateRecoveryState().AcceptedCommands.Count,
            restored.CreateRecoveryState().AcceptedCommands.Count);
    }

    [Fact]
    public void Replay_rejects_a_command_that_was_not_legal_in_the_recorded_state()
    {
        var recovery = MahjongSessionFactory.Start(MahjongMode.Standard, 71).CreateRecoveryState();
        recovery.AcceptedCommands.Add(new MahjongCommandRecord
        {
            Kind = MahjongStoredCommandKind.Pass,
            PlayerIndex = 0,
        });

        Assert.Throws<InvalidDataException>(() => MahjongSessionFactory.Restore(recovery));
    }

    [Fact]
    public void Sichuan_adapter_maps_a_complete_blood_battle_to_presentation_events()
    {
        var session = SichuanMahjongGameSession.Start(2026080102);
        var eventKinds = new HashSet<MahjongAnimationEventKind>();
        var acceptedCommands = 0;
        while (!session.Snapshot.IsFinished && acceptedCommands < 600)
        {
            var result = session.Snapshot.IsHumanActionRequired
                ? session.DispatchSuggestedAction()
                : session.AdvanceAiTurn();
            Assert.True(result.Accepted, result.Error);
            eventKinds.UnionWith(result.Events.Select(@event => @event.Kind));
            acceptedCommands++;
        }

        Assert.True(session.Snapshot.IsFinished);
        Assert.Contains(MahjongAnimationEventKind.Exchange, eventKinds);
        Assert.Contains(MahjongAnimationEventKind.Declaration, eventKinds);
        Assert.Contains(MahjongAnimationEventKind.MatchFinished, eventKinds);
        Assert.NotEmpty(session.Snapshot.SettlementLines);
        Assert.Equal(0, session.RuleSnapshot.ScoreChanges.Sum());
    }

    [Fact]
    public void Riichi_adapter_advances_across_hands_to_a_ranked_match_result()
    {
        var session = RiichiMahjongGameSession.Start(2026080103);
        var finishedHands = 0;
        var matchFinished = false;
        var acceptedCommands = 0;
        while (!session.Snapshot.IsFinished && acceptedCommands < 5000)
        {
            var result = session.Snapshot.IsHumanActionRequired
                ? session.DispatchSuggestedAction()
                : session.AdvanceAiTurn();
            Assert.True(result.Accepted, result.Error);
            finishedHands += result.Events.Count(@event =>
                @event.Kind == MahjongAnimationEventKind.HandFinished);
            matchFinished |= result.Events.Any(@event =>
                @event.Kind == MahjongAnimationEventKind.MatchFinished);
            acceptedCommands++;
        }

        Assert.True(session.Snapshot.IsFinished);
        Assert.True(finishedHands >= 4);
        Assert.True(matchFinished);
        Assert.Equal(100000, session.RuleSnapshot.MatchResult!.FinalScores.Sum());
        Assert.Equal(4, session.RuleSnapshot.MatchResult.Ranking.Count);
        Assert.Equal(4, session.Snapshot.SettlementLines.Count);
    }

    private static void Advance(IMahjongGameSession session, int count)
    {
        for (var index = 0; index < count && !session.Snapshot.IsFinished; index++)
        {
            var result = Advance(session);
            Assert.True(result.Accepted, result.Error);
        }
    }

    private static MahjongSessionResult Advance(IMahjongGameSession session)
    {
        return session.Snapshot.IsHumanActionRequired
            ? session.DispatchSuggestedAction()
            : session.AdvanceAiTurn();
    }

    private static void AssertEquivalent(
        IMahjongGameSession expectedSession,
        IMahjongGameSession actualSession)
    {
        var expected = expectedSession.Snapshot;
        var actual = actualSession.Snapshot;
        Assert.Equal(expectedSession.Seed, actualSession.Seed);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.HumanSeat, actual.HumanSeat);
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.IsFinished, actual.IsFinished);
        Assert.Equal(expected.Table.Dealer, actual.Table.Dealer);
        Assert.Equal(expected.Table.CurrentSeat, actual.Table.CurrentSeat);
        Assert.Equal(expected.Table.OfferedReactionSeat, actual.Table.OfferedReactionSeat);
        Assert.Equal(expected.Table.LiveTilesRemaining, actual.Table.LiveTilesRemaining);
        Assert.Equal(expected.LegalActions.Select(action => action.Label), actual.LegalActions.Select(action => action.Label));
        Assert.Equal(expected.SettlementLines, actual.SettlementLines);
        Assert.Equal(expected.LocalOutcome, actual.LocalOutcome);
        for (var seat = 0; seat < 4; seat++)
        {
            Assert.Equal(
                expected.Table.Seats[seat].Hand.Select(tile => tile.Tile),
                actual.Table.Seats[seat].Hand.Select(tile => tile.Tile));
            Assert.Equal(
                expected.Table.Seats[seat].River.Select(tile => tile.Tile),
                actual.Table.Seats[seat].River.Select(tile => tile.Tile));
            Assert.Equal(expected.Table.Seats[seat].Score, actual.Table.Seats[seat].Score);
        }
    }
}
