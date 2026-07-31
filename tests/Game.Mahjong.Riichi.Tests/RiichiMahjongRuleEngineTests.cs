using Game.Core.Random;
using Game.Mahjong.Commands;
using Game.Mahjong.Riichi.Actions;
using Game.Mahjong.Riichi.Commands;
using Game.Mahjong.Riichi.Events;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Tests;

public sealed class RiichiMahjongRuleEngineTests
{
    [Fact]
    public void Match_starts_at_east_one_with_dead_wall_dora_and_dealer_draw()
    {
        var engine = new RiichiMahjongRuleEngine(new SplitMix64Random(1));
        var snapshot = engine.Snapshot;

        Assert.Equal(RiichiMahjongPhase.AwaitingDiscard, snapshot.Phase);
        Assert.Equal(RiichiRoundWind.East, snapshot.RoundWind);
        Assert.Equal(1, snapshot.HandNumber);
        Assert.Equal(MahjongSeat.East, snapshot.Dealer);
        Assert.Equal(14, snapshot.Table.Hands[(int)snapshot.Dealer].Count);
        Assert.All(snapshot.Table.Hands.Skip(1), hand => Assert.Equal(13, hand.Count));
        Assert.Equal(69, snapshot.Table.LiveTilesRemaining);
        Assert.Equal(4, snapshot.Table.ReplacementTilesRemaining);
        Assert.Single(snapshot.DoraIndicators);
        Assert.Equal([25000, 25000, 25000, 25000], snapshot.Scores);
    }

    [Fact]
    public void Only_current_player_receives_turn_actions_and_wrong_seat_is_rejected()
    {
        var engine = new RiichiMahjongRuleEngine(new SplitMix64Random(2));
        var current = engine.Snapshot.Table.CurrentSeat;
        var wrong = current.Next();
        var tile = engine.Snapshot.Table.Hands[(int)current][0];

        Assert.NotEmpty(engine.GetLegalActions(current));
        Assert.Empty(engine.GetLegalActions(wrong));
        var result = engine.Dispatch(new DiscardMahjongTileCommand((int)wrong, tile));

        Assert.False(result.Accepted);
        Assert.Equal(current, result.Snapshot.Table.CurrentSeat);
    }

    [Fact]
    public void Offered_reactions_contain_one_priority_class_and_pass()
    {
        for (ulong seed = 1; seed <= 100; seed++)
        {
            var engine = new RiichiMahjongRuleEngine(new SplitMix64Random(seed));
            for (var turn = 0; turn < 60 && engine.Snapshot.Phase != RiichiMahjongPhase.Finished; turn++)
            {
                if (engine.Snapshot.Phase == RiichiMahjongPhase.AwaitingReaction)
                {
                    var seat = engine.Snapshot.OfferedReactionSeat!.Value;
                    var actions = engine.GetLegalActions(seat);
                    var classes = actions
                        .Where(action => action.Kind != RiichiMahjongActionKind.Pass)
                        .Select(PriorityClass)
                        .Distinct()
                        .ToArray();
                    Assert.Single(classes);
                    Assert.Contains(actions, action => action.Kind == RiichiMahjongActionKind.Pass);
                    return;
                }

                var current = engine.Snapshot.Table.CurrentSeat;
                var discard = engine.GetLegalActions(current)
                    .First(action => action.Kind == RiichiMahjongActionKind.Discard);
                var result = engine.Dispatch(new DiscardMahjongTileCommand((int)current, discard.Tile!.Value));
                Assert.True(result.Accepted, result.Error);
            }
        }

        Assert.Fail("Expected a deterministic seed to offer a reaction.");
    }

