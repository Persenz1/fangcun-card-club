using Game.Core.Random;
using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Sichuan.Actions;
using Game.Mahjong.Sichuan.AI;
using Game.Mahjong.Sichuan.Commands;
using Game.Mahjong.Sichuan.Events;
using Game.Mahjong.Sichuan.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Application.Mahjong.Sichuan;

public sealed class SichuanMahjongGameSession : IMahjongGameSession
{
    private readonly BasicSichuanMahjongAi _ai = new();
    private readonly SichuanMahjongRuleEngine _engine;

    private SichuanMahjongGameSession(ulong seed, MahjongSeat humanSeat, int baseScore)
    {
        if (!Enum.IsDefined(humanSeat))
        {
            throw new ArgumentOutOfRangeException(nameof(humanSeat));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);
        Seed = seed;
        HumanSeat = humanSeat;
        _engine = new SichuanMahjongRuleEngine(new SplitMix64Random(seed), baseScore: baseScore);
    }

    public event Action? StateChanged;

    public MahjongMode Mode => MahjongMode.Sichuan;

    public ulong Seed { get; }

    public MahjongSeat HumanSeat { get; }

    public MahjongSessionView Snapshot => CreateView();

    public SichuanMahjongSnapshot RuleSnapshot => _engine.Snapshot;

    public static SichuanMahjongGameSession Start(
        ulong seed,
        MahjongSeat humanSeat = MahjongSeat.East,
        int baseScore = 10)
    {
        return new SichuanMahjongGameSession(seed, humanSeat, baseScore);
    }

    public MahjongSessionResult Dispatch(int actionId)
    {
        var mapping = CreateMappings(HumanSeat).FirstOrDefault(item => item.Option.Id == actionId);
        return mapping is null
            ? Reject("该选项不在当前玩家合法操作中。")
            : DispatchAndPublish(mapping.Command);
    }

    public MahjongSessionResult DispatchSuggestedAction()
    {
        var view = CreateView();
        return view.SuggestedActionId is { } actionId
            ? Dispatch(actionId)
            : Reject("当前没有可用提示。");
    }

    public MahjongSessionResult AdvanceAiTurn()
    {
        var seat = FindAiSeat();
        if (seat is null)
        {
            return Reject("当前没有等待执行的 AI 操作。");
        }

        var actions = _engine.GetLegalActions(seat.Value);
        var command = _ai.ChooseCommand(_engine.Snapshot, seat.Value, actions);
        return DispatchAndPublish(command);
    }

    private MahjongSessionResult DispatchAndPublish(IGameCommand command)
    {
        var result = _engine.Dispatch(command);
        var events = result.Events.Select(MapEvent).ToArray();
        if (result.Accepted)
        {
            StateChanged?.Invoke();
        }

        return new MahjongSessionResult(
            result.Accepted,
            CreateView(),
            events,
            result.Error);
    }

    private MahjongSessionResult Reject(string error)
    {
        return new MahjongSessionResult(false, CreateView(), [], error);
    }

