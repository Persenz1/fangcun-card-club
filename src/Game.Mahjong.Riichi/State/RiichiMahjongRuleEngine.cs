using Game.Core.Random;
using Game.Core.Simulation;
using Game.Mahjong.Analysis;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.Actions;
using Game.Mahjong.Riichi.Commands;
using Game.Mahjong.Riichi.Events;
using Game.Mahjong.Riichi.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.State;

public sealed class RiichiMahjongRuleEngine
{
    private static readonly MahjongWinningOptions AllWinningShapes = new(
        AllowSevenPairs: true,
        AllowThirteenOrphans: true);

    private readonly HashSet<MahjongTileKind> _forbiddenDiscardKinds = [];
    private readonly IDeterministicRandom _random;
    private readonly long[] _scores = [25000, 25000, 25000, 25000];
    private readonly HashSet<MahjongSeat> _declinedReactionSeats = [];
    private readonly List<RiichiWinResult> _ronResults = [];
    private readonly HashSet<MahjongSeat> _ronWinnerSeats = [];
    private bool[] _doubleRiichi = new bool[4];
    private bool[] _ippatsu = new bool[4];
    private bool[] _riichi = new bool[4];
    private bool[] _riichiFuriten = new bool[4];
    private bool[] _temporaryFuriten = new bool[4];
    private bool _callsOccurred;
    private MahjongSeat _dealer;
    private int _handNumber = 1;
    private long[] _handScoreChanges = new long[4];
    private int _honba;
    private bool _lastDiscardFollowedLastLiveDraw;
    private bool _lastDrawWasLastLive;
    private bool _lastDrawWasReplacement;
    private RiichiHandResult? _lastHandResult;
    private RiichiMatchResult? _matchResult;
    private int _openKongCount;
    private int[] _playerKongCounts = new int[4];
    private IReadOnlyList<RiichiMahjongAction> _offeredActions = [];
    private MahjongSeat? _offeredReactionSeat;
    private PendingKong? _pendingKong;
    private PendingRiichi? _pendingRiichi;
    private ReactionContext _reactionContext;
    private int _riichiSticks;
    private RiichiRoundWind _roundWind = RiichiRoundWind.East;
    private long _sequence;
    private RiichiMahjongPhase _phase;
    private MahjongTableState _table = null!;
    private bool _fourKongAbortPending;

    public RiichiMahjongRuleEngine(
        IDeterministicRandom random,
        MahjongSeat initialDealer = MahjongSeat.East)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (!Enum.IsDefined(initialDealer))
        {
            throw new ArgumentOutOfRangeException(nameof(initialDealer));
        }