    [Fact]
    public void Riichi_declaration_tile_ron_does_not_establish_or_charge_riichi()
    {
        var engine = CreateRiggedEngine(
            new Dictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>>
            {
                [MahjongSeat.East] = Kinds(
                    MahjongTileKind.Characters1,
                    MahjongTileKind.Characters2,
                    MahjongTileKind.Characters3,
                    MahjongTileKind.Characters4,
                    MahjongTileKind.Characters5,
                    MahjongTileKind.Characters6,
                    MahjongTileKind.Dots1,
                    MahjongTileKind.Dots2,
                    MahjongTileKind.Dots3,
                    MahjongTileKind.Dots4,
                    MahjongTileKind.Dots5,
                    MahjongTileKind.Dots6,
                    MahjongTileKind.Bamboo5,
                    MahjongTileKind.Red),
                [MahjongSeat.South] = Kinds(
                    MahjongTileKind.Bamboo1,
                    MahjongTileKind.Bamboo2,
                    MahjongTileKind.Bamboo3,
                    MahjongTileKind.Bamboo4,
                    MahjongTileKind.Bamboo5,
                    MahjongTileKind.Bamboo6,
                    MahjongTileKind.Dots7,
                    MahjongTileKind.Dots8,
                    MahjongTileKind.Dots9,
                    MahjongTileKind.White,
                    MahjongTileKind.White,
                    MahjongTileKind.White,
                    MahjongTileKind.Red),
            },
            fillerForbiddenKinds: [MahjongTileKind.Red]);
        var red = engine.Snapshot.Table.Hands[(int)MahjongSeat.East]
            .Single(tile => tile.Kind == MahjongTileKind.Red);
        Assert.Contains(
            engine.GetLegalActions(MahjongSeat.East),
            action => action.Kind == RiichiMahjongActionKind.RiichiDiscard && action.Tile == red);

        var declaration = engine.Dispatch(new DeclareRiichiCommand((int)MahjongSeat.East, red));

        Assert.True(declaration.Accepted, declaration.Error);
        Assert.Equal(MahjongSeat.East, declaration.Snapshot.PendingRiichiSeat);
        Assert.Equal(25000, declaration.Snapshot.Scores[(int)MahjongSeat.East]);
        Assert.Equal(0, declaration.Snapshot.RiichiSticks);
        Assert.Contains(
            engine.GetLegalActions(MahjongSeat.South),
            action => action.Kind == RiichiMahjongActionKind.DiscardWin);

        var win = engine.Dispatch(new DeclareMahjongWinCommand((int)MahjongSeat.South));

        Assert.True(win.Accepted, win.Error);
        Assert.Equal(RiichiHandEndReason.Ron, win.Snapshot.LastHandResult!.Reason);
        Assert.DoesNotContain(win.Events, gameEvent => gameEvent is RiichiDeclaredEvent);
        Assert.Equal(0, win.Snapshot.RiichiSticks);
        Assert.Equal(
            win.Snapshot.LastHandResult.Wins[0].ScoreChanges[(int)MahjongSeat.East],
            win.Snapshot.LastHandResult.ScoreChanges[(int)MahjongSeat.East]);
    }

    [Fact]
    public void Passing_ron_causes_temporary_furiten_until_own_draw()
    {
        var engine = CreateRiggedEngine(
            new Dictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>>
            {
                [MahjongSeat.East] = [MahjongTileKind.Red],
                [MahjongSeat.West] = Kinds(
                    MahjongTileKind.Characters1,
                    MahjongTileKind.Characters2,
                    MahjongTileKind.Characters3,
                    MahjongTileKind.Characters4,
                    MahjongTileKind.Characters5,
                    MahjongTileKind.Characters6,
                    MahjongTileKind.White,
                    MahjongTileKind.White,
                    MahjongTileKind.White,
                    MahjongTileKind.Red,
                    MahjongTileKind.Red,
                    MahjongTileKind.Green,
                    MahjongTileKind.Green),
            },
            futureDrawKinds: [MahjongTileKind.Green],
            fillerForbiddenKinds: [MahjongTileKind.Red, MahjongTileKind.Green]);
        var red = engine.Snapshot.Table.Hands[(int)MahjongSeat.East]
            .Single(tile => tile.Kind == MahjongTileKind.Red);

        Assert.True(engine.Dispatch(new DiscardMahjongTileCommand((int)MahjongSeat.East, red)).Accepted);
        Assert.Equal(MahjongSeat.West, engine.Snapshot.OfferedReactionSeat);
        Assert.Contains(
            engine.GetLegalActions(MahjongSeat.West),
            action => action.Kind == RiichiMahjongActionKind.DiscardWin);
        Assert.True(engine.Dispatch(new PassMahjongCommand((int)MahjongSeat.West)).Accepted);

        Assert.Equal(MahjongSeat.South, engine.Snapshot.Table.CurrentSeat);
        var green = engine.Snapshot.Table.Hands[(int)MahjongSeat.South]
            .Single(tile => tile.Kind == MahjongTileKind.Green);
        Assert.True(engine.Dispatch(new DiscardMahjongTileCommand((int)MahjongSeat.South, green)).Accepted);

        Assert.Equal(MahjongSeat.West, engine.Snapshot.OfferedReactionSeat);
        Assert.True(engine.Snapshot.FuritenSeats[(int)MahjongSeat.West]);
        Assert.DoesNotContain(
            engine.GetLegalActions(MahjongSeat.West),
            action => action.Kind == RiichiMahjongActionKind.DiscardWin);
        Assert.Contains(
            engine.GetLegalActions(MahjongSeat.West),
            action => action.Kind == RiichiMahjongActionKind.Pong);
    }

