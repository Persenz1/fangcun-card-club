using Game.Core.Random;
using Game.Doudizhu.AI;
using Game.Doudizhu.Events;
using Game.Doudizhu.State;

namespace Game.Doudizhu.Tests;

public sealed class DoudizhuSimulationTests
{
    [Fact]
    public void Basic_ai_finishes_deterministic_games_without_illegal_commands_or_lost_cards()
    {
        for (ulong seed = 1; seed <= 128; seed++)
        {
            var result = RunGame(seed);

            Assert.InRange(result.CommandCount, 1, 500);
            Assert.Equal(54, result.AllCards.Count);
            Assert.Equal(54, result.AllCards.Distinct().Count());
            Assert.Equal(0, result.Settlement.ScoreChanges.Sum());
        }
    }

    [Fact]
    public void Same_seed_and_ai_produce_the_same_completed_game()
    {
        var first = RunGame(20260801);
        var second = RunGame(20260801);

        Assert.Equal(first.CommandCount, second.CommandCount);
        Assert.Equal(first.EventLog, second.EventLog);
        Assert.Equal(first.Settlement.WinningTeam, second.Settlement.WinningTeam);
        Assert.Equal(first.Settlement.FinalMultiplier, second.Settlement.FinalMultiplier);
        Assert.Equal(first.Settlement.ScoreChanges, second.Settlement.ScoreChanges);
    }

    private static SimulationResult RunGame(ulong seed)
    {
        var engine = new DoudizhuRuleEngine(new SplitMix64Random(seed));
        var ai = new BasicDoudizhuAi();
        var playedCards = new List<Game.Doudizhu.Cards.Card>();
        var eventLog = new List<string>();
        var commandCount = 0;

        while (engine.Snapshot.Phase != DoudizhuPhase.Finished && commandCount < 500)
        {
            var playerIndex = engine.Snapshot.CurrentPlayerIndex;
            var observation = engine.GetObservation(playerIndex);
            var command = ai.ChooseCommand(observation, engine.GetLegalMoves(playerIndex));
            var commandResult = engine.Dispatch(command);

            Assert.True(commandResult.Accepted, commandResult.Error);
            commandCount++;

            foreach (var gameEvent in commandResult.Events)
            {
                eventLog.Add(gameEvent switch
                {
                    BidMadeEvent bid => $"bid:{bid.PlayerIndex}:{bid.Action}:{bid.Multiplier}",
                    CardsRedealtEvent redealt => $"redeal:{redealt.RedealCount}:{redealt.FirstBidderIndex}",
                    LandlordDeterminedEvent landlord => $"landlord:{landlord.LandlordIndex}:{landlord.Multiplier}",
                    CardsPlayedEvent played => $"play:{played.PlayerIndex}:{TestCards.Key(played.Move.Cards)}",
                    PlayerPassedEvent passed => $"pass:{passed.PlayerIndex}",
                    TrickResetEvent reset => $"reset:{reset.LeaderIndex}",
                    DoudizhuFinishedEvent finished => $"finish:{finished.Settlement.WinningTeam}",
                    _ => gameEvent.GetType().Name,
                });

                if (gameEvent is CardsPlayedEvent cardsPlayed)
                {
                    playedCards.AddRange(cardsPlayed.Move.Cards);
                }
            }
        }

        var snapshot = engine.Snapshot;
        Assert.Equal(DoudizhuPhase.Finished, snapshot.Phase);
        Assert.NotNull(snapshot.Settlement);

        var allCards = snapshot.Hands.SelectMany(hand => hand).Concat(playedCards).ToArray();
        return new SimulationResult(commandCount, eventLog, allCards, snapshot.Settlement);
    }

    private sealed record SimulationResult(
        int CommandCount,
        IReadOnlyList<string> EventLog,
        IReadOnlyList<Game.Doudizhu.Cards.Card> AllCards,
        Game.Doudizhu.Settlement.DoudizhuSettlement Settlement);
}