    private MahjongSessionView CreateView()
    {
        var snapshot = _engine.Snapshot;
        var mappings = CreateMappings(HumanSeat);
        var statuses = Enum.GetValues<MahjongSeat>()
            .Select(seat =>
            {
                var values = new List<string>();
                if (!snapshot.ActiveSeats[(int)seat])
                {
                    values.Add("已和退出");
                }

                if (snapshot.ExchangeSubmitted[(int)seat])
                {
                    values.Add("已换牌");
                }

                if (snapshot.VoidSuits[(int)seat] is { } suit)
                {
                    values.Add($"缺{MahjongText.Suit(suit)}");
                }

                if (seat == snapshot.OfferedReactionSeat)
                {
                    values.Add("等待响应");
                }
                else if (snapshot.Phase == SichuanMahjongPhase.AwaitingDiscard
                    && seat == snapshot.Table.CurrentSeat)
                {
                    values.Add("行动中");
                }

                return (IReadOnlyList<string>)values;
            })
            .ToArray();
        var table = MahjongPresentationBuilder.CreateTable(
            snapshot.Table,
            HumanSeat,
            snapshot.OfferedReactionSeat,
            snapshot.ScoreChanges,
            statuses);
        var prompt = snapshot.Phase switch
        {
            SichuanMahjongPhase.ExchangeThree when mappings.Count > 0 => "选择同一门的三张牌同时交换",
            SichuanMahjongPhase.DeclareVoidSuit when mappings.Count > 0 => "选择本局定缺门",
            SichuanMahjongPhase.Finished => "血战结束",
            _ when mappings.Count > 0 => "请从规则引擎给出的合法操作中选择",
            _ when FindAiSeat() is { } seat => $"{MahjongText.Seat(seat)}思考中",
            _ => "等待其他座位完成特殊流程",
        };
        return new MahjongSessionView(
            Mode,
            HumanSeat,
            PhaseText(snapshot.Phase),
            prompt,
            table,
            [
                new MahjongHudItem("玩法", "四川血战"),
                new MahjongHudItem("换牌", DirectionText(snapshot.ExchangeDirection)),
                new MahjongHudItem("余牌", snapshot.Table.LiveTilesRemaining.ToString()),
                new MahjongHudItem("在战", snapshot.ActiveSeats.Count(active => active).ToString()),
            ],
            [],
            mappings.Select(mapping => mapping.Option),
            FindSuggestedActionId(snapshot, mappings),
            FindAiSeat() is not null,
            snapshot.Phase == SichuanMahjongPhase.Finished,
            CreateSettlementLines(snapshot));
    }

    private IReadOnlyList<MappedAction> CreateMappings(MahjongSeat seat)
    {
        return _engine.GetLegalActions(seat)
            .Select((action, index) => new MappedAction(
                CreateOption(index, action),
                CreateCommand(seat, action)))
            .ToArray();
    }

    private int? FindSuggestedActionId(
        SichuanMahjongSnapshot snapshot,
        IReadOnlyList<MappedAction> mappings)
    {
        if (mappings.Count == 0)
        {
            return null;
        }

        var command = _ai.ChooseCommand(
            snapshot,
            HumanSeat,
            _engine.GetLegalActions(HumanSeat));
        return mappings.First(mapping => CommandsMatch(mapping.Command, command)).Option.Id;
    }

    private MahjongSeat? FindAiSeat()
    {
        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            if (seat != HumanSeat && _engine.GetLegalActions(seat).Count > 0)
            {
                return seat;
            }
        }