    [Fact]
    public void One_discard_can_settle_multiple_ron_winners_once_each()
    {
        var engine = CreateRiggedEngine(
            new Dictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>>
            {
                [MahjongSeat.East] = [MahjongTileKind.Red],
                [MahjongSeat.South] = Kinds(
                    MahjongTileKind.Bamboo1,
                    MahjongTileKind.Bamboo2,
                    MahjongTileKind.Bamboo3,
                    MahjongTileKind.Bamboo4,
                    MahjongTileKind.Bamboo5,
                    MahjongTileKind.Bamboo6,
                    MahjongTileKind.Dots7,
                    MahjongTileKind.Dots8,
                    MahjongTileKind.Dots9,
                    MahjongTileKind.White,
                    MahjongTileKind.White,
                    MahjongTileKind.White,
                    MahjongTileKind.Red),
                [MahjongSeat.West] = Kinds(
                    MahjongTileKind.Characters1,
                    MahjongTileKind.Characters2,
                    MahjongTileKind.Characters3,
                    MahjongTileKind.Characters4,
                    MahjongTileKind.Characters5,
                    MahjongTileKind.Characters6,
                    MahjongTileKind.Dots4,
                    MahjongTileKind.Dots5,
                    MahjongTileKind.Dots6,
                    MahjongTileKind.Green,
                    MahjongTileKind.Green,
                    MahjongTileKind.Green,
                    MahjongTileKind.Red),
            },
            fillerForbiddenKinds: [MahjongTileKind.Red]);
        var red = engine.Snapshot.Table.Hands[(int)MahjongSeat.East]
            .Single(tile => tile.Kind == MahjongTileKind.Red);

        Assert.True(engine.Dispatch(new DiscardMahjongTileCommand((int)MahjongSeat.East, red)).Accepted);
        var first = engine.Dispatch(new DeclareMahjongWinCommand((int)MahjongSeat.South));

        Assert.True(first.Accepted, first.Error);
        Assert.Equal(RiichiMahjongPhase.AwaitingReaction, first.Snapshot.Phase);
        Assert.Equal(MahjongSeat.West, first.Snapshot.OfferedReactionSeat);
        var second = engine.Dispatch(new DeclareMahjongWinCommand((int)MahjongSeat.West));

        Assert.True(second.Accepted, second.Error);
        Assert.Equal(RiichiHandEndReason.Ron, second.Snapshot.LastHandResult!.Reason);
        Assert.Equal(2, second.Snapshot.LastHandResult.Wins.Count);
        Assert.Equal(
            [MahjongSeat.South, MahjongSeat.West],
            second.Snapshot.LastHandResult.Wins.Select(win => win.Winner));
        Assert.Equal(2, first.Events.Concat(second.Events).Count(gameEvent => gameEvent is RiichiWinSettledEvent));
    }

