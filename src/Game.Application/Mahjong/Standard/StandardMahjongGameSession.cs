using Game.Core.Random;
using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Standard.Actions;
using Game.Mahjong.Standard.AI;
using Game.Mahjong.Standard.Events;
using Game.Mahjong.Standard.State;
using Game.Mahjong.Table;

namespace Game.Application.Mahjong.Standard;

public sealed class StandardMahjongGameSession : IMahjongGameSession
{
    private readonly BasicStandardMahjongAi _ai = new();
    private readonly StandardMahjongRuleEngine _engine;

    private StandardMahjongGameSession(ulong seed, MahjongSeat humanSeat, int baseScore)
    {
        if (!Enum.IsDefined(humanSeat))
        {
            throw new ArgumentOutOfRangeException(nameof(humanSeat));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);
        Seed = seed;
        HumanSeat = humanSeat;
        _engine = new StandardMahjongRuleEngine(new SplitMix64Random(seed), baseScore: baseScore);
    }

    public event Action? StateChanged;

    public MahjongMode Mode => MahjongMode.Standard;

    public ulong Seed { get; }

    public MahjongSeat HumanSeat { get; }

    public MahjongSessionView Snapshot => CreateView();

    public StandardMahjongSnapshot RuleSnapshot => _engine.Snapshot;

