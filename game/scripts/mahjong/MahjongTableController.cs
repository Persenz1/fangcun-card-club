using Game.Application.Mahjong;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;
using Godot;

namespace FangcunCardClub.Game.Mahjong;

public partial class MahjongTableController : Control
{
    private const double DefaultAutomaticTurnDelaySeconds =
        MahjongAnimationTiming.AiThinkMilliseconds / 1000.0;

    private static long _lastIssuedSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private HBoxContainer _actionOptions = null!;
    private bool _automaticLoopActive;
    private double _automaticTurnDelaySeconds = DefaultAutomaticTurnDelaySeconds;
    private bool _autoEnabled;
    private Button _autoButton = null!;
    private Action? _backRequested;
    private MahjongBoard3D _board = null!;
    private Label _centerInfo = null!;
    private Button _discardButton = null!;
    private Label _leftSeatInfo = null!;
    private bool _fastAnimations;
    private Button _hintButton = null!;
    private bool _initialized;
    private bool _inputLocked;
    private int _lifecycleVersion;
    private MahjongMode _mode;
    private Label _playerSeatInfo = null!;
    private Control _resultOverlay = null!;
    private Label _resultDetails = null!;
    private Label _resultTitle = null!;
    private Label _rightSeatInfo = null!;
    private Label _roundInfo = null!;
    private readonly HashSet<MahjongTile> _selectedTiles = [];
    private IMahjongGameSession _session = null!;
    private Label _statusLabel = null!;
    private string _statusMessage = string.Empty;
    private Control _tableGuide = null!;
    private Label _topSeatInfo = null!;

    public override void _Ready()
    {
        _board = GetNode<MahjongBoard3D>("%MahjongBoard3D");
        _tableGuide = GetNode<Control>("%TableGuide");
        _roundInfo = GetNode<Label>("%RoundInfo");
        _topSeatInfo = GetNode<Label>("%TopSeatInfo");
        _leftSeatInfo = GetNode<Label>("%LeftSeatInfo");
        _rightSeatInfo = GetNode<Label>("%RightSeatInfo");
        _playerSeatInfo = GetNode<Label>("%PlayerSeatInfo");
        _centerInfo = GetNode<Label>("%CenterInfo");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _actionOptions = GetNode<HBoxContainer>("%ActionOptions");
        _discardButton = GetNode<Button>("%DiscardButton");
        _hintButton = GetNode<Button>("%HintButton");
        _autoButton = GetNode<Button>("%AutoButton");
        _resultOverlay = GetNode<Control>("%ResultOverlay");
        _resultTitle = GetNode<Label>("%ResultTitle");
        _resultDetails = GetNode<Label>("%ResultDetails");

        _board.PlayerSelectionChanged += OnPlayerSelectionChanged;
        GetNode<Button>("%BackButton").Pressed += ReturnToLobby;
        GetNode<Button>("%ResultLobbyButton").Pressed += ReturnToLobby;
        GetNode<Button>("%RematchButton").Pressed += StartNewMatch;
        _discardButton.Pressed += SubmitSelectedTiles;
        _hintButton.Pressed += ShowHint;
        _autoButton.Pressed += ToggleAutoPlay;

        var guideButton = GetNode<Button>("%GuideButton");
        guideButton.Pressed += () =>
        {
            _tableGuide.Visible = !_tableGuide.Visible;
            guideButton.Text = _tableGuide.Visible ? "标线：开" : "标线：关";
        };
    }

    public override void _ExitTree()
    {
        _lifecycleVersion++;
    }

    public void Initialize(
        MahjongMode mode,
        Action backRequested,
        bool startWithAutoPlay = false,
        double automaticTurnDelaySeconds = DefaultAutomaticTurnDelaySeconds,
        ulong? initialSeed = null)
    {
        ArgumentNullException.ThrowIfNull(backRequested);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(automaticTurnDelaySeconds);
        _mode = mode;
        _backRequested = backRequested;
        _autoEnabled = startWithAutoPlay;
        _automaticTurnDelaySeconds = automaticTurnDelaySeconds;
        _fastAnimations = automaticTurnDelaySeconds <= 0.02;
        _initialized = true;
        CreateFreshSession(initialSeed);
        RefreshTable();
        ContinueAutomaticTurns();
    }

    private void CreateFreshSession(ulong? seedOverride = null)
    {
        var seed = seedOverride ?? unchecked((ulong)Interlocked.Increment(ref _lastIssuedSeed));
        _session = MahjongSessionFactory.Start(_mode, seed, MahjongSeat.East);
        _selectedTiles.Clear();
        _board.ClearSelection();
        _statusMessage = $"{ModeText(_mode)}已开局。";
    }

    private void StartNewMatch()
    {
        if (!_initialized)
        {
            return;
        }

        _lifecycleVersion++;
        _inputLocked = false;
        _autoEnabled = false;
        CreateFreshSession();
        RefreshTable();
        ContinueAutomaticTurns();
    }

    private void ReturnToLobby()
    {
        _lifecycleVersion++;
        _backRequested?.Invoke();
    }

