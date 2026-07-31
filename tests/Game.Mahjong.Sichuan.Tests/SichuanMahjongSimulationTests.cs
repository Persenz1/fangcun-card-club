using Game.Core.Random;
using Game.Mahjong.Sichuan.AI;
using Game.Mahjong.Sichuan.Events;
using Game.Mahjong.Sichuan.State;
using Game.Mahjong.Table;

namespace Game.Mahjong.Sichuan.Tests;

public sealed class SichuanMahjongSimulationTests
{
    [Fact]
    public void Basic_ai_completes_deterministic_blood_battle_rounds()
    {
        var roundsWithWins = 0;
        var roundsWithMelds = 0;
        var roundsWithMultiRon = 0;
        for (ulong seed = 1; seed <= 256; seed++)
        {
            var result = RunRound(seed);

            Assert.InRange(result.CommandCount, 1, 600);
            Assert.Equal(0, result.ScoreChanges.Sum());
            roundsWithWins += result.WinCount > 0 ? 1 : 0;
            roundsWithMelds += result.EventLog.Any(entry =>
                entry.StartsWith("meld:", StringComparison.Ordinal)) ? 1 : 0;
            roundsWithMultiRon += result.MaxRonWinnersForOneDiscard > 1 ? 1 : 0;
        }

        Assert.True(roundsWithWins > 0);
        Assert.True(roundsWithMelds > 0);
        Assert.True(roundsWithMultiRon > 0);
    }

    [Fact]
    public void Same_seed_produces_the_same_exchange_play_and_settlement()
    {
        var first = RunRound(20260801);
        var second = RunRound(20260801);

        Assert.Equal(first.CommandCount, second.CommandCount);
        Assert.Equal(first.EventLog, second.EventLog);
        Assert.Equal(first.ScoreChanges, second.ScoreChanges);
    }

    private static SimulationResult RunRound(ulong seed)
    {
        var engine = new SichuanMahjongRuleEngine(new SplitMix64Random(seed));
        var ai = new BasicSichuanMahjongAi();
        var commandCount = 0;
        var eventLog = new List<string>();
        var ronWinnersForDiscard = 0;
        var maxRonWinnersForOneDiscard = 0;

        while (engine.Snapshot.Phase != SichuanMahjongPhase.Finished && commandCount < 600)
        {
            var seat = Actor(engine);
            var legalActions = engine.GetLegalActions(seat);
            Assert.NotEmpty(legalActions);
            var command = ai.ChooseCommand(engine.Snapshot, seat, legalActions);
            var result = engine.Dispatch(command);

            Assert.True(result.Accepted, result.Error);
            commandCount++;
            foreach (var gameEvent in result.Events)
            {
                if (gameEvent is SichuanTileDiscardedEvent)
                {
                    ronWinnersForDiscard = 0;
                }
                else if (gameEvent is SichuanWinSettledEvent { Win.DiscardSource: not null })
                {
                    ronWinnersForDiscard++;
                    maxRonWinnersForOneDiscard = Math.Max(
                        maxRonWinnersForOneDiscard,
                        ronWinnersForDiscard);
                }
                else if (gameEvent is SichuanTileDrawnEvent or SichuanMeldDeclaredEvent)
                {
                    ronWinnersForDiscard = 0;
                }
            }

            eventLog.AddRange(result.Events.Select(gameEvent => gameEvent switch
            {
                SichuanExchangeSubmittedEvent exchange => $"exchange:{exchange.Seat}",
                SichuanTilesExchangedEvent exchange => $"direction:{exchange.Direction}",
                SichuanVoidSuitDeclaredEvent declared => $"void:{declared.Seat}:{declared.Suit}",
                SichuanTileDrawnEvent draw => $"draw:{draw.Seat}:{draw.Tile.Kind}:{draw.Tile.CopyIndex}:{draw.IsReplacement}",
                SichuanTileDiscardedEvent discard => $"discard:{discard.Seat}:{discard.RiverTile.Tile.Kind}:{discard.RiverTile.Tile.CopyIndex}",
                SichuanMeldDeclaredEvent meld => $"meld:{meld.Seat}:{meld.Meld.Type}:{meld.Meld.Tiles[0].Kind}",
                SichuanReactionPassedEvent pass => $"pass:{pass.Seat}",
                SichuanWinSettledEvent win => $"win:{win.Win.Winner}:{win.Win.DiscardSource}:{win.Win.Fan}",
                SichuanMahjongFinishedEvent finish => $"finish:{finish.Settlement.IsExhaustiveDraw}:{finish.Settlement.Wins.Count}",
                _ => gameEvent.GetType().Name,
            }));
        }

        Assert.Equal(SichuanMahjongPhase.Finished, engine.Snapshot.Phase);
        var settlement = engine.Snapshot.Settlement!;
        return new SimulationResult(
            commandCount,
            eventLog,
            settlement.ScoreChanges,
            settlement.Wins.Count,
            maxRonWinnersForOneDiscard);
    }

    private static MahjongSeat Actor(SichuanMahjongRuleEngine engine)
    {
        if (engine.Snapshot.Phase == SichuanMahjongPhase.AwaitingReaction)
        {
            return engine.Snapshot.OfferedReactionSeat!.Value;
        }

        if (engine.Snapshot.Phase == SichuanMahjongPhase.AwaitingDiscard)
        {
            return engine.Snapshot.Table.CurrentSeat;
        }

        return Enum.GetValues<MahjongSeat>().First(seat => engine.GetLegalActions(seat).Count > 0);
    }

    private sealed record SimulationResult(
        int CommandCount,
        IReadOnlyList<string> EventLog,
        IReadOnlyList<long> ScoreChanges,
        int WinCount,
        int MaxRonWinnersForOneDiscard);
}