    public static StandardMahjongGameSession Start(
        ulong seed,
        MahjongSeat humanSeat = MahjongSeat.East,
        int baseScore = 10)
    {
        return new StandardMahjongGameSession(seed, humanSeat, baseScore);
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
        var suggestedId = FindSuggestedActionId(snapshot, mappings);
        var statuses = Enum.GetValues<MahjongSeat>()
            .Select(seat =>
            {
                var values = new List<string>();
                if (seat == snapshot.Table.Dealer)
                {
                    values.Add("庄家");
                }

                if (seat == snapshot.OfferedReactionSeat)
                {
                    values.Add("等待响应");
                }
                else if (snapshot.Phase == StandardMahjongPhase.AwaitingDiscard
                    && seat == snapshot.Table.CurrentSeat)
                {
                    values.Add("行动中");
                }

                return (IReadOnlyList<string>)values;
            })
            .ToArray();
        var scores = snapshot.Settlement?.ScoreChanges ?? new long[4];
        var table = MahjongPresentationBuilder.CreateTable(
            snapshot.Table,
            HumanSeat,
            snapshot.OfferedReactionSeat,
            scores,
            statuses);
        var prompt = snapshot.Phase switch
        {
            StandardMahjongPhase.Finished => "本局已结束",
            _ when mappings.Count > 0 => "请从规则引擎给出的合法操作中选择",
            _ when FindAiSeat() is { } seat => $"{MahjongText.Seat(seat)}思考中",
            _ => "等待牌局推进",
        };
        return new MahjongSessionView(
            Mode,
            HumanSeat,
            PhaseText(snapshot.Phase),
            prompt,
            table,
            [
                new MahjongHudItem("玩法", "大众麻将"),
                new MahjongHudItem("余牌", snapshot.Table.LiveTilesRemaining.ToString()),
                new MahjongHudItem("当前", MahjongText.Seat(snapshot.Table.CurrentSeat)),
            ],
            [],
            mappings.Select(mapping => mapping.Option),
            suggestedId,
            FindAiSeat() is not null,
            snapshot.Phase == StandardMahjongPhase.Finished,
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
        StandardMahjongSnapshot snapshot,
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
        return Enum.GetValues<MahjongSeat>()
            .Cast<MahjongSeat?>()
            .FirstOrDefault(seat => seat != HumanSeat
                && _engine.GetLegalActions(seat!.Value).Count > 0);
    }

    private static MahjongActionOption CreateOption(int id, StandardMahjongAction action)
    {
        var kind = action.Kind switch
        {
            StandardMahjongActionKind.Discard => MahjongActionViewKind.Discard,
            StandardMahjongActionKind.SelfDrawWin or StandardMahjongActionKind.DiscardWin => MahjongActionViewKind.Win,
            StandardMahjongActionKind.Chow => MahjongActionViewKind.Chow,
            StandardMahjongActionKind.Pong => MahjongActionViewKind.Pong,
            StandardMahjongActionKind.OpenKong => MahjongActionViewKind.OpenKong,
            StandardMahjongActionKind.ConcealedKong => MahjongActionViewKind.ConcealedKong,
            StandardMahjongActionKind.AddedKong => MahjongActionViewKind.AddedKong,
            StandardMahjongActionKind.Pass => MahjongActionViewKind.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var label = action.Kind switch
        {
            StandardMahjongActionKind.Discard => $"弃牌 {MahjongText.Tile(action.Tile!.Value)}",
            StandardMahjongActionKind.SelfDrawWin => "自摸胡",
            StandardMahjongActionKind.DiscardWin => "点炮胡",
            StandardMahjongActionKind.Chow => $"吃 {MahjongText.Tiles(action.ConcealedTiles.Append(action.Tile!.Value).OrderBy(tile => tile.Kind))}",
            StandardMahjongActionKind.Pong => $"碰 {MahjongText.Tile(action.Tile!.Value)}",
            StandardMahjongActionKind.OpenKong => $"明杠 {MahjongText.Tile(action.Tile!.Value)}",
            StandardMahjongActionKind.ConcealedKong => $"暗杠 {MahjongText.Tile(action.ConcealedTiles[0])}",
            StandardMahjongActionKind.AddedKong => $"加杠 {MahjongText.Tile(action.Tile!.Value)}",
            StandardMahjongActionKind.Pass => "跳过",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var tiles = action.ConcealedTiles.Count > 0
            ? action.ConcealedTiles
            : action.Tile is { } tile ? [tile] : [];
        return new MahjongActionOption(id, kind, label, action.Tile, tiles);
    }

    private static IGameCommand CreateCommand(MahjongSeat seat, StandardMahjongAction action)
    {
        return action.Kind switch
        {
            StandardMahjongActionKind.Discard => new DiscardMahjongTileCommand((int)seat, action.Tile!.Value),
            StandardMahjongActionKind.SelfDrawWin or StandardMahjongActionKind.DiscardWin =>
                new DeclareMahjongWinCommand((int)seat),
            StandardMahjongActionKind.Chow or StandardMahjongActionKind.Pong or StandardMahjongActionKind.OpenKong =>
                new ClaimMahjongDiscardCommand((int)seat, action.MeldType!.Value, action.ConcealedTiles),
            StandardMahjongActionKind.ConcealedKong =>
                new DeclareConcealedKongCommand((int)seat, action.ConcealedTiles),
            StandardMahjongActionKind.AddedKong =>
                new DeclareAddedKongCommand((int)seat, action.Tile!.Value),
            StandardMahjongActionKind.Pass => new PassMahjongCommand((int)seat),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static bool CommandsMatch(IGameCommand first, IGameCommand second)
    {
        return (first, second) switch
        {
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
            StandardTileDrawnEvent drawn => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Draw,
                $"{MahjongText.Seat(drawn.Seat)}摸牌",
                drawn.Seat,
                drawn.Tile),
            StandardTileDiscardedEvent discarded => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Discard,
                $"{MahjongText.Seat(discarded.Seat)}打出 {MahjongText.Tile(discarded.RiverTile.Tile)}",
                discarded.Seat,
                discarded.RiverTile.Tile),
            StandardMeldDeclaredEvent meld => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Meld,
                $"{MahjongText.Seat(meld.Seat)}{MahjongText.Meld(meld.Meld.Type)}",
                meld.Seat,
                meld: MahjongPresentationBuilder.FromMeld(meld.Meld)),
            StandardReactionPassedEvent passed => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Pass,
                $"{MahjongText.Seat(passed.Seat)}跳过",
                passed.Seat),
            StandardMahjongFinishedEvent finished when finished.Settlement.IsDraw =>
                new MahjongAnimationEvent(MahjongAnimationEventKind.HandFinished, "牌墙摸完，本局流局"),
            StandardMahjongFinishedEvent finished => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Win,
                $"{MahjongText.Seat(finished.Settlement.Winner!.Value)}和牌，{finished.Settlement.Fan}番",
                finished.Settlement.Winner),
            _ => throw new InvalidOperationException($"Unknown standard Mahjong event {@event.GetType().Name}."),
        };
    }

    private static IReadOnlyList<string> CreateSettlementLines(StandardMahjongSnapshot snapshot)
    {
        if (snapshot.Settlement is null)
        {
            return [];
        }

        if (snapshot.Settlement.IsDraw)
        {
            return ["流局：四家分数不变"];
        }

        return
        [
            $"胜者：{MahjongText.Seat(snapshot.Settlement.Winner!.Value)}",
            $"番型：{string.Join("、", snapshot.Settlement.Patterns)}（{snapshot.Settlement.Fan} 番）",
            $"分数：{string.Join(" / ", snapshot.Settlement.ScoreChanges.Select(FormatScore))}",
        ];
    }

    private static string PhaseText(StandardMahjongPhase phase)
    {
        return phase switch
        {
            StandardMahjongPhase.AwaitingDiscard => "摸打",
            StandardMahjongPhase.AwaitingReaction => "合法响应",
            StandardMahjongPhase.Finished => "结算",
            _ => phase.ToString(),
        };
    }

    private static string FormatScore(long value) => value >= 0 ? $"+{value}" : value.ToString();

    private sealed record MappedAction(MahjongActionOption Option, IGameCommand Command);
}