    [Fact]
    public void Nine_terminals_abort_repeats_dealer_and_increments_honba()
    {
        var engine = CreateRiggedEngine(
            new Dictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>>
            {
                [MahjongSeat.East] = Kinds(
                    MahjongTileKind.Characters1,
                    MahjongTileKind.Characters9,
                    MahjongTileKind.Dots1,
                    MahjongTileKind.Dots9,
                    MahjongTileKind.Bamboo1,
                    MahjongTileKind.Bamboo9,
                    MahjongTileKind.East,
                    MahjongTileKind.South,
                    MahjongTileKind.White),
            });

        Assert.Contains(
            engine.GetLegalActions(MahjongSeat.East),
            action => action.Kind == RiichiMahjongActionKind.NineTerminalsDraw);
        var result = engine.Dispatch(new DeclareNineTerminalsDrawCommand((int)MahjongSeat.East));

        Assert.True(result.Accepted, result.Error);
        Assert.Equal(RiichiHandEndReason.NineTerminals, result.Snapshot.LastHandResult!.Reason);
        Assert.True(result.Snapshot.LastHandResult.DealerRepeats);
        Assert.Equal(MahjongSeat.East, result.Snapshot.Dealer);
        Assert.Equal(1, result.Snapshot.HandNumber);
        Assert.Equal(1, result.Snapshot.Honba);
    }

    private static int PriorityClass(RiichiMahjongAction action)
    {
        return action.Kind switch
        {
            RiichiMahjongActionKind.DiscardWin => 3,
            RiichiMahjongActionKind.Pong or RiichiMahjongActionKind.OpenKong => 2,
            RiichiMahjongActionKind.Chow => 1,
            _ => 0,
        };
    }

    private static RiichiMahjongRuleEngine CreateRiggedEngine(
        IReadOnlyDictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>> desiredHands,
        IReadOnlyList<MahjongTileKind>? futureDrawKinds = null,
        IReadOnlyCollection<MahjongTileKind>? fillerForbiddenKinds = null)
    {
        var ordered = MahjongTileSet.CreateOrdered().ToArray();
        var remaining = ordered.ToList();
        var wall = new MahjongTile?[ordered.Length];
        var forbidden = fillerForbiddenKinds?.ToHashSet() ?? [];

        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            var required = seat == MahjongSeat.East ? 14 : 13;
            var requested = desiredHands.GetValueOrDefault(seat) ?? [];
            Assert.True(requested.Count <= required);
            for (var index = 0; index < requested.Count; index++)
            {
                var wallIndex = index == 13
                    ? 52
                    : (index * 4) + (int)seat;
                wall[wallIndex] = TakeTile(remaining, requested[index]);
            }
        }

        var drawIndex = 53;
        foreach (var kind in futureDrawKinds ?? [])
        {
            wall[drawIndex++] = TakeTile(remaining, kind);
        }

        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            var required = seat == MahjongSeat.East ? 14 : 13;
            for (var index = 0; index < required; index++)
            {
                var wallIndex = index == 13
                    ? 52
                    : (index * 4) + (int)seat;
                if (wall[wallIndex] is not null)
                {
                    continue;
                }

                var tile = remaining.First(candidate => !forbidden.Contains(candidate.Kind));
                wall[wallIndex] = tile;
                remaining.Remove(tile);
            }
        }

        for (var index = 0; index < wall.Length; index++)
        {
            wall[index] ??= remaining[0];
            remaining.Remove(wall[index]!.Value);
        }

        var target = wall.Select(tile => tile!.Value).ToArray();
        var current = ordered.ToArray();
        var choices = new List<int>();
        for (var index = current.Length - 1; index > 0; index--)
        {
            var otherIndex = Array.FindIndex(current, 0, index + 1, tile => tile == target[index]);
            Assert.InRange(otherIndex, 0, index);
            choices.Add(otherIndex);
            (current[index], current[otherIndex]) = (current[otherIndex], current[index]);
        }

        Assert.Equal(target, current);
        return new RiichiMahjongRuleEngine(new ScriptedRandom(choices));
    }

    private static MahjongTile TakeTile(ICollection<MahjongTile> remaining, MahjongTileKind kind)
    {
        var tile = remaining.First(tile => tile.Kind == kind);
        remaining.Remove(tile);
        return tile;
    }

    private static IReadOnlyList<MahjongTileKind> Kinds(params MahjongTileKind[] kinds)
    {
        return kinds;
    }

    private sealed class ScriptedRandom : IDeterministicRandom
    {
        private readonly Queue<int> _choices;

        public ScriptedRandom(IEnumerable<int> choices)
        {
            _choices = new Queue<int>(choices);
        }

        public ulong NextUInt64()
        {
            return 0;
        }

        public int NextInt(int exclusiveMax)
        {
            return _choices.Count > 0 ? _choices.Dequeue() : 0;
        }
    }
}
