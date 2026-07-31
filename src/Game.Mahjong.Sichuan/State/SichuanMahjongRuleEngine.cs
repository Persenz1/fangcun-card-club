using Game.Core.Random;
using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Sichuan.Actions;
using Game.Mahjong.Sichuan.Commands;
using Game.Mahjong.Sichuan.Events;
using Game.Mahjong.Sichuan.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.State;

public sealed class SichuanMahjongRuleEngine
{
    private readonly bool[] _activeSeats = [true, true, true, true];
    private readonly int _baseScore;
    private readonly HashSet<MahjongSeat> _declinedReactionSeats = [];
    private readonly Dictionary<MahjongSeat, IReadOnlyList<MahjongTile>> _exchangeSelections = [];
    private readonly HashSet<MahjongSeat> _ronWinnersForDiscard = [];
    private readonly long[] _scoreChanges = new long[4];
    private readonly MahjongTableState _table;
    private readonly MahjongTileSuit?[] _voidSuits = new MahjongTileSuit?[4];
    private readonly List<SichuanWinResult> _wins = [];
    private IReadOnlyList<SichuanMahjongAction> _offeredActions = [];
    private MahjongSeat? _offeredReactionSeat;
    private SichuanMahjongPhase _phase = SichuanMahjongPhase.ExchangeThree;
    private long _sequence;
    private SichuanMahjongSettlement? _settlement;

    public SichuanMahjongRuleEngine(
        IDeterministicRandom random,
        MahjongSeat dealer = MahjongSeat.East,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);

