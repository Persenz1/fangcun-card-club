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
    }
}
