using Game.Core.Random;
using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.Actions;
using Game.Mahjong.Riichi.AI;
using Game.Mahjong.Riichi.Commands;
using Game.Mahjong.Riichi.Events;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;

namespace Game.Application.Mahjong.Riichi;

public sealed class RiichiMahjongGameSession : IMahjongGameSession
{
    private readonly BasicRiichiMahjongAi _ai = new();
    private readonly RiichiMahjongRuleEngine _engine;

    private RiichiMahjongGameSession(ulong seed, MahjongSeat humanSeat)
    {
        if (!Enum.IsDefined(humanSeat))
        {
            throw new ArgumentOutOfRangeException(nameof(humanSeat));
        }

        Seed = seed;
        HumanSeat = humanSeat;
        _engine = new RiichiMahjongRuleEngine(new SplitMix64Random(seed));
    }

    public event Action? StateChanged;

    public MahjongMode Mode => MahjongMode.Riichi;

    public ulong Seed { get; }

    public MahjongSeat HumanSeat { get; }

    public MahjongSessionView Snapshot => CreateView();

    public RiichiMahjongSnapshot RuleSnapshot => _engine.Snapshot;

    public static RiichiMahjongGameSession Start(
        ulong seed,
        MahjongSeat humanSeat = MahjongSeat.East)
    {
        return new RiichiMahjongGameSession(seed, humanSeat);
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
                var values = new List<string>
                {
                    $"自风{MahjongText.Tile(SelfWind(snapshot, seat))}",
                };
                if (seat == snapshot.Dealer)
                {
                    values.Add("庄家");
                }

                if (snapshot.DoubleRiichiDeclared[(int)seat])
                {
                    values.Add("双立直");
                }
                else if (snapshot.RiichiDeclared[(int)seat])
                {
                    values.Add("立直");
                }

                if (snapshot.FuritenSeats[(int)seat])
                {
                    values.Add("振听");
                }

                if (seat == snapshot.PendingRiichiSeat)
                {
                    values.Add("立直宣言待成立");
                }

                if (seat == snapshot.OfferedReactionSeat)
                {
                    values.Add("等待响应");
                }
                else if (snapshot.Phase == RiichiMahjongPhase.AwaitingDiscard
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
            snapshot.Scores,
            statuses);
        var prompt = snapshot.Phase switch
        {
            RiichiMahjongPhase.Finished => "东风战已结束",
            _ when mappings.Count > 0 => "请从规则引擎给出的合法操作中选择",
            _ when FindAiSeat() is { } seat => $"{MahjongText.Seat(seat)}思考中",
            _ => "等待牌局推进",
        };
        return new MahjongSessionView(
            Mode,
            HumanSeat,
            $"{RoundWindText(snapshot.RoundWind)}{snapshot.HandNumber}局",
            prompt,
            table,
            [
                new MahjongHudItem("牌局", $"{RoundWindText(snapshot.RoundWind)}{snapshot.HandNumber}局"),
                new MahjongHudItem("本场", snapshot.Honba.ToString()),
                new MahjongHudItem("供托", snapshot.RiichiSticks.ToString()),
                new MahjongHudItem("余牌", snapshot.Table.LiveTilesRemaining.ToString()),
                new MahjongHudItem("宝牌指示", string.Join(" ", snapshot.DoraIndicators.Select(MahjongText.Tile))),
            ],
            snapshot.DoraIndicators,
            mappings.Select(mapping => mapping.Option),
            FindSuggestedActionId(snapshot, mappings),
            FindAiSeat() is not null,
            snapshot.Phase == RiichiMahjongPhase.Finished,
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
        RiichiMahjongSnapshot snapshot,
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

    private static MahjongActionOption CreateOption(int id, RiichiMahjongAction action)
    {
        var kind = action.Kind switch
        {
            RiichiMahjongActionKind.Discard => MahjongActionViewKind.Discard,
            RiichiMahjongActionKind.RiichiDiscard => MahjongActionViewKind.RiichiDiscard,
            RiichiMahjongActionKind.SelfDrawWin or RiichiMahjongActionKind.DiscardWin => MahjongActionViewKind.Win,
            RiichiMahjongActionKind.Chow => MahjongActionViewKind.Chow,
            RiichiMahjongActionKind.Pong => MahjongActionViewKind.Pong,
            RiichiMahjongActionKind.OpenKong => MahjongActionViewKind.OpenKong,
            RiichiMahjongActionKind.ConcealedKong => MahjongActionViewKind.ConcealedKong,
            RiichiMahjongActionKind.AddedKong => MahjongActionViewKind.AddedKong,
            RiichiMahjongActionKind.NineTerminalsDraw => MahjongActionViewKind.NineTerminalsDraw,
            RiichiMahjongActionKind.Pass => MahjongActionViewKind.Pass,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var label = action.Kind switch
        {
            RiichiMahjongActionKind.Discard => $"弃牌 {MahjongText.Tile(action.Tile!.Value)}",
            RiichiMahjongActionKind.RiichiDiscard => $"立直打 {MahjongText.Tile(action.Tile!.Value)}",
            RiichiMahjongActionKind.SelfDrawWin => "自摸",
            RiichiMahjongActionKind.DiscardWin => "荣和",
            RiichiMahjongActionKind.Chow => $"吃 {MahjongText.Tiles(action.ConcealedTiles.Append(action.Tile!.Value).OrderBy(tile => tile.Kind))}",
            RiichiMahjongActionKind.Pong => $"碰 {MahjongText.Tile(action.Tile!.Value)}",
            RiichiMahjongActionKind.OpenKong => $"大明杠 {MahjongText.Tile(action.Tile!.Value)}",
            RiichiMahjongActionKind.ConcealedKong => $"暗杠 {MahjongText.Tile(action.ConcealedTiles[0])}",
            RiichiMahjongActionKind.AddedKong => $"加杠 {MahjongText.Tile(action.Tile!.Value)}",
            RiichiMahjongActionKind.NineTerminalsDraw => "九种九牌流局",
            RiichiMahjongActionKind.Pass => "跳过",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        var tiles = action.ConcealedTiles.Count > 0
            ? action.ConcealedTiles
            : action.Tile is { } tile ? [tile] : [];
        return new MahjongActionOption(id, kind, label, action.Tile, tiles);
    }

    private static IGameCommand CreateCommand(MahjongSeat seat, RiichiMahjongAction action)
    {
        return action.Kind switch
        {
            RiichiMahjongActionKind.Discard =>
                new DiscardMahjongTileCommand((int)seat, action.Tile!.Value),
            RiichiMahjongActionKind.RiichiDiscard =>
                new DeclareRiichiCommand((int)seat, action.Tile!.Value),
            RiichiMahjongActionKind.SelfDrawWin or RiichiMahjongActionKind.DiscardWin =>
                new DeclareMahjongWinCommand((int)seat),
            RiichiMahjongActionKind.Chow or RiichiMahjongActionKind.Pong or RiichiMahjongActionKind.OpenKong =>
                new ClaimMahjongDiscardCommand((int)seat, action.MeldType!.Value, action.ConcealedTiles),
            RiichiMahjongActionKind.ConcealedKong =>
                new DeclareConcealedKongCommand((int)seat, action.ConcealedTiles),
            RiichiMahjongActionKind.AddedKong =>
                new DeclareAddedKongCommand((int)seat, action.Tile!.Value),
            RiichiMahjongActionKind.NineTerminalsDraw =>
                new DeclareNineTerminalsDrawCommand((int)seat),
            RiichiMahjongActionKind.Pass => new PassMahjongCommand((int)seat),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static bool CommandsMatch(IGameCommand first, IGameCommand second)
    {
        return (first, second) switch
        {
            (DiscardMahjongTileCommand left, DiscardMahjongTileCommand right) => left == right,
            (DeclareRiichiCommand left, DeclareRiichiCommand right) => left == right,
            (DeclareMahjongWinCommand left, DeclareMahjongWinCommand right) => left == right,
            (DeclareAddedKongCommand left, DeclareAddedKongCommand right) => left == right,
            (DeclareNineTerminalsDrawCommand left, DeclareNineTerminalsDrawCommand right) => left == right,
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
            RiichiHandStartedEvent started => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Declaration,
                $"{RoundWindText(started.RoundWind)}{started.HandNumber}局开始，{MahjongText.Seat(started.Dealer)}坐庄"),
            RiichiTileDrawnEvent drawn => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Draw,
                $"{MahjongText.Seat(drawn.Seat)}摸牌",
                drawn.Seat,
                drawn.Tile),
            RiichiTileDiscardedEvent discarded => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Discard,
                $"{MahjongText.Seat(discarded.Seat)}打出 {MahjongText.Tile(discarded.RiverTile.Tile)}",
                discarded.Seat,
                discarded.RiverTile.Tile),
            RiichiDeclaredEvent declared => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Declaration,
                $"{MahjongText.Seat(declared.Seat)}{(declared.IsDoubleRiichi ? "双立直" : "立直")}",
                declared.Seat),
            RiichiMeldDeclaredEvent meld => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Meld,
                $"{MahjongText.Seat(meld.Seat)}{MahjongText.Meld(meld.Meld.Type)}",
                meld.Seat,
                meld: MahjongPresentationBuilder.FromMeld(meld.Meld)),
            RiichiDoraRevealedEvent dora => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Dora,
                $"新宝牌指示牌 {MahjongText.Tile(dora.Indicator)}"),
            RiichiReactionPassedEvent passed => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Pass,
                $"{MahjongText.Seat(passed.Seat)}跳过{(passed.PassedWin ? "和牌" : string.Empty)}",
                passed.Seat),
            RiichiWinSettledEvent win => new MahjongAnimationEvent(
                MahjongAnimationEventKind.Win,
                $"{MahjongText.Seat(win.Win.Winner)}{(win.Win.SelfDraw ? "自摸" : "荣和")}：{string.Join("、", win.Win.HandScore.Yaku)}",
                win.Win.Winner),
            RiichiHandFinishedEvent finished => new MahjongAnimationEvent(
                MahjongAnimationEventKind.HandFinished,
                $"本局结束：{HandReasonText(finished.Result.Reason)}"),
            RiichiMatchFinishedEvent => new MahjongAnimationEvent(
                MahjongAnimationEventKind.MatchFinished,
                "东风战结束"),
            _ => throw new InvalidOperationException($"Unknown Riichi Mahjong event {@event.GetType().Name}."),
        };
    }

    private static IReadOnlyList<string> CreateSettlementLines(RiichiMahjongSnapshot snapshot)
    {
        if (snapshot.MatchResult is { } match)
        {
            return match.Ranking
                .Select((seat, index) => $"{index + 1}. {MahjongText.Seat(seat)} {match.FinalScores[(int)seat]}")
                .ToArray();
        }

        if (snapshot.LastHandResult is not { } hand)
        {
            return [];
        }

        var lines = new List<string> { HandReasonText(hand.Reason) };
        lines.AddRange(hand.Wins.Select(win =>
            $"{MahjongText.Seat(win.Winner)}：{string.Join("、", win.HandScore.Yaku)} "
            + $"{win.HandScore.Han}番{win.HandScore.Fu}符 {win.HandScore.LimitName}"));
        if (hand.TenpaiSeats.Count > 0)
        {
            lines.Add($"听牌：{string.Join("、", hand.TenpaiSeats.Select(MahjongText.Seat))}");
        }

        lines.Add($"局分：{string.Join(" / ", hand.ScoreChanges.Select(FormatScore))}");
        return lines;
    }

    private static Game.Mahjong.Tiles.MahjongTileKind SelfWind(
        RiichiMahjongSnapshot snapshot,
        MahjongSeat seat)
    {
        return (Game.Mahjong.Tiles.MahjongTileKind)(
            (int)Game.Mahjong.Tiles.MahjongTileKind.East + seat.DistanceFrom(snapshot.Dealer));
    }

    private static string HandReasonText(RiichiHandEndReason reason)
    {
        return reason switch
        {
            RiichiHandEndReason.Ron => "荣和",
            RiichiHandEndReason.Tsumo => "自摸",
            RiichiHandEndReason.ExhaustiveDraw => "荒牌流局",
            RiichiHandEndReason.NagashiMangan => "流局满贯",
            RiichiHandEndReason.NineTerminals => "九种九牌",
            RiichiHandEndReason.FourWinds => "四风连打",
            RiichiHandEndReason.FourRiichi => "四家立直",
            RiichiHandEndReason.FourKongs => "四杠散了",
            _ => reason.ToString(),
        };
    }

    private static string RoundWindText(RiichiRoundWind wind)
    {
        return wind == RiichiRoundWind.East ? "东" : "南";
    }

    private static string FormatScore(long value) => value >= 0 ? $"+{value}" : value.ToString();

    private sealed record MappedAction(MahjongActionOption Option, IGameCommand Command);
}
