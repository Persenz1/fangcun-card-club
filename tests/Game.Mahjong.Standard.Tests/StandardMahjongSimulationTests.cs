using Game.Core.Random;
using Game.Mahjong.Standard.AI;
using Game.Mahjong.Standard.Events;
using Game.Mahjong.Standard.State;

namespace Game.Mahjong.Standard.Tests;

public sealed class StandardMahjongSimulationTests
{
    [Fact]
    public void Basic_ai_completes_deterministic_rounds_with_only_accepted_commands()
    {
        var winningRounds = 0;
        var roundsWithMelds = 0;
        for (ulong seed = 1; seed <= 64; seed++)
        {
            var result = RunRound(seed);

            Assert.InRange(result.CommandCount, 1, 500);
            Assert.Equal(0, result.ScoreChanges.Sum());
            winningRounds += result.IsDraw ? 0 : 1;
            roundsWithMelds += result.EventLog.Any(entry => entry.StartsWith("meld:", StringComparison.Ordinal)) ? 1 : 0;
        }

        Assert.True(winningRounds > 0);
        Assert.True(roundsWithMelds > 0);
    }

    [Fact]
    public void Same_seed_produces_the_same_event_log_and_settlement()
    {
        var first = RunRound(20260801);
        var second = RunRound(20260801);

        Assert.Equal(first.CommandCount, second.CommandCount);
        Assert.Equal(first.EventLog, second.EventLog);
        Assert.Equal(first.ScoreChanges, second.ScoreChanges);
    }

    private static SimulationResult RunRound(ulong seed)
    {
        var engine = new StandardMahjongRuleEngine(new SplitMix64Random(seed));
        var ai = new BasicStandardMahjongAi();
        var commandCount = 0;
        var eventLog = new List<string>();

        while (engine.Snapshot.Phase != StandardMahjongPhase.Finished && commandCount < 500)
        {
            var seat = engine.Snapshot.Phase == StandardMahjongPhase.AwaitingReaction
                ? engine.Snapshot.OfferedReactionSeat!.Value
                : engine.Snapshot.Table.CurrentSeat;
            var command = ai.ChooseCommand(engine.Snapshot, seat, engine.GetLegalActions(seat));
            var result = engine.Dispatch(command);

            Assert.True(result.Accepted, result.Error);
            commandCount++;
            eventLog.AddRange(result.Events.Select(gameEvent => gameEvent switch
            {
                StandardTileDrawnEvent draw => $"draw:{draw.Seat}:{draw.Tile.Kind}:{draw.Tile.CopyIndex}:{draw.IsReplacement}",
                StandardTileDiscardedEvent discard => $"discard:{discard.Seat}:{discard.RiverTile.Tile.Kind}:{discard.RiverTile.Tile.CopyIndex}",
                StandardMeldDeclaredEvent meld => $"meld:{meld.Seat}:{meld.Meld.Type}:{meld.Meld.Tiles[0].Kind}",
                StandardReactionPassedEvent pass => $"pass:{pass.Seat}",
                StandardMahjongFinishedEvent finish => $"finish:{finish.Settlement.IsDraw}:{finish.Settlement.Winner}",
                _ => gameEvent.GetType().Name,
            }));
        }

        Assert.Equal(StandardMahjongPhase.Finished, engine.Snapshot.Phase);
        var settlement = engine.Snapshot.Settlement!;
        return new SimulationResult(commandCount, eventLog, settlement.ScoreChanges, settlement.IsDraw);
    }

    private sealed record SimulationResult(
        int CommandCount,
        IReadOnlyList<string> EventLog,
        IReadOnlyList<long> ScoreChanges,
        bool IsDraw);
}
