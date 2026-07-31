using Game.Application.Sessions;
using Game.Core.Random;
using Game.Core.Simulation;
using Game.Doudizhu.AI;
using Game.Doudizhu.State;

namespace Game.Application.Doudizhu;

public sealed class DoudizhuGameSession : IGameSession<DoudizhuSessionView>
{
    private readonly BasicDoudizhuAi _ai = new();
    private readonly List<DoudizhuCommandRecord> _acceptedCommands = [];
    private readonly int _baseScore;
    private readonly DoudizhuRuleEngine _engine;
    private readonly ulong _seed;

    private DoudizhuGameSession(ulong seed, int humanPlayerIndex, int baseScore)
    {
        if (humanPlayerIndex is < 0 or >= DoudizhuRuleEngine.PlayerCount)
        {
            throw new ArgumentOutOfRangeException(nameof(humanPlayerIndex));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);

        _seed = seed;
        _baseScore = baseScore;
        HumanPlayerIndex = humanPlayerIndex;
        _engine = new DoudizhuRuleEngine(
            new SplitMix64Random(seed),
            new DoudizhuRuleConfig { BaseScore = baseScore });
    }

    public int HumanPlayerIndex { get; }

    public DoudizhuSessionView Snapshot => CreateView();

    public DoudizhuSnapshot RuleSnapshot => _engine.Snapshot;

    public static DoudizhuGameSession Start(ulong seed, int humanPlayerIndex = 0, int baseScore = 10)
    {
        return new DoudizhuGameSession(seed, humanPlayerIndex, baseScore);
    }

    public static DoudizhuGameSession Restore(DoudizhuRecoveryState recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        recovery.Validate();

        var session = new DoudizhuGameSession(
            recovery.Seed,
            recovery.HumanPlayerIndex,
            recovery.BaseScore);
        foreach (var record in recovery.AcceptedCommands)
        {
            var command = record.ToCommand();
            var result = session._engine.Dispatch(command);
            if (!result.Accepted)
            {
                throw new InvalidDataException($"斗地主恢复命令无法重放：{result.Error}");
            }

            session._acceptedCommands.Add(DoudizhuCommandRecord.FromCommand(command));
        }

        return session;
    }

    public CommandResult<DoudizhuSessionView> Dispatch(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PlayerIndex != HumanPlayerIndex)
        {
            return new CommandResult<DoudizhuSessionView>(
                false,
                CreateView(),
                [],
                "玩家输入只能控制本地座位。");
        }

        return DispatchAndRecord(command);
    }

    public CommandResult<DoudizhuSessionView> AdvanceAiTurn()
    {
        var ruleSnapshot = _engine.Snapshot;
        if (ruleSnapshot.Phase is not (DoudizhuPhase.Bidding or DoudizhuPhase.Playing)
            || ruleSnapshot.CurrentPlayerIndex == HumanPlayerIndex)
        {
            return new CommandResult<DoudizhuSessionView>(
                false,
                CreateView(),
                [],
                "当前没有等待执行的 AI 回合。");
        }

        var playerIndex = ruleSnapshot.CurrentPlayerIndex;
        var command = _ai.ChooseCommand(
            _engine.GetObservation(playerIndex),
            _engine.GetLegalMoves(playerIndex));
        return DispatchAndRecord(command);
    }

    public DoudizhuRecoveryState CreateRecoveryState()
    {
        return new DoudizhuRecoveryState
        {
            Seed = _seed,
            BaseScore = _baseScore,
            HumanPlayerIndex = HumanPlayerIndex,
            AcceptedCommands = _acceptedCommands
                .Select(record => DoudizhuCommandRecord.FromCommand(record.ToCommand()))
                .ToList(),
        };
    }

    private CommandResult<DoudizhuSessionView> DispatchAndRecord(IGameCommand command)
    {
        var result = _engine.Dispatch(command);
        if (result.Accepted)
        {
            _acceptedCommands.Add(DoudizhuCommandRecord.FromCommand(command));
        }

        return new CommandResult<DoudizhuSessionView>(
            result.Accepted,
            CreateView(),
            result.Events,
            result.Error);
    }

    private DoudizhuSessionView CreateView()
    {
        var ruleSnapshot = _engine.Snapshot;
        var phaseAcceptsCommands = ruleSnapshot.Phase is DoudizhuPhase.Bidding or DoudizhuPhase.Playing;
        var isHumanTurn = phaseAcceptsCommands && ruleSnapshot.CurrentPlayerIndex == HumanPlayerIndex;
        var legalMoves = isHumanTurn
            ? _engine.GetLegalMoves(HumanPlayerIndex)
            : [];

        return new DoudizhuSessionView(
            _engine.GetObservation(HumanPlayerIndex),
            legalMoves,
            isHumanTurn);
    }
}