        _random = random;
        _dealer = initialDealer;
        StartHand(null);
    }

    public RiichiMahjongSnapshot Snapshot => CreateSnapshot();

    public IReadOnlyList<RiichiMahjongAction> GetLegalActions(MahjongSeat seat)
    {
        if (!Enum.IsDefined(seat))
        {
            return [];
        }

        return _phase switch
        {
            RiichiMahjongPhase.AwaitingDiscard when seat == _table.CurrentSeat =>
                CreateTurnActions(seat),
            RiichiMahjongPhase.AwaitingReaction when seat == _offeredReactionSeat =>
                _offeredActions,
            _ => [],
        };
    }

    public RiichiMahjongCommandResult Dispatch(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.PlayerIndex is < 0 or >= 4)
        {
            return Reject("玩家座位无效。");
        }

        var seat = (MahjongSeat)command.PlayerIndex;
        return _phase switch
        {
            RiichiMahjongPhase.AwaitingDiscard => ApplyTurnCommand(seat, command),
            RiichiMahjongPhase.AwaitingReaction => ApplyReactionCommand(seat, command),
            _ => Reject("本场已经结束。"),
        };
    }

    private RiichiMahjongCommandResult ApplyTurnCommand(MahjongSeat seat, IGameCommand command)
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
                when actions.Any(action =>
                    action.Kind == RiichiMahjongActionKind.Discard && action.Tile == discard.Tile):
                if (_riichi[(int)seat])
                {
                    _ippatsu[(int)seat] = false;
                }

                DiscardAndBeginReactions(seat, discard.Tile, events);
                return Accept(events);

            case DeclareRiichiCommand riichi:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == RiichiMahjongActionKind.RiichiDiscard
                        && candidate.Tile == riichi.DiscardTile);
                    if (action is null)
                    {
                        break;
                    }

                    var isDouble = IsFirstUninterruptedTurn(seat);
                    _pendingRiichi = new PendingRiichi(seat, isDouble);
                    DiscardAndBeginReactions(seat, riichi.DiscardTile, events);
                    return Accept(events);
                }

            case DeclareMahjongWinCommand
                when actions.Any(action => action.Kind == RiichiMahjongActionKind.SelfDrawWin):
                FinishSelfDraw(seat, events);
                return Accept(events);

            case DeclareNineTerminalsDrawCommand
                when actions.Any(action => action.Kind == RiichiMahjongActionKind.NineTerminalsDraw):
                FinishAbortiveDraw(RiichiHandEndReason.NineTerminals, events);
                return Accept(events);

            case DeclareConcealedKongCommand concealedKong:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == RiichiMahjongActionKind.ConcealedKong
                        && SameTiles(candidate.ConcealedTiles, concealedKong.Tiles));
                    if (action is null)
                    {
                        break;
                    }

                    BeginKongRobbery(
                        new PendingKong(
                            seat,
                            MahjongMeldType.ConcealedKong,
                            action.ConcealedTiles[^1],
                            action.ConcealedTiles),
                        events);
                    return Accept(events);
                }

            case DeclareAddedKongCommand addedKong:
                {
                    var action = actions.FirstOrDefault(candidate =>
                        candidate.Kind == RiichiMahjongActionKind.AddedKong
                        && candidate.Tile == addedKong.FourthTile);
                    if (action is null)
                    {
                        break;
                    }

                    BeginKongRobbery(
                        new PendingKong(
                            seat,
                            MahjongMeldType.AddedKong,
                            addedKong.FourthTile,
                            [addedKong.FourthTile]),
                        events);
                    return Accept(events);
                }
        }

        return Reject("命令不在当前合法操作中。");
    }

    private RiichiMahjongCommandResult ApplyReactionCommand(
        MahjongSeat seat,
        IGameCommand command)
    {
        if (seat != _offeredReactionSeat)
        {
            return Reject("当前未询问该玩家的反应。");
        }

        var events = new List<IGameEvent>();
        if (command is PassMahjongCommand
            && _offeredActions.Any(action => action.Kind == RiichiMahjongActionKind.Pass))
        {
            var passedWin = _offeredActions.Any(action =>
                action.Kind == RiichiMahjongActionKind.DiscardWin);
            if (passedWin)
            {
                _temporaryFuriten[(int)seat] = true;
                if (_riichi[(int)seat])
                {
                    _riichiFuriten[(int)seat] = true;
                }
            }

            _declinedReactionSeats.Add(seat);
            events.Add(new RiichiReactionPassedEvent(NextSequence(), seat, passedWin));
            OfferNextReaction(events);
            return Accept(events);
        }

        if (command is DeclareMahjongWinCommand
            && _offeredActions.Any(action => action.Kind == RiichiMahjongActionKind.DiscardWin))
        {
            SettleReactionWin(seat, events);
            OfferNextReaction(events);
            return Accept(events);
        }

        if (_reactionContext == ReactionContext.Discard
            && command is ClaimMahjongDiscardCommand claim)
        {
            var action = _offeredActions.FirstOrDefault(candidate =>
                candidate.MeldType == claim.MeldType
                && SameTiles(candidate.ConcealedTiles, claim.ConcealedTiles));
            if (action is not null)
            {
                ApplyDiscardClaim(seat, action, events);
                return Accept(events);
            }
        }

        return Reject("命令不在当前合法反应中。");
    }

    private IReadOnlyList<RiichiMahjongAction> CreateTurnActions(MahjongSeat seat)
    {
        var hand = _table.GetConcealedTiles(seat);
        var melds = _table.GetMelds(seat);
        var lastDraw = _table.Snapshot.LastDrawnTile;
        var actions = new List<RiichiMahjongAction>();

        var discardTiles = _riichi[(int)seat]
            ? lastDraw is { } drawn ? [drawn] : []
            : hand.Where(tile => !_forbiddenDiscardKinds.Contains(tile.Kind)).ToArray();
        actions.AddRange(discardTiles.Select(tile =>
            new RiichiMahjongAction(RiichiMahjongActionKind.Discard, tile)));

        if (lastDraw is { } winningTile && CanWin(seat, winningTile.Kind, selfDraw: true))
        {
            actions.Add(new RiichiMahjongAction(RiichiMahjongActionKind.SelfDrawWin, winningTile));
        }

        if (!_riichi[(int)seat]
            && melds.All(meld => !meld.IsOpen)
            && _scores[(int)seat] >= 1000
            && _table.Wall.LiveTilesRemaining >= 4)
        {
            foreach (var tile in discardTiles.Where(tile => IsTenpaiAfterDiscard(seat, tile)))
            {
                actions.Add(new RiichiMahjongAction(RiichiMahjongActionKind.RiichiDiscard, tile));
            }
        }

        if (lastDraw is not null
            && IsFirstUninterruptedTurn(seat)
            && hand.Where(tile => tile.Kind.IsTerminalOrHonor()).Select(tile => tile.Kind).Distinct().Count() >= 9)
        {
            actions.Add(new RiichiMahjongAction(RiichiMahjongActionKind.NineTerminalsDraw));
        }

        if (lastDraw is not null
            && _openKongCount < 4
            && _table.Wall.ReplacementTilesRemaining > 0
            && _table.Wall.LiveTilesRemaining > 0)
        {
            foreach (var group in hand.GroupBy(tile => tile.Kind).Where(group => group.Count() == 4))
            {
                var tiles = group.ToArray();
                if (!_riichi[(int)seat] || RiichiKongPreservesWait(seat, tiles, lastDraw.Value))
                {
                    actions.Add(new RiichiMahjongAction(
                        RiichiMahjongActionKind.ConcealedKong,
                        meldType: MahjongMeldType.ConcealedKong,
                        concealedTiles: tiles));
                }
            }

            if (!_riichi[(int)seat])
            {
                foreach (var pong in melds.Where(meld => meld.Type == MahjongMeldType.Pong))
                {
                    foreach (var tile in hand.Where(tile => tile.Kind == pong.Tiles[0].Kind))
                    {
                        actions.Add(new RiichiMahjongAction(
                            RiichiMahjongActionKind.AddedKong,
                            tile,
                            MahjongMeldType.AddedKong));
                    }
                }
            }
        }

        return actions;
    }

    private void DiscardAndBeginReactions(
        MahjongSeat seat,
        MahjongTile tile,
        ICollection<IGameEvent> events)
    {
        _lastDiscardFollowedLastLiveDraw = _lastDrawWasLastLive;
        var riverTile = _table.Discard(seat, tile);
        _forbiddenDiscardKinds.Clear();
        _lastDrawWasLastLive = false;
        _lastDrawWasReplacement = false;
        events.Add(new RiichiTileDiscardedEvent(NextSequence(), seat, riverTile));
        _reactionContext = ReactionContext.Discard;
        BeginReactions(events);
    }

    private void BeginKongRobbery(PendingKong kong, ICollection<IGameEvent> events)
    {
        _pendingKong = kong;
        _reactionContext = ReactionContext.Kong;
        BeginReactions(events);
    }

    private void BeginReactions(ICollection<IGameEvent> events)
    {
        _phase = RiichiMahjongPhase.AwaitingReaction;
        _declinedReactionSeats.Clear();
        _ronResults.Clear();
        _ronWinnerSeats.Clear();
        OfferNextReaction(events);
    }

    private void OfferNextReaction(ICollection<IGameEvent> events)
    {
        if (_reactionContext == ReactionContext.Kong)
        {
            OfferNextKongReaction(events);
            return;
        }

        OfferNextDiscardReaction(events);
    }

    private void OfferNextDiscardReaction(ICollection<IGameEvent> events)
    {
        var source = _table.LastDiscardSeat
            ?? throw new InvalidOperationException("Discard reactions require a source.");
        var candidates = Enum.GetValues<MahjongSeat>()
            .Where(seat => seat != source
                && !_declinedReactionSeats.Contains(seat)
                && !_ronWinnerSeats.Contains(seat))
            .Select(seat => new ReactionCandidate(seat, CreateDiscardReactionActions(seat)))
            .ToArray();
        var winner = candidates
            .Where(candidate => candidate.Actions.Any(action =>
                action.Kind == RiichiMahjongActionKind.DiscardWin))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(source))
            .FirstOrDefault();
        if (winner is not null)
        {
            Offer(winner.Seat, winner.Actions.Where(action =>
                action.Kind == RiichiMahjongActionKind.DiscardWin));
            return;
        }

        if (_ronResults.Count > 0)
        {
            _pendingRiichi = null;
            ClearReactionState();
            FinishRon(events);
            return;
        }

        EstablishPendingRiichi(events);
        var abortReason = PendingAbortReason();
        if (abortReason is not null)
        {
            ClearReactionState();
            FinishAbortiveDraw(abortReason.Value, events);
            return;
        }

        var meldCandidate = candidates
            .Where(candidate => candidate.Actions.Any(action => action.Kind is
                RiichiMahjongActionKind.Pong or RiichiMahjongActionKind.OpenKong))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(source))
            .FirstOrDefault();
        if (meldCandidate is not null)
        {
            Offer(meldCandidate.Seat, meldCandidate.Actions.Where(action => action.Kind is
                RiichiMahjongActionKind.Pong or RiichiMahjongActionKind.OpenKong));
            return;
        }

        var chowCandidate = candidates.SingleOrDefault(candidate =>
            candidate.Seat == source.Next()
            && candidate.Actions.Any(action => action.Kind == RiichiMahjongActionKind.Chow));
        if (chowCandidate is not null)
        {
            Offer(chowCandidate.Seat, chowCandidate.Actions.Where(action =>
                action.Kind == RiichiMahjongActionKind.Chow));
            return;
        }

        ClearReactionState();
        DrawNextOrFinish(events);
    }

    private void OfferNextKongReaction(ICollection<IGameEvent> events)
    {
        var kong = _pendingKong
            ?? throw new InvalidOperationException("Kong reactions require a pending kong.");
        var winner = Enum.GetValues<MahjongSeat>()
            .Where(seat => seat != kong.Declarer
                && !_declinedReactionSeats.Contains(seat)
                && !_ronWinnerSeats.Contains(seat))
            .Select(seat => new ReactionCandidate(seat, CreateKongReactionActions(seat, kong)))
            .Where(candidate => candidate.Actions.Any(action =>
                action.Kind == RiichiMahjongActionKind.DiscardWin))
            .OrderBy(candidate => candidate.Seat.DistanceFrom(kong.Declarer))
            .FirstOrDefault();
        if (winner is not null)
        {
            Offer(winner.Seat, winner.Actions);
            return;
        }

        if (_ronResults.Count > 0)
        {
            ClearReactionState();
            _pendingKong = null;
            FinishRon(events);
            return;
        }

        CommitPendingKong(events);
    }

    private IReadOnlyList<RiichiMahjongAction> CreateDiscardReactionActions(MahjongSeat seat)
    {
        var discard = _table.LastDiscard
            ?? throw new InvalidOperationException("There is no discard to react to.");
        var source = _table.LastDiscardSeat!.Value;
        var hand = _table.GetConcealedTiles(seat);
        var actions = new List<RiichiMahjongAction>();
        if (CanWin(seat, discard.Tile.Kind, selfDraw: false))
        {
            actions.Add(new RiichiMahjongAction(RiichiMahjongActionKind.DiscardWin, discard.Tile));
        }

        if (_riichi[(int)seat] || _table.Wall.LiveTilesRemaining == 0)
        {
            return actions;
        }

        var matching = hand.Where(tile => tile.Kind == discard.Tile.Kind).ToArray();
        foreach (var pair in Choose(matching, 2))
        {
            actions.Add(new RiichiMahjongAction(
                RiichiMahjongActionKind.Pong,
                discard.Tile,
                MahjongMeldType.Pong,
                pair));
        }

        if (_openKongCount < 4
            && _table.Wall.ReplacementTilesRemaining > 0
            && _table.Wall.LiveTilesRemaining > 0)
        {
            foreach (var triple in Choose(matching, 3))
            {
                actions.Add(new RiichiMahjongAction(
                    RiichiMahjongActionKind.OpenKong,
                    discard.Tile,
                    MahjongMeldType.OpenKong,
                    triple));
            }
        }

        if (seat == source.Next() && discard.Tile.Kind.IsSuited())
        {
            actions.AddRange(CreateChowActions(hand, discard.Tile));
        }

        return actions;
    }

    private IReadOnlyList<RiichiMahjongAction> CreateKongReactionActions(
        MahjongSeat seat,
        PendingKong kong)
    {
        if (IsFuriten(seat))
        {
            return [];
        }

        var handKinds = _table.GetConcealedTiles(seat).Select(tile => tile.Kind).Append(kong.RobbedTile.Kind);
        var context = CreateWinContext(
            seat,
            kong.RobbedTile.Kind,
            selfDraw: false,
            isChankan: true,
            riichiSticksAwarded: 0);
        if (!RiichiMahjongScorer.TryEvaluate(
            handKinds,
            _table.GetMelds(seat),
            context,
            out var score))
        {
            return [];
        }

        if (kong.Type == MahjongMeldType.ConcealedKong
            && !score!.Yaku.Contains("国士无双"))
        {
            return [];
        }

        return [new RiichiMahjongAction(RiichiMahjongActionKind.DiscardWin, kong.RobbedTile)];
    }

    private static IEnumerable<RiichiMahjongAction> CreateChowActions(
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
            foreach (var first in hand.Where(tile => tile.Kind == neededKinds[0]))
            {
                foreach (var second in hand.Where(tile => tile.Kind == neededKinds[1]))
                {
                    yield return new RiichiMahjongAction(
                        RiichiMahjongActionKind.Chow,
                        discard,
                        MahjongMeldType.Chow,
                        [first, second]);
                }
            }
        }
    }

    private void ApplyDiscardClaim(
        MahjongSeat seat,
        RiichiMahjongAction action,
        ICollection<IGameEvent> events)
    {
        var claimedKind = _table.LastDiscard!.Tile.Kind;
        var meld = _table.ClaimDiscard(seat, action.MeldType!.Value, action.ConcealedTiles);
        ClearReactionState();
        ClearIppatsuForCall();
        events.Add(new RiichiMeldDeclaredEvent(NextSequence(), seat, meld));
        if (meld.Type == MahjongMeldType.OpenKong)
        {
            RegisterKong(seat, events);
            DrawReplacement(events);
            return;
        }

        SetForbiddenDiscards(meld, claimedKind);
        _lastDrawWasLastLive = false;
        _lastDrawWasReplacement = false;
        _phase = RiichiMahjongPhase.AwaitingDiscard;
    }

    private void SetForbiddenDiscards(MahjongMeld meld, MahjongTileKind claimedKind)
    {
        _forbiddenDiscardKinds.Clear();
        _forbiddenDiscardKinds.Add(claimedKind);
        if (meld.Type != MahjongMeldType.Chow)
        {
            return;
        }

        var first = meld.Tiles[0].Kind;
        var last = meld.Tiles[^1].Kind;
        if (claimedKind == first && last.GetNumber() < 9)
        {
            _forbiddenDiscardKinds.Add((MahjongTileKind)((int)last + 1));
        }
        else if (claimedKind == last && first.GetNumber() > 1)
        {
            _forbiddenDiscardKinds.Add((MahjongTileKind)((int)first - 1));
        }
    }

    private void CommitPendingKong(ICollection<IGameEvent> events)
    {
        var kong = _pendingKong
            ?? throw new InvalidOperationException("There is no pending kong to commit.");
        MahjongMeld meld = kong.Type switch
        {
            MahjongMeldType.ConcealedKong => _table.DeclareConcealedKong(kong.Declarer, kong.Tiles),
            MahjongMeldType.AddedKong => _table.DeclareAddedKong(kong.Declarer, kong.RobbedTile),
            _ => throw new InvalidOperationException("Only self-declared kongs can be pending."),
        };
        ClearReactionState();
        _pendingKong = null;
        ClearIppatsuForCall();
        events.Add(new RiichiMeldDeclaredEvent(NextSequence(), kong.Declarer, meld));
        RegisterKong(kong.Declarer, events);
        DrawReplacement(events);
    }

    private void RegisterKong(MahjongSeat declarer, ICollection<IGameEvent> events)
    {
        _openKongCount++;
        _playerKongCounts[(int)declarer]++;
        var indicators = DoraIndicators();
        events.Add(new RiichiDoraRevealedEvent(NextSequence(), indicators[^1]));
        if (_openKongCount == 4 && _playerKongCounts.Count(count => count > 0) > 1)
        {
            _fourKongAbortPending = true;
        }
    }

    private void DrawReplacement(ICollection<IGameEvent> events)
    {
        var seat = _table.CurrentSeat;
        _temporaryFuriten[(int)seat] = false;
        var tile = _table.DrawCurrent(replacement: true);
        _lastDrawWasReplacement = true;
        _lastDrawWasLastLive = false;
        _phase = RiichiMahjongPhase.AwaitingDiscard;
        events.Add(new RiichiTileDrawnEvent(NextSequence(), seat, tile, true));
    }

    private void SettleReactionWin(MahjongSeat winner, ICollection<IGameEvent> events)
    {
        var (source, winningTile, chankan) = _reactionContext switch
        {
            ReactionContext.Discard => (
                _table.LastDiscardSeat!.Value,
                _table.LastDiscard!.Tile,
                false),
            ReactionContext.Kong => (
                _pendingKong!.Declarer,
                _pendingKong.RobbedTile,
                true),
            _ => throw new InvalidOperationException("There is no win reaction context."),
        };
        var context = CreateWinContext(
            winner,
            winningTile.Kind,
            selfDraw: false,
            isChankan: chankan,
            riichiSticksAwarded: _ronResults.Count == 0 ? _riichiSticks : 0);
        var result = RiichiMahjongScorer.CalculateWin(
            _table.GetConcealedTiles(winner).Select(tile => tile.Kind).Append(winningTile.Kind),
            _table.GetMelds(winner),
            context,
            source);
        ApplyScoreChanges(result.ScoreChanges);
        if (_ronResults.Count == 0)
        {
            _riichiSticks = 0;
        }

        _ronResults.Add(result);
        _ronWinnerSeats.Add(winner);
        events.Add(new RiichiWinSettledEvent(NextSequence(), result));
    }

    private void FinishSelfDraw(MahjongSeat winner, ICollection<IGameEvent> events)
    {
        var winningTile = _table.Snapshot.LastDrawnTile!.Value;
        var context = CreateWinContext(
            winner,
            winningTile.Kind,
            selfDraw: true,
            isChankan: false,
            riichiSticksAwarded: _riichiSticks);
        var result = RiichiMahjongScorer.CalculateWin(
            _table.GetConcealedTiles(winner).Select(tile => tile.Kind),
            _table.GetMelds(winner),
            context,
            null);
        ApplyScoreChanges(result.ScoreChanges);
        _riichiSticks = 0;
        events.Add(new RiichiWinSettledEvent(NextSequence(), result));
        CompleteHand(
            RiichiHandEndReason.Tsumo,
            [result],
            [],
            winner == _dealer,
            events);
    }

    private void FinishRon(ICollection<IGameEvent> events)
    {
        var wins = _ronResults.ToArray();
        CompleteHand(
            RiichiHandEndReason.Ron,
            wins,
            [],
            wins.Any(win => win.Winner == _dealer),
            events);
    }

    private void DrawNextOrFinish(ICollection<IGameEvent> events)
    {
        if (_table.Wall.LiveTilesRemaining == 0)
        {
            FinishExhaustiveDraw(events);
            return;
        }

        var seat = _table.CurrentSeat;
        _temporaryFuriten[(int)seat] = false;
        var tile = _table.DrawCurrent();
        _lastDrawWasReplacement = false;
        _lastDrawWasLastLive = _table.Wall.LiveTilesRemaining == 0;
        _phase = RiichiMahjongPhase.AwaitingDiscard;
        events.Add(new RiichiTileDrawnEvent(NextSequence(), seat, tile, false));
    }

    private void FinishExhaustiveDraw(ICollection<IGameEvent> events)
    {
        var nagashiWinners = Enum.GetValues<MahjongSeat>()
            .Where(IsNagashiMangan)
            .OrderBy(seat => seat.DistanceFrom(_dealer))
            .ToArray();
        if (nagashiWinners.Length > 0)
        {
            var wins = new List<RiichiWinResult>();
            foreach (var winner in nagashiWinners)
            {
                var result = CalculateNagashiMangan(
                    winner,
                    wins.Count == 0 ? _riichiSticks : 0);
                ApplyScoreChanges(result.ScoreChanges);
                wins.Add(result);
            }

            _riichiSticks = 0;
            CompleteHand(
                RiichiHandEndReason.NagashiMangan,
                wins,
                [],
                nagashiWinners.Contains(_dealer),
                events);
            return;
        }

        var tenpaiSeats = Enum.GetValues<MahjongSeat>()
            .Where(seat => RiichiMahjongScorer.GetWinningKinds(
                _table.GetConcealedTiles(seat).Select(tile => tile.Kind),
                _table.GetMelds(seat).Count).Count > 0)
            .ToArray();
        ApplyNotenPayments(tenpaiSeats);
        CompleteHand(
            RiichiHandEndReason.ExhaustiveDraw,
            [],
            tenpaiSeats,
            tenpaiSeats.Contains(_dealer),
            events);
    }

    private void ApplyNotenPayments(IReadOnlyCollection<MahjongSeat> tenpaiSeats)
    {
        ApplyScoreChanges(RiichiMahjongScorer.CalculateNotenPayments(tenpaiSeats));
    }

    private RiichiWinResult CalculateNagashiMangan(MahjongSeat winner, int sticksAwarded)
    {
        var handScore = new RiichiHandScore(5, 0, 0, 0, 2000, "满贯", ["流局满贯"]);
        var changes = new long[4];
        foreach (var payer in Enum.GetValues<MahjongSeat>().Where(seat => seat != winner))
        {
            var multiplier = winner == _dealer || payer == _dealer ? 2 : 1;
            var payment = RoundUpToHundred(handScore.BasicPoints * multiplier) + (_honba * 100L);
            changes[(int)payer] -= payment;
            changes[(int)winner] += payment;
        }

        changes[(int)winner] += sticksAwarded * 1000L;
        return new RiichiWinResult(winner, null, handScore, changes);
    }

    private void FinishAbortiveDraw(
        RiichiHandEndReason reason,
        ICollection<IGameEvent> events)
    {
        CompleteHand(reason, [], [], dealerRepeats: true, events);
    }

    private void CompleteHand(
        RiichiHandEndReason reason,
        IReadOnlyList<RiichiWinResult> wins,
        IReadOnlyList<MahjongSeat> tenpaiSeats,
        bool dealerRepeats,
        ICollection<IGameEvent> events)
    {
        _lastHandResult = new RiichiHandResult(
            reason,
            wins,
            tenpaiSeats,
            _handScoreChanges,
            dealerRepeats);
        events.Add(new RiichiHandFinishedEvent(NextSequence(), _lastHandResult));

        var isWin = reason is RiichiHandEndReason.Ron
            or RiichiHandEndReason.Tsumo
            or RiichiHandEndReason.NagashiMangan;
        if (dealerRepeats)
        {
            _honba++;
        }
        else
        {
            _honba = isWin ? 0 : _honba + 1;
        }

        if (_scores.Any(score => score < 0))
        {
            FinishMatch(events);
            return;
        }

        if (!dealerRepeats)
        {
            if (ShouldEndAfterCurrentHand())
            {
                FinishMatch(events);
                return;
            }

            AdvanceDealer();
        }

        StartHand(events);
    }

    private bool ShouldEndAfterCurrentHand()
    {
        if (_roundWind == RiichiRoundWind.East && _handNumber < 4)
        {
            return false;
        }

        if (_roundWind == RiichiRoundWind.East)
        {
            return _scores.Max() >= 30000;
        }

        return _handNumber >= 4 || _scores.Max() >= 30000;
    }

    private void AdvanceDealer()
    {
        _dealer = _dealer.Next();
        _handNumber++;
        if (_handNumber <= 4)
        {
            return;
        }

        _roundWind = RiichiRoundWind.South;
        _handNumber = 1;
    }

    private void FinishMatch(ICollection<IGameEvent> events)
    {
        if (_riichiSticks > 0)
        {
            var leader = Enum.GetValues<MahjongSeat>()
                .OrderByDescending(seat => _scores[(int)seat])
                .ThenBy(seat => seat)
                .First();
            _scores[(int)leader] += _riichiSticks * 1000L;
            _riichiSticks = 0;
        }

        var ranking = Enum.GetValues<MahjongSeat>()
            .OrderByDescending(seat => _scores[(int)seat])
            .ThenBy(seat => seat)
            .ToArray();
        _matchResult = new RiichiMatchResult(_scores, ranking);
        _phase = RiichiMahjongPhase.Finished;
        ClearReactionState();
        events.Add(new RiichiMatchFinishedEvent(NextSequence(), _matchResult));
    }

    private void StartHand(ICollection<IGameEvent>? events)
    {
        var wall = new MahjongWall(_random, deadWallSize: 14, replacementLimit: 4);
        _table = new MahjongTableState(wall, _dealer);
        _riichi = new bool[4];
        _doubleRiichi = new bool[4];
        _ippatsu = new bool[4];
        _temporaryFuriten = new bool[4];
        _riichiFuriten = new bool[4];
        _playerKongCounts = new int[4];
        _handScoreChanges = new long[4];
        _openKongCount = 0;
        _callsOccurred = false;
        _fourKongAbortPending = false;
        _pendingKong = null;
        _pendingRiichi = null;
        _forbiddenDiscardKinds.Clear();
        _lastDrawWasReplacement = false;
        _lastDrawWasLastLive = false;
        _lastDiscardFollowedLastLiveDraw = false;
        ClearReactionState();
        _phase = RiichiMahjongPhase.AwaitingDiscard;
        events?.Add(new RiichiHandStartedEvent(
            NextSequence(),
            _roundWind,
            _handNumber,
            _dealer,
            _honba,
            _riichiSticks,
            DoraIndicators()[0]));
    }

    private void EstablishPendingRiichi(ICollection<IGameEvent> events)
    {
        if (_pendingRiichi is not { } pending)
        {
            return;
        }

        _scores[(int)pending.Seat] -= 1000;
        _handScoreChanges[(int)pending.Seat] -= 1000;
        _riichiSticks++;
        _riichi[(int)pending.Seat] = true;
        _doubleRiichi[(int)pending.Seat] = pending.IsDouble;
        _ippatsu[(int)pending.Seat] = true;
        _pendingRiichi = null;
        events.Add(new RiichiDeclaredEvent(NextSequence(), pending.Seat, pending.IsDouble));
    }

    private RiichiHandEndReason? PendingAbortReason()
    {
        if (_fourKongAbortPending)
        {
            return RiichiHandEndReason.FourKongs;
        }

        if (_riichi.All(value => value))
        {
            return RiichiHandEndReason.FourRiichi;
        }

        var rivers = Enum.GetValues<MahjongSeat>().Select(_table.GetRiver).ToArray();
        if (!_callsOccurred
            && rivers.All(river => river.Count == 1)
            && rivers.Select(river => river[0].Tile.Kind).Distinct().Count() == 1
            && rivers[0][0].Tile.Kind is >= MahjongTileKind.East and <= MahjongTileKind.North)
        {
            return RiichiHandEndReason.FourWinds;
        }

        return null;
    }

    private bool CanWin(MahjongSeat seat, MahjongTileKind winningKind, bool selfDraw)
    {
        if (!selfDraw && IsFuriten(seat))
        {
            return false;
        }

        IEnumerable<MahjongTileKind> kinds = _table.GetConcealedTiles(seat).Select(tile => tile.Kind);
        if (!selfDraw)
        {
            kinds = kinds.Append(winningKind);
        }

        var context = CreateWinContext(
            seat,
            winningKind,
            selfDraw,
            isChankan: false,
            riichiSticksAwarded: 0);
        return RiichiMahjongScorer.TryEvaluate(
            kinds,
            _table.GetMelds(seat),
            context,
            out _);
    }

    private RiichiWinContext CreateWinContext(
        MahjongSeat winner,
        MahjongTileKind winningKind,
        bool selfDraw,
        bool isChankan,
        int riichiSticksAwarded)
    {
        var firstTurn = IsFirstUninterruptedTurn(winner);
        return new RiichiWinContext(
            winner,
            _dealer,
            _roundWind,
            winningKind,
            selfDraw,
            isRiichi: _riichi[(int)winner],
            isDoubleRiichi: _doubleRiichi[(int)winner],
            isIppatsu: _ippatsu[(int)winner],
            isRinshan: selfDraw && _lastDrawWasReplacement,
            isChankan: isChankan,
            isHaitei: selfDraw && _lastDrawWasLastLive,
            isHoutei: !selfDraw && !isChankan && _lastDiscardFollowedLastLiveDraw,
            isTenhou: selfDraw && winner == _dealer && firstTurn,
            isChiihou: selfDraw && winner != _dealer && firstTurn,
            doraIndicators: DoraIndicators(),
            uraDoraIndicators: UraDoraIndicators(),
            honba: _honba,
            riichiSticksAwarded: riichiSticksAwarded);
    }

    private bool IsFuriten(MahjongSeat seat)
    {
        if (_temporaryFuriten[(int)seat] || _riichiFuriten[(int)seat])
        {
            return true;
        }

        var waits = RiichiMahjongScorer.GetWinningKinds(
            _table.GetConcealedTiles(seat).Select(tile => tile.Kind),
            _table.GetMelds(seat).Count);
        return _table.GetRiver(seat).Any(tile => waits.Contains(tile.Tile.Kind));
    }

    private bool IsTenpaiAfterDiscard(MahjongSeat seat, MahjongTile discard)
    {
        return RiichiMahjongScorer.GetWinningKinds(
            _table.GetConcealedTiles(seat)
                .Where(tile => tile != discard)
                .Select(tile => tile.Kind),
            _table.GetMelds(seat).Count).Count > 0;
    }

    private bool RiichiKongPreservesWait(
        MahjongSeat seat,
        IReadOnlyCollection<MahjongTile> kongTiles,
        MahjongTile lastDraw)
    {
        var hand = _table.GetConcealedTiles(seat);
        var before = RiichiMahjongScorer.GetWinningKinds(
            hand.Where(tile => tile != lastDraw).Select(tile => tile.Kind),
            _table.GetMelds(seat).Count);
        var after = RiichiMahjongScorer.GetWinningKinds(
            hand.Where(tile => !kongTiles.Contains(tile)).Select(tile => tile.Kind),
            _table.GetMelds(seat).Count + 1);
        return before.OrderBy(kind => kind).SequenceEqual(after.OrderBy(kind => kind));
    }

    private bool IsFirstUninterruptedTurn(MahjongSeat seat)
    {
        return !_callsOccurred && _table.GetRiver(seat).Count == 0;
    }

    private bool IsNagashiMangan(MahjongSeat seat)
    {
        var river = _table.GetRiver(seat);
        return river.Count > 0
            && _table.GetMelds(seat).Count == 0
            && river.All(tile => !tile.IsClaimed && tile.Tile.Kind.IsTerminalOrHonor());
    }

    private IReadOnlyList<MahjongTileKind> DoraIndicators()
    {
        return Enumerable.Range(0, _openKongCount + 1)
            .Select(index => _table.Wall.DeadWall[8 - (index * 2)].Kind)
            .ToArray();
    }

    private IReadOnlyList<MahjongTileKind> UraDoraIndicators()
    {
        return Enumerable.Range(0, _openKongCount + 1)
            .Select(index => _table.Wall.DeadWall[9 - (index * 2)].Kind)
            .ToArray();
    }

    private void ClearIppatsuForCall()
    {
        Array.Fill(_ippatsu, false);
        _callsOccurred = true;
    }

    private void ApplyScoreChanges(IReadOnlyList<long> changes)
    {
        for (var index = 0; index < 4; index++)
        {
            _scores[index] += changes[index];
            _handScoreChanges[index] += changes[index];
        }
    }

    private void Offer(MahjongSeat seat, IEnumerable<RiichiMahjongAction> actions)
    {
        _offeredReactionSeat = seat;
        _offeredActions = actions
            .Append(new RiichiMahjongAction(RiichiMahjongActionKind.Pass))
            .ToArray();
        _phase = RiichiMahjongPhase.AwaitingReaction;
    }

    private void ClearReactionState()
    {
        _offeredReactionSeat = null;
        _offeredActions = [];
        _declinedReactionSeats.Clear();
        _ronWinnerSeats.Clear();
        _reactionContext = ReactionContext.None;
    }

    private RiichiMahjongSnapshot CreateSnapshot()
    {
        return new RiichiMahjongSnapshot(
            _phase,
            _roundWind,
            _handNumber,
            _dealer,
            _honba,
            _riichiSticks,
            _scores,
            _table.Snapshot,
            _riichi,
            _doubleRiichi,
            Enum.GetValues<MahjongSeat>().Select(IsFuriten),
            _pendingRiichi?.Seat,
            _offeredReactionSeat,
            DoraIndicators(),
            _lastHandResult,
            _matchResult);
    }

    private RiichiMahjongCommandResult Accept(IReadOnlyList<IGameEvent> events)
    {
        return new RiichiMahjongCommandResult(true, CreateSnapshot(), events);
    }

    private RiichiMahjongCommandResult Reject(string error)
    {
        return new RiichiMahjongCommandResult(false, CreateSnapshot(), [], error);
    }

    private long NextSequence()
    {
        return ++_sequence;
    }

    private static long RoundUpToHundred(long points)
    {
        return ((points + 99) / 100) * 100;
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

    private enum ReactionContext
    {
        None,
        Discard,
        Kong,
    }

    private sealed record PendingRiichi(MahjongSeat Seat, bool IsDouble);

    private sealed record PendingKong(
        MahjongSeat Declarer,
        MahjongMeldType Type,
        MahjongTile RobbedTile,
        IReadOnlyList<MahjongTile> Tiles);

    private sealed record ReactionCandidate(
        MahjongSeat Seat,
        IReadOnlyList<RiichiMahjongAction> Actions);
}
