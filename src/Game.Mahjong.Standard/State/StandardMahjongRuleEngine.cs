using Game.Core.Random;
using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Standard.Actions;
using Game.Mahjong.Standard.Events;
using Game.Mahjong.Standard.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Standard.State;

public sealed class StandardMahjongRuleEngine
{
    private readonly int _baseScore;
    private readonly HashSet<MahjongSeat> _declinedReactionSeats = [];
    private readonly MahjongTableState _table;
    private IReadOnlyList<StandardMahjongAction> _offeredActions = [];
    private MahjongSeat? _offeredReactionSeat;
    private StandardMahjongPhase _phase = StandardMahjongPhase.AwaitingDiscard;
    private long _sequence;
    private StandardMahjongSettlement? _settlement;

    public StandardMahjongRuleEngine(
        IDeterministicRandom random,
        MahjongSeat dealer = MahjongSeat.East,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);
        _baseScore = baseScore;
        _table = new MahjongTableState(random, dealer);
    }

    public StandardMahjongSnapshot Snapshot => CreateSnapshot();

    public IReadOnlyList<StandardMahjongAction> GetLegalActions(MahjongSeat seat)
    {
        if (_phase == StandardMahjongPhase.AwaitingDiscard && seat == _table.CurrentSeat)
        {
            return CreateTurnActions(seat);
        }

        if (_phase == StandardMahjongPhase.AwaitingReaction && seat == _offeredReactionSeat)
        {
            return _offeredActions;
        }

        return [];
    }

    public StandardMahjongCommandResult Dispatch(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.PlayerIndex is < 0 or >= 4)
        {
            return Reject("玩家座位无效。");
        }

        var seat = (MahjongSeat)command.PlayerIndex;
        return _phase switch
        {
            StandardMahjongPhase.AwaitingDiscard => ApplyTurnCommand(seat, command),
            StandardMahjongPhase.AwaitingReaction => ApplyReactionCommand(seat, command),
            _ => Reject("本局已经结束。"),
        };
    }

    private StandardMahjongCommandResult ApplyTurnCommand(MahjongSeat seat, IGameCommand command)
    {
        if (seat != _table.CurrentSeat)
        {
            return Reject("尚未轮到该玩家。");
        }

        var actions = CreateTurnActions(seat);
        var events = new List<IGameEvent>();
        switch (command)
        {
            case DiscardMahjongTileCommand discard
                when actions.Any(action => action.Kind == StandardMahjongActionKind.Discard && action.Tile == discard.Tile):
                {
                    var riverTile = _table.Discard(seat, discard.Tile);
                    events.Add(new StandardTileDiscardedEvent(NextSequence(), seat, riverTile));
                    BeginReactions(events);
                    return Accept(events);
                }

            case DeclareMahjongWinCommand
                when actions.Any(action => action.Kind == StandardMahjongActionKind.SelfDrawWin):
                FinishWin(seat, null, events);
                return Accept(events);

            case DeclareConcealedKongCommand concealedKong:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == StandardMahjongActionKind.ConcealedKong
                        && SameTiles(candidate.ConcealedTiles, concealedKong.Tiles));
                    if (action is null)
                    {
                        break;
                    }

                    var meld = _table.DeclareConcealedKong(seat, action.ConcealedTiles);
                    events.Add(new StandardMeldDeclaredEvent(NextSequence(), seat, meld));
                    DrawReplacement(events);
                    return Accept(events);
                }

            case DeclareAddedKongCommand addedKong:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == StandardMahjongActionKind.AddedKong
                        && candidate.Tile == addedKong.FourthTile);
                    if (action is null)
                    {
                        break;
                    }

                    var meld = _table.DeclareAddedKong(seat, addedKong.FourthTile);
                    events.Add(new StandardMeldDeclaredEvent(NextSequence(), seat, meld));
                    DrawReplacement(events);
                    return Accept(events);
                }
        }

        return Reject("命令不在当前合法操作中。");
    }

    private StandardMahjongCommandResult ApplyReactionCommand(MahjongSeat seat, IGameCommand command)
    {
        if (seat != _offeredReactionSeat)
        {
            return Reject("当前未询问该玩家的鸣牌反应。");
        }

        var events = new List<IGameEvent>();
        if (command is PassMahjongCommand
            && _offeredActions.Any(action => action.Kind == StandardMahjongActionKind.Pass))
        {
            _declinedReactionSeats.Add(seat);
            events.Add(new StandardReactionPassedEvent(NextSequence(), seat));
            OfferNextReactionOrDraw(events);
            return Accept(events);
        }

        if (command is DeclareMahjongWinCommand
            && _offeredActions.Any(action => action.Kind == StandardMahjongActionKind.DiscardWin))
        {
            FinishWin(seat, _table.LastDiscardSeat, events);
            return Accept(events);
        }

        if (command is ClaimMahjongDiscardCommand claim)
        {
            var action = _offeredActions.FirstOrDefault(candidate =>
                candidate.MeldType == claim.MeldType
                && SameTiles(candidate.ConcealedTiles, claim.ConcealedTiles));
            if (action is not null)
            {
                var meld = _table.ClaimDiscard(seat, claim.MeldType, action.ConcealedTiles);
                events.Add(new StandardMeldDeclaredEvent(NextSequence(), seat, meld));
                ClearReactionState();
                _phase = StandardMahjongPhase.AwaitingDiscard;
                if (claim.MeldType == MahjongMeldType.OpenKong)
                {
                    DrawReplacement(events);
                }

                return Accept(events);
            }
        }

        return Reject("命令不在当前合法反应中。");
    }

    private IReadOnlyList<StandardMahjongAction> CreateTurnActions(MahjongSeat seat)
    {
        var hand = _table.GetConcealedTiles(seat);
        var melds = _table.GetMelds(seat);
        var actions = hand
            .Select(tile => new StandardMahjongAction(StandardMahjongActionKind.Discard, tile))
            .ToList();

        if (StandardMahjongScorer.CanWin(hand.Select(tile => tile.Kind), melds.Count))
        {
            actions.Add(new StandardMahjongAction(StandardMahjongActionKind.SelfDrawWin));
        }

        if (_table.Wall.ReplacementTilesRemaining > 0 && _table.Wall.LiveTilesRemaining > 0)
        {
            foreach (var group in hand.GroupBy(tile => tile.Kind).Where(group => group.Count() == 4))
            {
                actions.Add(new StandardMahjongAction(
                    StandardMahjongActionKind.ConcealedKong,
                    meldType: MahjongMeldType.ConcealedKong,
                    concealedTiles: group));
            }

            foreach (var pong in melds.Where(meld => meld.Type == MahjongMeldType.Pong))
            {
                foreach (var tile in hand.Where(tile => tile.Kind == pong.Tiles[0].Kind))
                {
                    actions.Add(new StandardMahjongAction(
                        StandardMahjongActionKind.AddedKong,
                        tile,
                        MahjongMeldType.AddedKong));
                }
            }
        }

        return actions;
    }

    private void BeginReactions(ICollection<IGameEvent> events)
    {
        _phase = StandardMahjongPhase.AwaitingReaction;
        _declinedReactionSeats.Clear();
        OfferNextReactionOrDraw(events);
    }

    private void OfferNextReactionOrDraw(ICollection<IGameEvent> events)
    {
        var discardSeat = _table.LastDiscardSeat
            ?? throw new InvalidOperationException("Reaction state requires a discard.");
        var candidates = Enum.GetValues<MahjongSeat>()
            .Where(seat => seat != discardSeat && !_declinedReactionSeats.Contains(seat))
            .Select(seat => new ReactionCandidate(seat, CreateReactionActions(seat)))
            .ToArray();

        var winningCandidate = candidates
            .Where(candidate => candidate.Actions.Any(action => action.Kind == StandardMahjongActionKind.DiscardWin))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(discardSeat))
            .FirstOrDefault();
        if (winningCandidate is not null)
        {
            Offer(winningCandidate.Seat, winningCandidate.Actions
                .Where(action => action.Kind == StandardMahjongActionKind.DiscardWin));
            return;
        }

        var pongCandidate = candidates
            .Where(candidate => candidate.Actions.Any(action =>
                action.Kind is StandardMahjongActionKind.Pong or StandardMahjongActionKind.OpenKong))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(discardSeat))
            .FirstOrDefault();
        if (pongCandidate is not null)
        {
            Offer(pongCandidate.Seat, pongCandidate.Actions.Where(action =>
                action.Kind is StandardMahjongActionKind.Pong or StandardMahjongActionKind.OpenKong));
            return;
        }

        var chowCandidate = candidates.SingleOrDefault(candidate =>
            candidate.Seat == discardSeat.Next()
            && candidate.Actions.Any(action => action.Kind == StandardMahjongActionKind.Chow));
        if (chowCandidate is not null)
        {
            Offer(chowCandidate.Seat, chowCandidate.Actions
                .Where(action => action.Kind == StandardMahjongActionKind.Chow));
            return;
        }

        ClearReactionState();
        if (_table.Wall.LiveTilesRemaining == 0)
        {
            FinishDraw(events);
            return;
        }

        var tile = _table.DrawCurrent();
        events.Add(new StandardTileDrawnEvent(NextSequence(), _table.CurrentSeat, tile, false));
        _phase = StandardMahjongPhase.AwaitingDiscard;
    }

    private IReadOnlyList<StandardMahjongAction> CreateReactionActions(MahjongSeat seat)
    {
        var discard = _table.LastDiscard ?? throw new InvalidOperationException("There is no discard to react to.");
        var sourceSeat = _table.LastDiscardSeat!.Value;
        var hand = _table.GetConcealedTiles(seat);
        var matchingTiles = hand.Where(tile => tile.Kind == discard.Tile.Kind).ToArray();
        var actions = new List<StandardMahjongAction>();

        if (StandardMahjongScorer.CanWin(
            hand.Select(tile => tile.Kind).Append(discard.Tile.Kind),
            _table.GetMelds(seat).Count))
        {
            actions.Add(new StandardMahjongAction(StandardMahjongActionKind.DiscardWin, discard.Tile));
        }

        foreach (var pair in Choose(matchingTiles, 2))
        {
            actions.Add(new StandardMahjongAction(
                StandardMahjongActionKind.Pong,
                discard.Tile,
                MahjongMeldType.Pong,
                pair));
        }

        if (_table.Wall.ReplacementTilesRemaining > 0 && _table.Wall.LiveTilesRemaining > 0)
        {
            foreach (var triple in Choose(matchingTiles, 3))
            {
                actions.Add(new StandardMahjongAction(
                    StandardMahjongActionKind.OpenKong,
                    discard.Tile,
                    MahjongMeldType.OpenKong,
                    triple));
            }
        }

        if (seat == sourceSeat.Next() && discard.Tile.Kind.IsSuited())
        {
            actions.AddRange(CreateChowActions(hand, discard.Tile));
        }

        return actions;
    }

    private static IEnumerable<StandardMahjongAction> CreateChowActions(
        IReadOnlyList<MahjongTile> hand,
        MahjongTile discard)
    {
        var number = discard.Kind.GetNumber();
        var suit = discard.Kind.GetSuit();
        for (var start = Math.Max(1, number - 2); start <= Math.Min(7, number); start++)
        {
            var neededKinds = Enumerable.Range(start, 3)
                .Select(tileNumber => MahjongTileKinds.FromSuitAndNumber(suit, tileNumber))
                .Where(kind => kind != discard.Kind)
                .ToArray();
            var firstChoices = hand.Where(tile => tile.Kind == neededKinds[0]).ToArray();
            var secondChoices = hand.Where(tile => tile.Kind == neededKinds[1]).ToArray();
            foreach (var first in firstChoices)
            {
                foreach (var second in secondChoices)
                {
                    yield return new StandardMahjongAction(
                        StandardMahjongActionKind.Chow,
                        discard,
                        MahjongMeldType.Chow,
                        [first, second]);
                }
            }
        }
    }

    private void Offer(MahjongSeat seat, IEnumerable<StandardMahjongAction> actions)
    {
        _offeredReactionSeat = seat;
        _offeredActions = actions
            .Append(new StandardMahjongAction(StandardMahjongActionKind.Pass))
            .ToArray();
        _phase = StandardMahjongPhase.AwaitingReaction;
    }

    private void DrawReplacement(ICollection<IGameEvent> events)
    {
        var tile = _table.DrawCurrent(replacement: true);
        events.Add(new StandardTileDrawnEvent(NextSequence(), _table.CurrentSeat, tile, true));
        _phase = StandardMahjongPhase.AwaitingDiscard;
    }

    private void FinishWin(
        MahjongSeat winner,
        MahjongSeat? discardSource,
        ICollection<IGameEvent> events)
    {
        var winningKinds = _table.GetConcealedTiles(winner).Select(tile => tile.Kind);
        if (discardSource is not null)
        {
            winningKinds = winningKinds.Append(_table.LastDiscard!.Tile.Kind);
        }

        _settlement = StandardMahjongScorer.Calculate(
            winningKinds,
            _table.GetMelds(winner),
            winner,
            discardSource,
            _baseScore);
        _phase = StandardMahjongPhase.Finished;
        ClearReactionState();
        events.Add(new StandardMahjongFinishedEvent(NextSequence(), _settlement));
    }

    private void FinishDraw(ICollection<IGameEvent> events)
    {
        _settlement = StandardMahjongSettlement.Draw();
        _phase = StandardMahjongPhase.Finished;
        events.Add(new StandardMahjongFinishedEvent(NextSequence(), _settlement));
    }

    private void ClearReactionState()
    {
        _offeredReactionSeat = null;
        _offeredActions = [];
        _declinedReactionSeats.Clear();
    }

    private StandardMahjongSnapshot CreateSnapshot()
    {
        return new StandardMahjongSnapshot(_phase, _table.Snapshot, _offeredReactionSeat, _settlement);
    }

    private StandardMahjongCommandResult Accept(IReadOnlyList<IGameEvent> events)
    {
        return new StandardMahjongCommandResult(true, CreateSnapshot(), events);
    }

    private StandardMahjongCommandResult Reject(string error)
    {
        return new StandardMahjongCommandResult(false, CreateSnapshot(), [], error);
    }

    private long NextSequence()
    {
        return ++_sequence;
    }

    private static bool SameTiles(IReadOnlyCollection<MahjongTile> first, IReadOnlyCollection<MahjongTile> second)
    {
        return first.Count == second.Count && first.All(second.Contains);
    }

    private static IEnumerable<IReadOnlyList<MahjongTile>> Choose(
        IReadOnlyList<MahjongTile> tiles,
        int count)
    {
        if (tiles.Count < count)
        {
            yield break;
        }

        var choice = new MahjongTile[count];
        foreach (var result in Choose(tiles, count, 0, 0, choice))
        {
            yield return result;
        }
    }

    private static IEnumerable<IReadOnlyList<MahjongTile>> Choose(
        IReadOnlyList<MahjongTile> tiles,
        int count,
        int sourceIndex,
        int choiceIndex,
        MahjongTile[] choice)
    {
        if (choiceIndex == count)
        {
            yield return (MahjongTile[])choice.Clone();
            yield break;
        }

        for (var index = sourceIndex; index <= tiles.Count - (count - choiceIndex); index++)
        {
            choice[choiceIndex] = tiles[index];
            foreach (var result in Choose(tiles, count, index + 1, choiceIndex + 1, choice))
            {
                yield return result;
            }
        }
    }

    private sealed record ReactionCandidate(
        MahjongSeat Seat,
        IReadOnlyList<StandardMahjongAction> Actions);
}