        _baseScore = baseScore;
        var wall = new MahjongWall(MahjongTileSet.CreateSuitedShuffled(random));
        _table = new MahjongTableState(wall, dealer, drawDealerOpeningTile: false);
        ExchangeDirection = (SichuanExchangeDirection)(random.NextInt(3) + 1);
    }

    public SichuanExchangeDirection ExchangeDirection { get; }

    public SichuanMahjongSnapshot Snapshot => CreateSnapshot();

    public IReadOnlyList<SichuanMahjongAction> GetLegalActions(MahjongSeat seat)
    {
        if (!Enum.IsDefined(seat))
        {
            return [];
        }

        return _phase switch
        {
            SichuanMahjongPhase.ExchangeThree when !_exchangeSelections.ContainsKey(seat) =>
                CreateExchangeActions(seat),
            SichuanMahjongPhase.DeclareVoidSuit when _voidSuits[(int)seat] is null =>
                CreateVoidSuitActions(),
            SichuanMahjongPhase.AwaitingDiscard when seat == _table.CurrentSeat && IsActive(seat) =>
                CreateTurnActions(seat),
            SichuanMahjongPhase.AwaitingReaction when seat == _offeredReactionSeat =>
                _offeredActions,
            _ => [],
        };
    }

    public SichuanMahjongCommandResult Dispatch(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.PlayerIndex is < 0 or >= 4)
        {
            return Reject("玩家座位无效。");
        }

        var seat = (MahjongSeat)command.PlayerIndex;
        return _phase switch
        {
            SichuanMahjongPhase.ExchangeThree => ApplyExchangeCommand(seat, command),
            SichuanMahjongPhase.DeclareVoidSuit => ApplyVoidSuitCommand(seat, command),
            SichuanMahjongPhase.AwaitingDiscard => ApplyTurnCommand(seat, command),
            SichuanMahjongPhase.AwaitingReaction => ApplyReactionCommand(seat, command),
            _ => Reject("本局已经结束。"),
        };
    }

    private SichuanMahjongCommandResult ApplyExchangeCommand(MahjongSeat seat, IGameCommand command)
    {
        if (_exchangeSelections.ContainsKey(seat))
        {
            return Reject("该玩家已经提交换三张。");
        }

        if (command is not ExchangeThreeTilesCommand exchange)
        {
            return Reject("当前需要提交换三张。");
        }

        var action = GetLegalActions(seat).FirstOrDefault(candidate =>
            candidate.Kind == SichuanMahjongActionKind.ExchangeThree
            && SameTiles(candidate.ConcealedTiles, exchange.Tiles));
        if (action is null)
        {
            return Reject("换三张必须是手中的三张同门实体牌。");
        }

        var events = new List<IGameEvent>();
        _exchangeSelections.Add(seat, action.ConcealedTiles);
        events.Add(new SichuanExchangeSubmittedEvent(NextSequence(), seat));
        if (_exchangeSelections.Count == 4)
        {
            _table.ExchangeTiles(_exchangeSelections, (int)ExchangeDirection);
            _phase = SichuanMahjongPhase.DeclareVoidSuit;
            events.Add(new SichuanTilesExchangedEvent(NextSequence(), ExchangeDirection));
        }

        return Accept(events);
    }

    private SichuanMahjongCommandResult ApplyVoidSuitCommand(MahjongSeat seat, IGameCommand command)
    {
        if (_voidSuits[(int)seat] is not null)
        {
            return Reject("该玩家已经定缺。");
        }

        if (command is not DeclareVoidSuitCommand declare
            || declare.Suit == MahjongTileSuit.Honors
            || !Enum.IsDefined(declare.Suit))
        {
            return Reject("定缺必须选择万、筒或条。");
        }

        var events = new List<IGameEvent>();
        _voidSuits[(int)seat] = declare.Suit;
        events.Add(new SichuanVoidSuitDeclaredEvent(NextSequence(), seat, declare.Suit));
        if (_voidSuits.All(suit => suit is not null))
        {
            var tile = _table.DrawCurrent();
            _phase = SichuanMahjongPhase.AwaitingDiscard;
            events.Add(new SichuanTileDrawnEvent(NextSequence(), _table.CurrentSeat, tile, false));
        }

        return Accept(events);
    }

    private SichuanMahjongCommandResult ApplyTurnCommand(MahjongSeat seat, IGameCommand command)
    {
        if (seat != _table.CurrentSeat || !IsActive(seat))
        {
            return Reject("尚未轮到该玩家。");
        }

        var actions = CreateTurnActions(seat);
        var events = new List<IGameEvent>();
        switch (command)
        {
            case DiscardMahjongTileCommand discard
                when actions.Any(action =>
                    action.Kind == SichuanMahjongActionKind.Discard && action.Tile == discard.Tile):
                {
                    var riverTile = _table.Discard(seat, discard.Tile);
                    events.Add(new SichuanTileDiscardedEvent(NextSequence(), seat, riverTile));
                    BeginReactions(events);
                    return Accept(events);
                }

            case DeclareMahjongWinCommand
                when actions.Any(action => action.Kind == SichuanMahjongActionKind.SelfDrawWin):
                SettleWin(seat, null, events);
                _table.EndCurrentTurnWithoutDiscard(seat);
                ContinueAfterWinner(seat, events);
                return Accept(events);

            case DeclareConcealedKongCommand concealedKong:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == SichuanMahjongActionKind.ConcealedKong
                        && SameTiles(candidate.ConcealedTiles, concealedKong.Tiles));
                    if (action is null)
                    {
                        break;
                    }

                    var meld = _table.DeclareConcealedKong(seat, action.ConcealedTiles);
                    var changes = ApplyKongPayment(meld.Type, seat, null);
                    events.Add(new SichuanMeldDeclaredEvent(NextSequence(), seat, meld, changes));
                    DrawReplacement(events);
                    return Accept(events);
                }

            case DeclareAddedKongCommand addedKong:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == SichuanMahjongActionKind.AddedKong
                        && candidate.Tile == addedKong.FourthTile);
                    if (action is null)
                    {
                        break;
                    }

                    var meld = _table.DeclareAddedKong(seat, addedKong.FourthTile);
                    var changes = ApplyKongPayment(meld.Type, seat, null);
                    events.Add(new SichuanMeldDeclaredEvent(NextSequence(), seat, meld, changes));
                    DrawReplacement(events);
                    return Accept(events);
                }
        }

        return Reject("命令不在当前合法操作中。");
    }

    private SichuanMahjongCommandResult ApplyReactionCommand(MahjongSeat seat, IGameCommand command)
    {
        if (seat != _offeredReactionSeat || !IsActive(seat))
        {
            return Reject("当前未询问该玩家的鸣牌反应。");
        }

        var events = new List<IGameEvent>();
        if (command is PassMahjongCommand
            && _offeredActions.Any(action => action.Kind == SichuanMahjongActionKind.Pass))
        {
            _declinedReactionSeats.Add(seat);
            events.Add(new SichuanReactionPassedEvent(NextSequence(), seat));
            OfferNextReactionOrDraw(events);
            return Accept(events);
        }

        if (command is DeclareMahjongWinCommand
            && _offeredActions.Any(action => action.Kind == SichuanMahjongActionKind.DiscardWin))
        {
            var discardSource = _table.LastDiscardSeat!.Value;
            SettleWin(seat, discardSource, events);
            _ronWinnersForDiscard.Add(seat);
            OfferNextReactionOrDraw(events);
            return Accept(events);
        }

        if (command is ClaimMahjongDiscardCommand claim)
        {
            var action = _offeredActions.FirstOrDefault(candidate =>
                candidate.MeldType == claim.MeldType
                && SameTiles(candidate.ConcealedTiles, claim.ConcealedTiles));
            if (action is not null)
            {
                var source = _table.LastDiscardSeat!.Value;
                var meld = _table.ClaimDiscard(seat, claim.MeldType, action.ConcealedTiles);
                var changes = claim.MeldType == MahjongMeldType.OpenKong
                    ? ApplyKongPayment(meld.Type, seat, source)
                    : Array.AsReadOnly(new long[4]);
                events.Add(new SichuanMeldDeclaredEvent(NextSequence(), seat, meld, changes));
                ClearReactionState();
                _phase = SichuanMahjongPhase.AwaitingDiscard;
                if (claim.MeldType == MahjongMeldType.OpenKong)
                {
                    DrawReplacement(events);
                }

                return Accept(events);
            }
        }

        return Reject("命令不在当前合法反应中。");
    }

    private IReadOnlyList<SichuanMahjongAction> CreateExchangeActions(MahjongSeat seat)
    {
        return Enum.GetValues<MahjongTileSuit>()
            .Where(suit => suit != MahjongTileSuit.Honors)
            .SelectMany(suit => Choose(
                _table.GetConcealedTiles(seat).Where(tile => tile.Kind.GetSuit() == suit).ToArray(),
                3))
            .Select(tiles => new SichuanMahjongAction(
                SichuanMahjongActionKind.ExchangeThree,
                concealedTiles: tiles))
            .ToArray();
    }

    private static IReadOnlyList<SichuanMahjongAction> CreateVoidSuitActions()
    {
        return
        [
            new SichuanMahjongAction(SichuanMahjongActionKind.DeclareVoidSuit, suit: MahjongTileSuit.Characters),
            new SichuanMahjongAction(SichuanMahjongActionKind.DeclareVoidSuit, suit: MahjongTileSuit.Dots),
            new SichuanMahjongAction(SichuanMahjongActionKind.DeclareVoidSuit, suit: MahjongTileSuit.Bamboo),
        ];
    }

    private IReadOnlyList<SichuanMahjongAction> CreateTurnActions(MahjongSeat seat)
    {
        var hand = _table.GetConcealedTiles(seat);
        var voidSuit = _voidSuits[(int)seat]!.Value;
        var voidTiles = hand.Where(tile => tile.Kind.GetSuit() == voidSuit).ToArray();
        if (voidTiles.Length > 0)
        {
            return voidTiles
                .Select(tile => new SichuanMahjongAction(SichuanMahjongActionKind.Discard, tile))
                .ToArray();
        }

        var melds = _table.GetMelds(seat);
        var actions = hand
            .Select(tile => new SichuanMahjongAction(SichuanMahjongActionKind.Discard, tile))
            .ToList();

        if (SichuanMahjongScorer.CanWin(hand.Select(tile => tile.Kind), melds.Count, voidSuit))
        {
            actions.Add(new SichuanMahjongAction(SichuanMahjongActionKind.SelfDrawWin));
        }

        if (_table.Wall.ReplacementTilesRemaining > 0 && _table.Wall.LiveTilesRemaining > 0)
        {
            foreach (var group in hand.GroupBy(tile => tile.Kind).Where(group => group.Count() == 4))
            {
                actions.Add(new SichuanMahjongAction(
                    SichuanMahjongActionKind.ConcealedKong,
                    meldType: MahjongMeldType.ConcealedKong,
                    concealedTiles: group));
            }

            foreach (var pong in melds.Where(meld => meld.Type == MahjongMeldType.Pong))
            {
                foreach (var tile in hand.Where(tile => tile.Kind == pong.Tiles[0].Kind))
                {
                    actions.Add(new SichuanMahjongAction(
                        SichuanMahjongActionKind.AddedKong,
                        tile,
                        meldType: MahjongMeldType.AddedKong));
                }
            }
        }

        return actions;
    }

    private void BeginReactions(ICollection<IGameEvent> events)
    {
        _phase = SichuanMahjongPhase.AwaitingReaction;
        _declinedReactionSeats.Clear();
        _ronWinnersForDiscard.Clear();
        OfferNextReactionOrDraw(events);
    }

    private void OfferNextReactionOrDraw(ICollection<IGameEvent> events)
    {
        var discardSeat = _table.LastDiscardSeat
            ?? throw new InvalidOperationException("Reaction state requires a discard.");
        var candidates = Enum.GetValues<MahjongSeat>()
            .Where(seat => IsActive(seat)
                && seat != discardSeat
                && !_declinedReactionSeats.Contains(seat))
            .Select(seat => new ReactionCandidate(seat, CreateReactionActions(seat)))
            .ToArray();

        var winningCandidate = candidates
            .Where(candidate => candidate.Actions.Any(action =>
                action.Kind == SichuanMahjongActionKind.DiscardWin))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(discardSeat))
            .FirstOrDefault();
        if (winningCandidate is not null)
        {
            Offer(winningCandidate.Seat, winningCandidate.Actions.Where(action =>
                action.Kind == SichuanMahjongActionKind.DiscardWin));
            return;
        }

        if (_ronWinnersForDiscard.Count > 0)
        {
            ClearReactionState();
            ContinueAfterWinner(discardSeat, events);
            return;
        }

        var meldCandidate = candidates
            .Where(candidate => candidate.Actions.Any(action =>
                action.Kind is SichuanMahjongActionKind.Pong or SichuanMahjongActionKind.OpenKong))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(discardSeat))
            .FirstOrDefault();
        if (meldCandidate is not null)
        {
            Offer(meldCandidate.Seat, meldCandidate.Actions.Where(action =>
                action.Kind is SichuanMahjongActionKind.Pong or SichuanMahjongActionKind.OpenKong));
            return;
        }

        ClearReactionState();
        ContinueAfterDiscard(discardSeat, events);
    }

    private IReadOnlyList<SichuanMahjongAction> CreateReactionActions(MahjongSeat seat)
    {
        var discard = _table.LastDiscard
            ?? throw new InvalidOperationException("There is no discard to react to.");
        var hand = _table.GetConcealedTiles(seat);
        var melds = _table.GetMelds(seat);
        var voidSuit = _voidSuits[(int)seat]!.Value;
        var matchingTiles = hand.Where(tile => tile.Kind == discard.Tile.Kind).ToArray();
        var actions = new List<SichuanMahjongAction>();

        if (SichuanMahjongScorer.CanWin(
            hand.Select(tile => tile.Kind).Append(discard.Tile.Kind),
            melds.Count,
            voidSuit))
        {
            actions.Add(new SichuanMahjongAction(
                SichuanMahjongActionKind.DiscardWin,
                discard.Tile));
        }

        if (discard.Tile.Kind.GetSuit() != voidSuit)
        {
            foreach (var pair in Choose(matchingTiles, 2))
            {
                actions.Add(new SichuanMahjongAction(
                    SichuanMahjongActionKind.Pong,
                    discard.Tile,
                    meldType: MahjongMeldType.Pong,
                    concealedTiles: pair));
            }

            if (_table.Wall.ReplacementTilesRemaining > 0 && _table.Wall.LiveTilesRemaining > 0)
            {
                foreach (var triple in Choose(matchingTiles, 3))
                {
                    actions.Add(new SichuanMahjongAction(
                        SichuanMahjongActionKind.OpenKong,
                        discard.Tile,
                        meldType: MahjongMeldType.OpenKong,
                        concealedTiles: triple));
                }
            }
        }

        return actions;
    }

    private void Offer(MahjongSeat seat, IEnumerable<SichuanMahjongAction> actions)
    {
        _offeredReactionSeat = seat;
        _offeredActions = actions
            .Append(new SichuanMahjongAction(SichuanMahjongActionKind.Pass))
            .ToArray();
        _phase = SichuanMahjongPhase.AwaitingReaction;
    }

    private void DrawReplacement(ICollection<IGameEvent> events)
    {
        var tile = _table.DrawCurrent(replacement: true);
        events.Add(new SichuanTileDrawnEvent(NextSequence(), _table.CurrentSeat, tile, true));
        _phase = SichuanMahjongPhase.AwaitingDiscard;
    }

    private void SettleWin(
        MahjongSeat winner,
        MahjongSeat? discardSource,
        ICollection<IGameEvent> events)
    {
        var winningKinds = _table.GetConcealedTiles(winner).Select(tile => tile.Kind);
        if (discardSource is not null)
        {
            winningKinds = winningKinds.Append(_table.LastDiscard!.Tile.Kind);
        }

        var result = SichuanMahjongScorer.CalculateWin(
            winningKinds,
            _table.GetMelds(winner),
            winner,
            discardSource,
            ActiveSeatValues(),
            _baseScore);
        ApplyScoreChanges(result.ScoreChanges);
        _wins.Add(result);
        _activeSeats[(int)winner] = false;
        events.Add(new SichuanWinSettledEvent(NextSequence(), result));
    }

    private IReadOnlyList<long> ApplyKongPayment(
        MahjongMeldType kongType,
        MahjongSeat declarer,
        MahjongSeat? discardSource)
    {
        var changes = SichuanMahjongScorer.CalculateKongPayment(
            kongType,
            declarer,
            discardSource,
            ActiveSeatValues(),
            _baseScore);
        ApplyScoreChanges(changes);
        return changes;
    }

    private void ContinueAfterWinner(MahjongSeat origin, ICollection<IGameEvent> events)
    {
        if (_activeSeats.Count(active => active) <= 1)
        {
            FinishNormally(events);
            return;
        }

        ContinueAfterDiscard(origin, events);
    }

    private void ContinueAfterDiscard(MahjongSeat origin, ICollection<IGameEvent> events)
    {
        if (_table.Wall.LiveTilesRemaining == 0)
        {
            FinishExhaustiveDraw(events);
            return;
        }

        var next = NextActive(origin);
        _table.MoveTurnTo(next);
        var tile = _table.DrawCurrent();
        events.Add(new SichuanTileDrawnEvent(NextSequence(), next, tile, false));
        _phase = SichuanMahjongPhase.AwaitingDiscard;
    }

    private void FinishNormally(ICollection<IGameEvent> events)
    {
        _settlement = new SichuanMahjongSettlement(false, _wins, _scoreChanges);
        _phase = SichuanMahjongPhase.Finished;
        ClearReactionState();
        events.Add(new SichuanMahjongFinishedEvent(NextSequence(), _settlement));
    }

    private void FinishExhaustiveDraw(ICollection<IGameEvent> events)
    {
        var remaining = ActiveSeatValues();
        var draw = SichuanMahjongScorer.CalculateExhaustive(
            remaining.ToDictionary(
                seat => seat,
                seat => (IReadOnlyList<MahjongTileKind>)_table.GetConcealedTiles(seat)
                    .Select(tile => tile.Kind)
                    .ToArray()),
            remaining.ToDictionary(seat => seat, seat => _table.GetMelds(seat)),
            remaining.ToDictionary(seat => seat, seat => _voidSuits[(int)seat]!.Value),
            remaining,
            _baseScore);
        ApplyScoreChanges(draw.ScoreChanges);

        _settlement = new SichuanMahjongSettlement(
            true,
            _wins,
            _scoreChanges,
            draw.FlowerPigSeats,
            draw.TenpaiSeats);
        _phase = SichuanMahjongPhase.Finished;
        ClearReactionState();
        events.Add(new SichuanMahjongFinishedEvent(NextSequence(), _settlement));
    }

    private void ApplyScoreChanges(IReadOnlyList<long> changes)
    {
        for (var index = 0; index < _scoreChanges.Length; index++)
        {
            _scoreChanges[index] += changes[index];
        }
    }

    private MahjongSeat NextActive(MahjongSeat origin)
    {
        for (var distance = 1; distance <= 4; distance++)
        {
            var seat = (MahjongSeat)(((int)origin + distance) % 4);
            if (IsActive(seat))
            {
                return seat;
            }
        }

        throw new InvalidOperationException("There is no active Mahjong seat.");
    }

    private IReadOnlyList<MahjongSeat> ActiveSeatValues()
    {
        return Enum.GetValues<MahjongSeat>().Where(IsActive).ToArray();
    }

    private bool IsActive(MahjongSeat seat)
    {
        return _activeSeats[(int)seat];
    }

    private void ClearReactionState()
    {
        _offeredReactionSeat = null;
        _offeredActions = [];
        _declinedReactionSeats.Clear();
        _ronWinnersForDiscard.Clear();
    }

    private SichuanMahjongSnapshot CreateSnapshot()
    {
        return new SichuanMahjongSnapshot(
            _phase,
            ExchangeDirection,
            _table.Snapshot,
            Enum.GetValues<MahjongSeat>().Select(seat => _exchangeSelections.ContainsKey(seat)),
            _voidSuits,
            _activeSeats,
            _offeredReactionSeat,
            _scoreChanges,
            _wins,
            _settlement);
    }

    private SichuanMahjongCommandResult Accept(IReadOnlyList<IGameEvent> events)
    {
        return new SichuanMahjongCommandResult(true, CreateSnapshot(), events);
    }

    private SichuanMahjongCommandResult Reject(string error)
    {
        return new SichuanMahjongCommandResult(false, CreateSnapshot(), [], error);
    }

    private long NextSequence()
    {
        return ++_sequence;
    }

    private static bool SameTiles(
        IReadOnlyCollection<MahjongTile> first,
        IReadOnlyCollection<MahjongTile> second)
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
        IReadOnlyList<SichuanMahjongAction> Actions);
}