    private void ToggleAutoPlay()
    {
        if (!_initialized || _session.Snapshot.IsFinished)
        {
            return;
        }

        _autoEnabled = !_autoEnabled;
        _statusMessage = _autoEnabled ? "托管已开启。" : "托管已关闭，将在当前操作后停下。";
        RefreshTable();
        if (_autoEnabled)
        {
            ContinueAutomaticTurns();
        }
    }

    private void ShowHint()
    {
        if (!_initialized || _inputLocked || _session.Snapshot.SuggestedActionId is not { } actionId)
        {
            return;
        }

        var action = _session.Snapshot.LegalActions.Single(option => option.Id == actionId);
        _board.SelectTiles(action.Tiles);
        _statusMessage = $"提示：{action.Label}";
        _statusLabel.Text = _statusMessage;
        foreach (var child in _actionOptions.GetChildren().OfType<Button>())
        {
            if (child.GetMeta("action_id").AsInt32() == actionId)
            {
                child.GrabFocus();
                break;
            }
        }
    }

    private void SubmitSelectedTiles()
    {
        var action = FindSelectedAction();
        if (action is null)
        {
            _statusMessage = _selectedTiles.Count == 0
                ? "请先选择手牌。"
                : "当前选牌不是规则引擎提供的合法操作。";
            _statusLabel.Text = _statusMessage;
            return;
        }

        SubmitAction(action.Id);
    }

    private async void SubmitAction(int actionId)
    {
        if (!_initialized || _inputLocked || _autoEnabled)
        {
            return;
        }

        _inputLocked = true;
        RefreshTable();
        var result = _session.Dispatch(actionId);
        if (!result.Accepted)
        {
            _statusMessage = result.Error ?? "该操作当前不可用。";
            _inputLocked = false;
            RefreshTable();
            return;
        }

        _selectedTiles.Clear();
        _board.ClearSelection();
        await PlayEvents(result.Events, _lifecycleVersion);
        _inputLocked = false;
        RefreshTable();
        HandleFinished();
        ContinueAutomaticTurns();
    }

    private async void ContinueAutomaticTurns()
    {
        if (!_initialized || _automaticLoopActive)
        {
            return;
        }

        _automaticLoopActive = true;
        var lifecycleVersion = _lifecycleVersion;
        try
        {
            while (lifecycleVersion == _lifecycleVersion && IsInsideTree())
            {
                var view = _session.Snapshot;
                if (view.IsFinished || view.IsHumanActionRequired && !_autoEnabled)
                {
                    break;
                }

                if (!view.CanAdvanceAi && !(view.IsHumanActionRequired && _autoEnabled))
                {
                    break;
                }

                _inputLocked = true;
                RefreshTable();
                await ToSignal(
                    GetTree().CreateTimer(_automaticTurnDelaySeconds),
                    SceneTreeTimer.SignalName.Timeout);
                if (lifecycleVersion != _lifecycleVersion || !IsInsideTree())
                {
                    return;
                }

                view = _session.Snapshot;
                if (view.IsHumanActionRequired && !_autoEnabled)
                {
                    break;
                }

                var result = view.IsHumanActionRequired
                    ? _session.DispatchSuggestedAction()
                    : _session.AdvanceAiTurn();
                if (!result.Accepted)
                {
                    _statusMessage = result.Error ?? "自动操作未被接受。";
                    break;
                }

                await PlayEvents(result.Events, lifecycleVersion);
                RefreshTable();
                if (_session.Snapshot.IsFinished)
                {
                    HandleFinished();
                    break;
                }
            }
        }
        finally
        {
            _automaticLoopActive = false;
            _inputLocked = false;
            if (lifecycleVersion == _lifecycleVersion && IsInsideTree())
            {
                RefreshTable();
            }
        }
    }

    private async Task PlayEvents(
        IReadOnlyList<MahjongAnimationEvent> events,
        int lifecycleVersion)
    {
        foreach (var animationEvent in events)
        {
            if (lifecycleVersion != _lifecycleVersion || !IsInsideTree())
            {
                return;
            }

            _statusMessage = animationEvent.Message;
            RefreshTable(animationEvent);
            var duration = _fastAnimations
                ? 0.001
                : animationEvent.DurationMilliseconds / 1000.0;
            await ToSignal(
                GetTree().CreateTimer(duration),
                SceneTreeTimer.SignalName.Timeout);
        }
    }

    private void RefreshTable(MahjongAnimationEvent? cue = null)
    {
        if (!_initialized)
        {
            return;
        }

        var view = _session.Snapshot;
        _board.Render(view, cue);
        _roundInfo.Text = string.Join("   ", view.HudItems.Take(4).Select(item => $"{item.Label} {item.Value}"));
        _topSeatInfo.Text = FormatSeat(view, SeatAtDistance(view.HumanSeat, 2));
        _leftSeatInfo.Text = FormatSeat(view, SeatAtDistance(view.HumanSeat, 3));
        _rightSeatInfo.Text = FormatSeat(view, SeatAtDistance(view.HumanSeat, 1));
        _playerSeatInfo.Text = FormatSeat(view, view.HumanSeat);
        _centerInfo.Text = $"{view.Phase}\n{MahjongText.Seat(view.Table.CurrentSeat)}\n余 {view.Table.LiveTilesRemaining}";
        _statusLabel.Text = string.IsNullOrWhiteSpace(_statusMessage)
            ? view.Prompt
            : _statusMessage;
        _autoButton.Text = _autoEnabled ? "托管：开" : "托管：关";
        _autoButton.Disabled = view.IsFinished;
        _hintButton.Disabled = _inputLocked
            || _autoEnabled
            || view.SuggestedActionId is null;
        RebuildActionOptions(view);
        RefreshResult(view);
    }

