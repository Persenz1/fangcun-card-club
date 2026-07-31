using Game.Application.Doudizhu;
using Game.Core.Simulation;
using Game.Doudizhu.AI;
using Game.Doudizhu.Cards;
using Game.Doudizhu.State;

namespace Game.Application.Tests;

public sealed class DoudizhuGameSessionTests
{
    [Fact]
    public void Accepted_command_log_restores_the_exact_rule_state()
    {
        var original = DoudizhuGameSession.Start(20260801, humanPlayerIndex: 0);
        Advance(original, 20);

        var restored = DoudizhuGameSession.Restore(original.CreateRecoveryState());

        AssertEquivalent(original.RuleSnapshot, restored.RuleSnapshot);
        Assert.Equal(
            original.CreateRecoveryState().AcceptedCommands.Count,
            restored.CreateRecoveryState().AcceptedCommands.Count);
    }

    [Fact]
    public void Restored_session_continues_to_the_same_result()
    {
        var original = DoudizhuGameSession.Start(42, humanPlayerIndex: 0);
        Advance(original, 25);
        var restored = DoudizhuGameSession.Restore(original.CreateRecoveryState());

        Finish(original);
        Finish(restored);

        AssertEquivalent(original.RuleSnapshot, restored.RuleSnapshot);
        Assert.Equal(
            original.CreateRecoveryState().AcceptedCommands.Count,
            restored.CreateRecoveryState().AcceptedCommands.Count);
    }

    [Fact]
    public void Tampered_but_well_formed_command_log_is_rejected_during_replay()
    {
        var recovery = new DoudizhuRecoveryState
        {
            Seed = 7,
            AcceptedCommands =
            [
                DoudizhuCommandRecord.FromCommand(new Game.Doudizhu.Commands.PassCommand(0)),
            ],
        };

        Assert.Throws<InvalidDataException>(() => DoudizhuGameSession.Restore(recovery));
    }

    [Fact]
    public void Player_dispatch_cannot_control_an_ai_seat()
    {
        var session = DoudizhuGameSession.Start(1, humanPlayerIndex: 0);
        var aiPlayer = (session.HumanPlayerIndex + 1) % 3;
        IGameCommand command = new Game.Doudizhu.Commands.BidCommand(
            aiPlayer,
            Game.Doudizhu.Commands.DoudizhuBidAction.Pass);

        var result = session.Dispatch(command);

        Assert.False(result.Accepted);
        Assert.Empty(result.Events);
        Assert.Empty(session.CreateRecoveryState().AcceptedCommands);
    }

    private static void Advance(DoudizhuGameSession session, int maximumCommands)
    {
        var ai = new BasicDoudizhuAi();
        for (var count = 0;
             count < maximumCommands && session.RuleSnapshot.Phase != DoudizhuPhase.Finished;
             count++)
        {
            var result = session.Snapshot.IsHumanTurn
                ? session.Dispatch(ai.ChooseCommand(
                    session.Snapshot.PlayerObservation,
                    session.Snapshot.LegalMoves))
                : session.AdvanceAiTurn();
            Assert.True(result.Accepted, result.Error);
        }
    }

    private static void Finish(DoudizhuGameSession session)
    {
        Advance(session, 500);
        Assert.Equal(DoudizhuPhase.Finished, session.RuleSnapshot.Phase);
    }

    private static void AssertEquivalent(DoudizhuSnapshot expected, DoudizhuSnapshot actual)
    {
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.CurrentPlayerIndex, actual.CurrentPlayerIndex);
        Assert.Equal(expected.FirstBidderIndex, actual.FirstBidderIndex);
        Assert.Equal(expected.BidPrompt, actual.BidPrompt);
        Assert.Equal(expected.LandlordIndex, actual.LandlordIndex);
        Assert.Equal(expected.Multiplier, actual.Multiplier);
        Assert.Equal(expected.RedealCount, actual.RedealCount);
        Assert.Equal(expected.Hands.Select(CardKey), actual.Hands.Select(CardKey));
        Assert.Equal(
            expected.LastMove is null ? null : CardKey(expected.LastMove.Cards),
            actual.LastMove is null ? null : CardKey(actual.LastMove.Cards));
        Assert.Equal(expected.SuccessfulPlayCounts, actual.SuccessfulPlayCounts);
        Assert.Equal(expected.Settlement?.WinningTeam, actual.Settlement?.WinningTeam);
        Assert.Equal(expected.Settlement?.FinalMultiplier, actual.Settlement?.FinalMultiplier);
        Assert.Equal(expected.Settlement?.ScoreChanges, actual.Settlement?.ScoreChanges);
    }

    private static string CardKey(IEnumerable<Card> cards)
    {
        return string.Join(",", cards
            .OrderBy(card => card.Rank)
            .ThenBy(card => card.Suit)
            .Select(card => $"{(int)card.Rank}:{(int)card.Suit}"));
    }
}
