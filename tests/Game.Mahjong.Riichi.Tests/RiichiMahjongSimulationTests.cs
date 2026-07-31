using Game.Core.Random;
using Game.Mahjong.Riichi.AI;
using Game.Mahjong.Riichi.Events;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;

namespace Game.Mahjong.Riichi.Tests;

public sealed class RiichiMahjongSimulationTests
{
    [Fact]
    public void Basic_ai_completes_matches_with_conserved_points_and_only_accepted_commands()
    {
        var matchesWithWins = 0;
        var matchesWithRiichi = 0;
        var matchesWithCalls = 0;
        var matchesWithDraws = 0;
        var matchesWithKongs = 0;
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var result = RunMatch(seed);

            Assert.InRange(result.CommandCount, 1, 5000);
            Assert.Equal(100000, result.FinalScores.Sum());
            matchesWithWins += result.EventLog.Any(entry => entry.StartsWith("win:", StringComparison.Ordinal)) ? 1 : 0;
            matchesWithRiichi += result.EventLog.Any(entry => entry.StartsWith("riichi:", StringComparison.Ordinal)) ? 1 : 0;
            matchesWithCalls += result.EventLog.Any(entry => entry.StartsWith("meld:", StringComparison.Ordinal)) ? 1 : 0;
            matchesWithDraws += result.EventLog.Any(entry => entry.Contains(":ExhaustiveDraw:", StringComparison.Ordinal)) ? 1 : 0;
            matchesWithKongs += result.EventLog.Any(entry => entry.StartsWith("dora:", StringComparison.Ordinal)) ? 1 : 0;
        }

        Assert.True(matchesWithWins > 0);
        Assert.True(matchesWithRiichi > 0);
        Assert.True(matchesWithCalls > 0);
        Assert.True(matchesWithDraws > 0);
        Assert.True(matchesWithKongs > 0);
    }

    [Fact]
    public void Same_seed_produces_identical_hands_events_and_final_scores()
    {
        var first = RunMatch(20260801);
        var second = RunMatch(20260801);

        Assert.Equal(first.CommandCount, second.CommandCount);
        Assert.Equal(first.EventLog, second.EventLog);
        Assert.Equal(first.FinalScores, second.FinalScores);
    }

    private static SimulationResult RunMatch(ulong seed)
    {
        var engine = new RiichiMahjongRuleEngine(new SplitMix64Random(seed));
        var ai = new BasicRiichiMahjongAi();
        var events = new List<string>();
        var commandCount = 0;

        while (engine.Snapshot.Phase != RiichiMahjongPhase.Finished && commandCount < 5000)
        {
            var seat = engine.Snapshot.Phase == RiichiMahjongPhase.AwaitingReaction
                ? engine.Snapshot.OfferedReactionSeat!.Value
                : engine.Snapshot.Table.CurrentSeat;
            var legalActions = engine.GetLegalActions(seat);
            Assert.NotEmpty(legalActions);
            var command = ai.ChooseCommand(engine.Snapshot, seat, legalActions);
            var result = engine.Dispatch(command);

            Assert.True(result.Accepted, result.Error);
            Assert.Equal(100000, result.Snapshot.Scores.Sum() + (result.Snapshot.RiichiSticks * 1000L));
            commandCount++;
            events.AddRange(result.Events.Select(gameEvent => gameEvent switch
            {
                RiichiHandStartedEvent started => $"start:{started.RoundWind}:{started.HandNumber}:{started.Dealer}:{started.Honba}:{started.RiichiSticks}:{started.DoraIndicator}",
                RiichiTileDrawnEvent draw => $"draw:{draw.Seat}:{draw.Tile.Kind}:{draw.Tile.CopyIndex}:{draw.IsReplacement}",
                RiichiTileDiscardedEvent discard => $"discard:{discard.Seat}:{discard.RiverTile.Tile.Kind}:{discard.RiverTile.Tile.CopyIndex}",
                RiichiDeclaredEvent riichi => $"riichi:{riichi.Seat}:{riichi.IsDoubleRiichi}",
                RiichiMeldDeclaredEvent meld => $"meld:{meld.Seat}:{meld.Meld.Type}:{meld.Meld.Tiles[0].Kind}",
                RiichiDoraRevealedEvent dora => $"dora:{dora.Indicator}",
                RiichiReactionPassedEvent pass => $"pass:{pass.Seat}:{pass.PassedWin}",
                RiichiWinSettledEvent win => $"win:{win.Win.Winner}:{win.Win.DiscardSource}:{win.Win.HandScore.Han}:{win.Win.HandScore.Fu}:{win.Win.HandScore.YakumanCount}",
                RiichiHandFinishedEvent finish => $"hand:{finish.Result.Reason}:{finish.Result.DealerRepeats}:{finish.Result.Wins.Count}",
                RiichiMatchFinishedEvent finish => $"match:{string.Join(',', finish.Result.Ranking)}",
                _ => gameEvent.GetType().Name,
            }));
        }

        Assert.True(
            engine.Snapshot.Phase == RiichiMahjongPhase.Finished,
            $"Seed {seed} stopped after {commandCount} commands at {engine.Snapshot.RoundWind}{engine.Snapshot.HandNumber} "
            + $"phase {engine.Snapshot.Phase}, honba {engine.Snapshot.Honba}, actor "
            + $"{engine.Snapshot.OfferedReactionSeat?.ToString() ?? engine.Snapshot.Table.CurrentSeat.ToString()}, "
            + $"actions {string.Join(',', (engine.Snapshot.OfferedReactionSeat is { } offered
                ? engine.GetLegalActions(offered)
                : engine.GetLegalActions(engine.Snapshot.Table.CurrentSeat)).Select(action => action.Kind))}, "
            + $"tail {string.Join('|', events.TakeLast(20))}.");
        var match = engine.Snapshot.MatchResult!;
        Assert.Equal(
            match.FinalScores.OrderByDescending(score => score),
            match.Ranking.Select(seat => match.FinalScores[(int)seat]));
        return new SimulationResult(commandCount, events, match.FinalScores);
    }

    private sealed record SimulationResult(
        int CommandCount,
        IReadOnlyList<string> EventLog,
        IReadOnlyList<long> FinalScores);
}