    private void RebuildActionOptions(MahjongSessionView view)
    {
        foreach (var child in _actionOptions.GetChildren())
        {
            _actionOptions.RemoveChild(child);
            child.QueueFree();
        }

        var canAct = view.IsHumanActionRequired && !_inputLocked && !_autoEnabled;
        foreach (var action in view.LegalActions.Where(action => action.Kind is not (
                     MahjongActionViewKind.Discard or MahjongActionViewKind.ExchangeThree)))
        {
            var button = new Button
            {
                Text = action.Label,
                TooltipText = action.Label,
                CustomMinimumSize = new Vector2(Math.Max(74, 24 + (action.Label.Length * 15)), 42),
                Disabled = !canAct,
            };
            button.SetMeta("action_id", action.Id);
            var actionId = action.Id;
            button.Pressed += () => SubmitAction(actionId);
            _actionOptions.AddChild(button);
        }

        var selectionKinds = view.LegalActions
            .Where(action => action.Kind is MahjongActionViewKind.Discard or MahjongActionViewKind.ExchangeThree)
            .Select(action => action.Kind)
            .Distinct()
            .ToArray();
        _discardButton.Visible = selectionKinds.Length > 0;
        _discardButton.Text = selectionKinds.Contains(MahjongActionViewKind.ExchangeThree)
            ? "换三张"
            : "出牌";
        _discardButton.Disabled = !canAct || FindSelectedAction() is null;
    }

    private void RefreshResult(MahjongSessionView view)
    {
        _resultOverlay.Visible = view.IsFinished;
        if (!view.IsFinished)
        {
            return;
        }

        _resultTitle.Text = _mode == MahjongMode.Riichi ? "整场结果" : "本局结算";
        _resultDetails.Text = view.SettlementLines.Count == 0
            ? "本局已结束"
            : string.Join("\n", view.SettlementLines);
    }

    private void OnPlayerSelectionChanged(IReadOnlyList<MahjongTile> tiles)
    {
        _selectedTiles.Clear();
        _selectedTiles.UnionWith(tiles);
        if (!_initialized)
        {
            return;
        }

        var action = FindSelectedAction();
        _statusMessage = tiles.Count == 0
            ? _session.Snapshot.Prompt
            : action is null
                ? $"已选 {tiles.Count} 张，当前组合不是合法操作"
                : $"已选：{action.Label}";
        _statusLabel.Text = _statusMessage;
        _discardButton.Disabled = _inputLocked || _autoEnabled || action is null;
    }

    private MahjongActionOption? FindSelectedAction()
    {
        if (!_initialized || _selectedTiles.Count == 0)
        {
            return null;
        }

        return _session.Snapshot.LegalActions.FirstOrDefault(action =>
            action.Kind is MahjongActionViewKind.Discard or MahjongActionViewKind.ExchangeThree
            && action.Tiles.Count == _selectedTiles.Count
            && action.Tiles.All(_selectedTiles.Contains));
    }

    private void HandleFinished()
    {
        if (!_session.Snapshot.IsFinished)
        {
            return;
        }

        GD.Print($"麻将对局完成：{ModeText(_mode)}，种子 {_session.Seed}。");
        if (OS.GetCmdlineUserArgs().Contains("--quit-on-finish", StringComparer.Ordinal))
        {
            GetTree().Quit();
        }
    }

    private static string FormatSeat(MahjongSessionView view, MahjongSeat seat)
    {
        var seatView = view.Table.Seats[(int)seat];
        var acting = seat == (view.Table.OfferedReactionSeat ?? view.Table.CurrentSeat)
            && !view.IsFinished
            ? "▶ "
            : string.Empty;
        var status = seatView.Status.Count == 0 ? string.Empty : $"\n{string.Join(" ", seatView.Status)}";
        return $"{acting}{(seat == view.HumanSeat ? "你" : seatView.Name)}\n"
            + $"{seatView.Hand.Count} 张  {FormatScore(seatView.Score)}{status}";
    }

    private static MahjongSeat SeatAtDistance(MahjongSeat origin, int distance)
    {
        return (MahjongSeat)(((int)origin + distance) % 4);
    }

    private static string FormatScore(long score)
    {
        return score >= 0 ? $"+{score}" : score.ToString();
    }

    private static string ModeText(MahjongMode mode)
    {
        return mode switch
        {
            MahjongMode.Standard => "大众麻将",
            MahjongMode.Sichuan => "四川血战",
            MahjongMode.Riichi => "四人日麻",
            _ => mode.ToString(),
        };
    }
}