        return null;
    }

    private static MahjongActionOption CreateOption(int id, SichuanMahjongAction action)
    {
        var kind = action.Kind switch
        {
            SichuanMahjongActionKind.ExchangeThree => MahjongActionViewKind.ExchangeThree,
            SichuanMahjongActionKind.DeclareVoidSuit => MahjongActionViewKind.DeclareVoidSuit,
            SichuanMahjongActionKind.Discard => MahjongActionViewKind.Discard,
            SichuanMahjongActionKind.SelfDrawWin or SichuanMahjongActionKind.DiscardWin => MahjongActionViewKind.Win,
            SichuanMahjongActionKind.Pong => MahjongActionViewKind.Pong,
            SichuanMahjongActionKind.OpenKong => MahjongActionViewKind.OpenKong,
            SichuanMahjongActionKind.ConcealedKong => MahjongActionViewKind.ConcealedKong,
            SichuanMahjongActionKind.AddedKong => MahjongActionViewKind.AddedKong,
            SichuanMahjongActionKind.Pass => MahjongActionViewKind.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var label = action.Kind switch
        {
            SichuanMahjongActionKind.ExchangeThree => $"换出 {MahjongText.Tiles(action.ConcealedTiles)}",
            SichuanMahjongActionKind.DeclareVoidSuit => $"定缺 {MahjongText.Suit(action.Suit!.Value)}",
            SichuanMahjongActionKind.Discard => $"弃牌 {MahjongText.Tile(action.Tile!.Value)}",
            SichuanMahjongActionKind.SelfDrawWin => "自摸胡",
            SichuanMahjongActionKind.DiscardWin => "点炮胡",
            SichuanMahjongActionKind.Pong => $"碰 {MahjongText.Tile(action.Tile!.Value)}",
            SichuanMahjongActionKind.OpenKong => $"点杠 {MahjongText.Tile(action.Tile!.Value)}",
            SichuanMahjongActionKind.ConcealedKong => $"暗杠 {MahjongText.Tile(action.ConcealedTiles[0])}",
            SichuanMahjongActionKind.AddedKong => $"补杠 {MahjongText.Tile(action.Tile!.Value)}",
            SichuanMahjongActionKind.Pass => "跳过",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var tiles = action.ConcealedTiles.Count > 0
            ? action.ConcealedTiles
            : action.Tile is { } tile ? [tile] : [];
        return new MahjongActionOption(id, kind, label, action.Tile, tiles, action.Suit);
    }

    private static IGameCommand CreateCommand(MahjongSeat seat, SichuanMahjongAction action)
    {
        return action.Kind switch
        {
            SichuanMahjongActionKind.ExchangeThree =>
                new ExchangeThreeTilesCommand((int)seat, action.ConcealedTiles),
            SichuanMahjongActionKind.DeclareVoidSuit =>
                new DeclareVoidSuitCommand((int)seat, action.Suit!.Value),
            SichuanMahjongActionKind.Discard =>
                new DiscardMahjongTileCommand((int)seat, action.Tile!.Value),
            SichuanMahjongActionKind.SelfDrawWin or SichuanMahjongActionKind.DiscardWin =>
                new DeclareMahjongWinCommand((int)seat),
            SichuanMahjongActionKind.Pong or SichuanMahjongActionKind.OpenKong =>
                new ClaimMahjongDiscardCommand((int)seat, action.MeldType!.Value, action.ConcealedTiles),
            SichuanMahjongActionKind.ConcealedKong =>
                new DeclareConcealedKongCommand((int)seat, action.ConcealedTiles),
            SichuanMahjongActionKind.AddedKong =>
                new DeclareAddedKongCommand((int)seat, action.Tile!.Value),
            SichuanMahjongActionKind.Pass => new PassMahjongCommand((int)seat),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static bool CommandsMatch(IGameCommand first, IGameCommand second)
    {
        return (first, second) switch
        {
            (ExchangeThreeTilesCommand left, ExchangeThreeTilesCommand right) =>
                left.PlayerIndex == right.PlayerIndex && SameTiles(left.Tiles, right.Tiles),
            (DeclareVoidSuitCommand left, DeclareVoidSuitCommand right) => left == right,
            (DiscardMahjongTileCommand left, DiscardMahjongTileCommand right) => left == right,
            (DeclareMahjongWinCommand left, DeclareMahjongWinCommand right) => left == right,
            (DeclareAddedKongCommand left, DeclareAddedKongCommand right) => left == right,
            (PassMahjongCommand left, PassMahjongCommand right) => left == right,
            (ClaimMahjongDiscardCommand left, ClaimMahjongDiscardCommand right) =>
                left.PlayerIndex == right.PlayerIndex
                && left.MeldType == right.MeldType
                && SameTiles(left.ConcealedTiles, right.ConcealedTiles),
            (DeclareConcealedKongCommand left, DeclareConcealedKongCommand right) =>
                left.PlayerIndex == right.PlayerIndex && SameTiles(left.Tiles, right.Tiles),
            _ => false,
        };
    }

    private static bool SameTiles<T>(IReadOnlyCollection<T> first, IReadOnlyCollection<T> second)
    {
        return first.Count == second.Count && first.All(second.Contains);
    }

    private static MahjongAnimationEvent MapEvent(IGameEvent @event)
    {
        return @event switch
        {
            SichuanExchangeSubmittedEvent submitted => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Declaration,
                $"{MahjongText.Seat(submitted.Seat)}已选好换三张",
                submitted.Seat),
            SichuanTilesExchangedEvent exchanged => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Exchange,
                $"按{DirectionText(exchanged.Direction)}完成换三张"),
            SichuanVoidSuitDeclaredEvent declared => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Declaration,
                $"{MahjongText.Seat(declared.Seat)}定缺{MahjongText.Suit(declared.Suit)}",
                declared.Seat),
            SichuanTileDrawnEvent drawn => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Draw,
                $"{MahjongText.Seat(drawn.Seat)}摸牌",
                drawn.Seat,
                drawn.Tile),
            SichuanTileDiscardedEvent discarded => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Discard,
                $"{MahjongText.Seat(discarded.Seat)}打出 {MahjongText.Tile(discarded.RiverTile.Tile)}",
                discarded.Seat,
                discarded.RiverTile.Tile),
            SichuanMeldDeclaredEvent meld => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Meld,
                $"{MahjongText.Seat(meld.Seat)}{MahjongText.Meld(meld.Meld.Type)}",
                meld.Seat,
                meld: MahjongPresentationBuilder.FromMeld(meld.Meld)),
            SichuanReactionPassedEvent passed => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Pass,
                $"{MahjongText.Seat(passed.Seat)}跳过",
                passed.Seat),
            SichuanWinSettledEvent win => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Win,
                $"{MahjongText.Seat(win.Win.Winner)}和牌退出，{win.Win.Fan}番",
                win.Win.Winner),
            SichuanMahjongFinishedEvent finished => new MahjongAnimationEvent(
                MahjongAnimationEventKind.MatchFinished,
                finished.Settlement.IsExhaustiveDraw ? "牌墙摸完，完成查花猪与查大叫" : "血战结束"),
            _ => throw new InvalidOperationException($"Unknown Sichuan Mahjong event {@event.GetType().Name}."),
        };
    }

    private static IReadOnlyList<string> CreateSettlementLines(SichuanMahjongSnapshot snapshot)
    {
        if (snapshot.Settlement is null)
        {
            return [];
        }

        var lines = snapshot.Settlement.Wins
            .Select(win => $"{MahjongText.Seat(win.Winner)}：{string.Join("、", win.Patterns)}（{win.Fan} 番）")
            .ToList();
        if (snapshot.Settlement.FlowerPigSeats.Count > 0)
        {
            lines.Add($"查花猪：{string.Join("、", snapshot.Settlement.FlowerPigSeats.Select(MahjongText.Seat))}");
        }

        if (snapshot.Settlement.TenpaiSeats.Count > 0)
        {
            lines.Add($"查大叫听牌：{string.Join("、", snapshot.Settlement.TenpaiSeats.Select(MahjongText.Seat))}");
        }

        lines.Add($"总分：{string.Join(" / ", snapshot.Settlement.ScoreChanges.Select(FormatScore))}");
        return lines;
    }

    private static string DirectionText(SichuanExchangeDirection direction)
    {
        return direction switch
        {
            SichuanExchangeDirection.Clockwise => "顺时针",
            SichuanExchangeDirection.Opposite => "对家",
            SichuanExchangeDirection.CounterClockwise => "逆时针",
            _ => direction.ToString(),
        };
    }

    private static string PhaseText(SichuanMahjongPhase phase)
    {
        return phase switch
        {
            SichuanMahjongPhase.ExchangeThree => "换三张",
            SichuanMahjongPhase.DeclareVoidSuit => "定缺",
            SichuanMahjongPhase.AwaitingDiscard => "血战摸打",
            SichuanMahjongPhase.AwaitingReaction => "合法响应",
            SichuanMahjongPhase.Finished => "最终结算",
            _ => phase.ToString(),
        };
    }

    private static string FormatScore(long value) => value >= 0 ? $"+{value}" : value.ToString();

    private sealed record MappedAction(MahjongActionOption Option, IGameCommand Command);
}
