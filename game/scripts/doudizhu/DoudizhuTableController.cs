using Game.Application.Doudizhu;
using Game.Application.Profiles;
using Game.Application.Sessions;
using Game.Core.Simulation;
using Game.Doudizhu.AI;
using Game.Doudizhu.Cards;
using Game.Doudizhu.Commands;
using Game.Doudizhu.Events;
using Game.Doudizhu.Moves;
using Game.Doudizhu.Patterns;
using Game.Doudizhu.Settlement;
using Game.Doudizhu.State;
using Godot;

namespace FangcunCardClub.Game.Doudizhu;

public partial class DoudizhuTableController : Control
{
    private const int HumanPlayerIndex = 0;
    private const int BaseScore = 10;
    private const double DefaultAutomaticTurnDelaySeconds = 0.42;

    private static long _lastIssuedSeed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private readonly BasicDoudizhuAi _assistantAi = new();
    private readonly Dictionary<Card, Button> _cardButtons = [];
    private readonly Color _pieceBackground = new("f1e5cc");
    private readonly Color _pieceBorder = new("6d6257");
    private readonly Color _pieceHover = new("fff5dc");
    private readonly Color _pieceSelected = new("4fc8ad");
    private readonly HashSet<Card> _selectedCards = [];

    private HBoxContainer _actionBar = null!;
    private double _automaticTurnDelaySeconds = DefaultAutomaticTurnDelaySeconds;
    private Button _autoButton = null!;
    private Action? _backRequested;
    private HBoxContainer _bidActionBar = null!;
    private Button _bidPassButton = null!;
    private Label _bottomCardsLabel = null!;
    private Button _callButton = null!;
    private Button _hintButton = null!;
    private bool _automaticLoopActive;
    private bool _autoEnabled;
    private bool _inputLocked;
    private bool _initialized;
    private Label _lastPlayInfo = null!;
    private Label _leftSeatInfo = null!;
    private int _lifecycleVersion;
    private LocalPlayerProfile _profile = null!;
    private Label _playerSeatInfo = null!;
    private HBoxContainer _playerHand = null!;
    private Button _playButton = null!;
    private Control _resultOverlay = null!;
    private Label _resultDetails = null!;
    private Button _rematchButton = null!;
    private Label _resultTitle = null!;
    private Label _rightSeatInfo = null!;
    private Label _roundInfo = null!;
    private Action? _saveProfile;
    private DoudizhuGameSession _session = null!;
    private Label _statusLabel = null!;
    private string _statusMessage = string.Empty;
    private bool _synchronizingSelection;
    private Button _passButton = null!;

    public override void _Ready()
    {
        _roundInfo = GetNode<Label>("%RoundInfo");
        _leftSeatInfo = GetNode<Label>("%LeftSeatInfo");
        _rightSeatInfo = GetNode<Label>("%RightSeatInfo");
        _playerSeatInfo = GetNode<Label>("%PlayerSeatInfo");
        _bottomCardsLabel = GetNode<Label>("%BottomCardsLabel");
        _lastPlayInfo = GetNode<Label>("%LastPlayInfo");
        _playerHand = GetNode<HBoxContainer>("%PlayerHand");
        _statusLabel = GetNode<Label>("%StatusLabel");
        _actionBar = GetNode<HBoxContainer>("%ActionBar");
        _bidActionBar = GetNode<HBoxContainer>("%BidActionBar");
        _autoButton = GetNode<Button>("%AutoButton");
        _hintButton = GetNode<Button>("%HintButton");
        _passButton = GetNode<Button>("%PassButton");
        _playButton = GetNode<Button>("%PlayButton");
        _callButton = GetNode<Button>("%CallButton");
        _bidPassButton = GetNode<Button>("%BidPassButton");
        _resultOverlay = GetNode<Control>("%ResultOverlay");
        _resultTitle = GetNode<Label>("%ResultTitle");
        _resultDetails = GetNode<Label>("%ResultDetails");
        _rematchButton = GetNode<Button>("%RematchButton");

        GetNode<Button>("%BackButton").Pressed += ReturnToLobby;
        GetNode<Button>("%ResultLobbyButton").Pressed += ReturnToLobby;
        _rematchButton.Pressed += StartNewRound;
        _autoButton.Pressed += ToggleAutoPlay;
        _hintButton.Pressed += ShowHint;
        _passButton.Pressed += PassPlay;
        _playButton.Pressed += PlaySelection;
        _callButton.Pressed += MakePositiveBid;
        _bidPassButton.Pressed += PassBid;
    }

    public override void _ExitTree()
    {
        _lifecycleVersion++;
    }

    public void Initialize(
        LocalPlayerProfile profile,
        Action saveProfile,
        Action backRequested,
        bool startWithAutoPlay = false,
        double automaticTurnDelaySeconds = DefaultAutomaticTurnDelaySeconds)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(saveProfile);
        ArgumentNullException.ThrowIfNull(backRequested);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(automaticTurnDelaySeconds);

        _profile = profile;
        _saveProfile = saveProfile;
        _backRequested = backRequested;
        _autoEnabled = startWithAutoPlay;
        _automaticTurnDelaySeconds = automaticTurnDelaySeconds;
        _initialized = true;

        if (_profile.ActiveDoudizhu is { } recovery)
        {
            _session = DoudizhuGameSession.Restore(recovery);
            _statusMessage = "已恢复上次未完成的牌局。";
            if (_session.RuleSnapshot.Phase == DoudizhuPhase.Finished)
            {
                ApplySettlement();
            }
        }
        else
        {
            CreateFreshSession();
            _statusMessage = "新牌局已发牌，等待叫地主。";
        }

        RefreshTable();
        ContinueAutomaticTurns();
    }

    private void StartNewRound()
    {
        if (!_initialized)
        {
            return;
        }

        _lifecycleVersion++;
        _autoEnabled = false;
        _inputLocked = false;
        _selectedCards.Clear();
        if (LocalProfileEconomy.ClaimFreeSupply(_profile))
        {
            _saveProfile?.Invoke();
        }

        CreateFreshSession();
        _statusMessage = "新牌局已发牌，等待叫地主。";
        RefreshTable();
        ContinueAutomaticTurns();
    }

    private void CreateFreshSession()
    {
        var seed = unchecked((ulong)Interlocked.Increment(ref _lastIssuedSeed));
        _session = DoudizhuGameSession.Start(seed, HumanPlayerIndex, BaseScore);
        _profile.ActiveDoudizhu = _session.CreateRecoveryState();
        _saveProfile?.Invoke();
    }

    private void ReturnToLobby()
    {
        _lifecycleVersion++;
        _backRequested?.Invoke();
    }

    private void ToggleAutoPlay()
    {
        if (!_initialized || _session.RuleSnapshot.Phase == DoudizhuPhase.Finished)
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

    private void MakePositiveBid()
    {
        var observation = _session.Snapshot.PlayerObservation;
        var action = observation.BidPrompt switch
        {
            DoudizhuBidPrompt.Call => DoudizhuBidAction.Call,
            DoudizhuBidPrompt.Rob => DoudizhuBidAction.Rob,
            _ => (DoudizhuBidAction?)null,
        };
        if (action is { } bidAction)
        {
            SubmitHumanCommand(new BidCommand(HumanPlayerIndex, bidAction));
        }
    }

    private void PassBid()
    {
        SubmitHumanCommand(new BidCommand(HumanPlayerIndex, DoudizhuBidAction.Pass));
    }

    private void PassPlay()
    {
        SubmitHumanCommand(new PassCommand(HumanPlayerIndex));
    }

    private void PlaySelection()
    {
        var selectedMove = FindSelectedLegalMove();
        if (selectedMove is null)
        {
            _statusMessage = _selectedCards.Count == 0
                ? "请先选择手牌。"
                : "当前组合不是规则层提供的合法出牌。";
            RefreshActionAvailability();
            return;
        }

        SubmitHumanCommand(new PlayCardsCommand(HumanPlayerIndex, selectedMove.Cards));
    }

    private void ShowHint()
    {
        var view = _session.Snapshot;
        if (!view.IsHumanTurn || view.PlayerObservation.Phase != DoudizhuPhase.Playing)
        {
            return;
        }

        var command = _assistantAi.ChooseCommand(view.PlayerObservation, view.LegalMoves);
        _selectedCards.Clear();
        if (command is PlayCardsCommand play)
        {
            _selectedCards.UnionWith(play.Cards);
            var move = FindSelectedLegalMove();
            _statusMessage = move is null
                ? "当前没有可用提示。"
                : $"提示：{FormatPattern(move.Pattern.Kind)}，共 {move.Cards.Count} 张。";
        }
        else
        {
            _statusMessage = "提示：建议不出。";
        }

        SynchronizeCardSelection();
        RefreshActionAvailability();
    }

    private void SubmitHumanCommand(IGameCommand command)
    {
        if (!_initialized || _inputLocked || !_session.Snapshot.IsHumanTurn)
        {
            return;
        }

        _inputLocked = true;
        var result = _session.Dispatch(command);
        _inputLocked = false;
        if (!result.Accepted)
        {
            _statusMessage = result.Error ?? "该操作当前不可用。";
            RefreshTable();
            return;
        }

        _selectedCards.Clear();
        AcceptResult(result);
        RefreshTable();
        ContinueAutomaticTurns();
    }

    private void AcceptResult(CommandResult<DoudizhuSessionView> result)
    {
        _statusMessage = DescribeEvents(result.Events);
        if (_session.RuleSnapshot.Phase == DoudizhuPhase.Finished)
        {
            ApplySettlement();
            return;
        }

        _profile.ActiveDoudizhu = _session.CreateRecoveryState();
        _saveProfile?.Invoke();
    }

    private void ApplySettlement()
    {
        var snapshot = _session.RuleSnapshot;
        if (snapshot.Settlement is null || snapshot.LandlordIndex is null)
        {
            throw new InvalidOperationException("斗地主结束状态缺少结算信息。");
        }

        LocalProfileEconomy.ApplyDoudizhuSettlement(
            _profile,
            snapshot.Settlement,
            HumanPlayerIndex,
            snapshot.LandlordIndex.Value);
        _saveProfile?.Invoke();
        GD.Print(
            $"斗地主牌局完成：{FormatWinningTeam(snapshot.Settlement.WinningTeam)}，"
            + $"倍数 {snapshot.Settlement.FinalMultiplier}，玩家变化 {snapshot.Settlement.ScoreChanges[HumanPlayerIndex]}。");
        if (OS.GetCmdlineUserArgs().Contains("--quit-on-finish", StringComparer.Ordinal))
        {
            GetTree().Quit();
        }
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
                if (view.PlayerObservation.Phase == DoudizhuPhase.Finished
                    || view.IsHumanTurn && !_autoEnabled)
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
                if (view.IsHumanTurn && !_autoEnabled)
                {
                    break;
                }

                var result = view.IsHumanTurn
                    ? _session.Dispatch(_assistantAi.ChooseCommand(view.PlayerObservation, view.LegalMoves))
                    : _session.AdvanceAiTurn();
                if (!result.Accepted)
                {
                    _statusMessage = result.Error ?? "自动操作未被接受。";
                    break;
                }

                AcceptResult(result);
                RefreshTable();
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

    private void RefreshTable()
    {
        if (!_initialized)
        {
            return;
        }

        var view = _session.Snapshot;
        var observation = view.PlayerObservation;
        var ruleSnapshot = _session.RuleSnapshot;
        _selectedCards.IntersectWith(observation.Hand);

        var localChange = ruleSnapshot.Settlement?.ScoreChanges[HumanPlayerIndex];
        _roundInfo.Text = $"豆子 {_profile.Beans:N0}    │    底分 {BaseScore}    │    倍数 {observation.Multiplier}    │    本局 {FormatSigned(localChange)}";
        _leftSeatInfo.Text = FormatSeatInfo(2, observation);
        _rightSeatInfo.Text = FormatSeatInfo(1, observation);
        _playerSeatInfo.Text = FormatSeatInfo(HumanPlayerIndex, observation);
        _bottomCardsLabel.Text = observation.VisibleBottomCards.Count == 0
            ? "底牌：叫地主后公开"
            : $"底牌：{string.Join("  ", observation.VisibleBottomCards.Select(FormatCard))}";
        _lastPlayInfo.Text = FormatLastPlay(observation);

        RebuildHand(observation);
        RefreshControls(view);
        RefreshResult(ruleSnapshot);
        _statusLabel.Text = string.IsNullOrWhiteSpace(_statusMessage)
            ? CreateDefaultStatus(view)
            : _statusMessage;
    }

    private void RefreshControls(DoudizhuSessionView view)
    {
        var observation = view.PlayerObservation;
        var active = observation.Phase is DoudizhuPhase.Bidding or DoudizhuPhase.Playing;
        var bidding = observation.Phase == DoudizhuPhase.Bidding;
        var playing = observation.Phase == DoudizhuPhase.Playing;

        _actionBar.Visible = active;
        _bidActionBar.Visible = bidding;
        _autoButton.Visible = active;
        _autoButton.Text = _autoEnabled ? "托管：开" : "托管：关";
        _autoButton.Disabled = !active;
        _hintButton.Visible = playing;
        _passButton.Visible = playing;
        _playButton.Visible = playing;

        _callButton.Text = observation.BidPrompt == DoudizhuBidPrompt.Rob ? "抢地主" : "叫地主";
        _bidPassButton.Text = observation.BidPrompt == DoudizhuBidPrompt.Rob ? "不抢" : "不叫";
        var canBid = bidding && view.IsHumanTurn && !_inputLocked && !_autoEnabled;
        _callButton.Disabled = !canBid;
        _bidPassButton.Disabled = !canBid;

        RefreshActionAvailability();
    }

    private void RefreshActionAvailability()
    {
        if (!_initialized)
        {
            return;
        }

        var view = _session.Snapshot;
        var canAct = view.IsHumanTurn
            && view.PlayerObservation.Phase == DoudizhuPhase.Playing
            && !_inputLocked
            && !_autoEnabled;
        _hintButton.Disabled = !canAct;
        _passButton.Disabled = !canAct || !view.PlayerObservation.CanPass;
        _playButton.Disabled = !canAct || FindSelectedLegalMove() is null;
        foreach (var button in _cardButtons.Values)
        {
            button.Disabled = !canAct;
        }
    }

    private void RefreshResult(DoudizhuSnapshot snapshot)
    {
        var finished = snapshot.Phase == DoudizhuPhase.Finished && snapshot.Settlement is not null;
        _resultOverlay.Visible = finished;
        if (!finished)
        {
            return;
        }

        var settlement = snapshot.Settlement!;
        var localChange = settlement.ScoreChanges[HumanPlayerIndex];
        _resultTitle.Text = localChange > 0 ? "本局胜利" : "本局惜败";
        _resultDetails.Text = $"{FormatWinningTeam(settlement.WinningTeam)}｜{FormatSpring(settlement.SpringKind)}\n"
            + $"最终倍数 ×{settlement.FinalMultiplier}｜本局 {FormatSigned(localChange)} 豆\n"
            + $"当前豆子 {_profile.Beans:N0}｜战绩 {_profile.DoudizhuStatistics.GamesWon}/{_profile.DoudizhuStatistics.GamesPlayed}";
        _rematchButton.Text = LocalProfileEconomy.CanClaimFreeSupply(_profile)
            ? "补给并再来一局"
            : "再来一局";
    }

    private void RebuildHand(DoudizhuObservation observation)
    {
        foreach (var child in _playerHand.GetChildren())
        {
            _playerHand.RemoveChild(child);
            child.QueueFree();
        }

        _cardButtons.Clear();
        foreach (var card in observation.Hand
                     .OrderByDescending(card => card.Rank)
                     .ThenBy(card => card.Suit))
        {
            var button = CreateCardButton(card);
            _cardButtons.Add(card, button);
            _playerHand.AddChild(button);
        }
    }

    private Button CreateCardButton(Card card)
    {
        var button = new Button
        {
            Text = FormatCardFace(card),
            TooltipText = FormatCard(card),
            CustomMinimumSize = new Vector2(45, 88),
            ToggleMode = true,
            ButtonPressed = _selectedCards.Contains(card),
            FocusMode = FocusModeEnum.All,
        };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", IsRed(card) ? new Color("9c3030") : new Color("18202a"));
        button.AddThemeColorOverride("font_pressed_color", new Color("102521"));
        button.AddThemeStyleboxOverride("normal", CreatePieceStyle(_pieceBackground, _pieceBorder));
        button.AddThemeStyleboxOverride("hover", CreatePieceStyle(_pieceHover, new Color("d3a84a")));
        button.AddThemeStyleboxOverride("pressed", CreatePieceStyle(_pieceSelected, new Color("b9f3dc")));
        button.AddThemeStyleboxOverride("focus", CreatePieceStyle(new Color(0, 0, 0, 0), new Color("f2bd55")));
        button.Toggled += pressed => OnCardToggled(card, pressed);
        return button;
    }

    private void OnCardToggled(Card card, bool pressed)
    {
        if (_synchronizingSelection)
        {
            return;
        }

        if (pressed)
        {
            _selectedCards.Add(card);
        }
        else
        {
            _selectedCards.Remove(card);
        }

        var move = FindSelectedLegalMove();
        _statusMessage = _selectedCards.Count == 0
            ? "请选择要出的牌。"
            : move is null
                ? $"已选择 {_selectedCards.Count} 张，当前组合不可出。"
                : $"已选择：{FormatPattern(move.Pattern.Kind)}。";
        RefreshActionAvailability();
        _statusLabel.Text = _statusMessage;
    }

    private void SynchronizeCardSelection()
    {
        _synchronizingSelection = true;
        foreach (var (card, button) in _cardButtons)
        {
            button.ButtonPressed = _selectedCards.Contains(card);
        }

        _synchronizingSelection = false;
    }

    private DoudizhuMove? FindSelectedLegalMove()
    {
        if (!_initialized || _selectedCards.Count == 0)
        {
            return null;
        }

        return _session.Snapshot.LegalMoves.FirstOrDefault(move =>
            move.Cards.Count == _selectedCards.Count
            && move.Cards.All(_selectedCards.Contains));
    }

    private static StyleBoxFlat CreatePieceStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomRight = 5,
            CornerRadiusBottomLeft = 5,
        };
    }

    private static string FormatSeatInfo(int playerIndex, DoudizhuObservation observation)
    {
        var current = observation.CurrentPlayerIndex == playerIndex
            && observation.Phase is DoudizhuPhase.Bidding or DoudizhuPhase.Playing
            ? "▶ "
            : string.Empty;
        var role = observation.LandlordIndex is null
            ? "叫抢中"
            : observation.LandlordIndex == playerIndex ? "地主" : "农民";
        var name = playerIndex switch
        {
            0 => "你",
            1 => "右家",
            2 => "左家",
            _ => $"座位 {playerIndex}",
        };
        return $"{current}{name}\n\n{observation.RemainingCardCounts[playerIndex]} 张\n{role}";
    }

    private static string FormatLastPlay(DoudizhuObservation observation)
    {
        if (observation.LastMove is { } move && observation.LastMovePlayerIndex is { } playerIndex)
        {
            var cardSummary = move.Cards.Count <= 10
                ? string.Join("  ", move.Cards.Select(FormatCard))
                : $"{string.Join("  ", move.Cards.Take(8).Select(FormatCard))}  …（共 {move.Cards.Count} 张）";
            return $"{FormatSeatName(playerIndex)}出牌\n{cardSummary}\n{FormatPattern(move.Pattern.Kind)}";
        }

        return observation.Phase switch
        {
            DoudizhuPhase.Bidding => "等待叫地主\n\n底牌尚未公开",
            DoudizhuPhase.Playing => $"新一轮\n\n{FormatSeatName(observation.CurrentPlayerIndex)}领出",
            DoudizhuPhase.Finished => "本局结束",
            _ => "准备牌局",
        };
    }

    private static string CreateDefaultStatus(DoudizhuSessionView view)
    {
        var observation = view.PlayerObservation;
        if (!view.IsHumanTurn)
        {
            return $"等待{FormatSeatName(observation.CurrentPlayerIndex)}操作……";
        }

        return observation.Phase switch
        {
            DoudizhuPhase.Bidding when observation.BidPrompt == DoudizhuBidPrompt.Rob => "轮到你：抢地主或不抢。",
            DoudizhuPhase.Bidding => "轮到你：叫地主或不叫。",
            DoudizhuPhase.Playing when observation.CanPass => "轮到你：请选择更大的牌，或不出。",
            DoudizhuPhase.Playing => "轮到你领出，请选择一手牌。",
            DoudizhuPhase.Finished => "本局已经结束。",
            _ => "牌局准备中。",
        };
    }

    private static string DescribeEvents(IReadOnlyList<IGameEvent> events)
    {
        return events.Count == 0
            ? string.Empty
            : string.Join("；", events.Select(DescribeEvent));
    }

    private static string DescribeEvent(IGameEvent gameEvent)
    {
        return gameEvent switch
        {
            BidMadeEvent bid => $"{FormatSeatName(bid.PlayerIndex)}{FormatBidAction(bid.Action)}",
            CardsRedealtEvent redealt => $"无人叫地主，第 {redealt.RedealCount + 1} 次发牌",
            LandlordDeterminedEvent landlord => $"{FormatSeatName(landlord.LandlordIndex)}成为地主",
            CardsPlayedEvent played => $"{FormatSeatName(played.PlayerIndex)}打出{FormatPattern(played.Move.Pattern.Kind)}",
            PlayerPassedEvent passed => $"{FormatSeatName(passed.PlayerIndex)}不出",
            TrickResetEvent trick => $"一轮结束，{FormatSeatName(trick.LeaderIndex)}重新领出",
            DoudizhuFinishedEvent finished => $"本局结束，{FormatWinningTeam(finished.Settlement.WinningTeam)}",
            _ => "牌局状态已更新",
        };
    }

    private static string FormatBidAction(DoudizhuBidAction action)
    {
        return action switch
        {
            DoudizhuBidAction.Call => "叫地主",
            DoudizhuBidAction.Rob => "抢地主",
            DoudizhuBidAction.Pass => "放弃叫抢",
            _ => action.ToString(),
        };
    }

    private static string FormatPattern(CardPatternKind kind)
    {
        return kind switch
        {
            CardPatternKind.Single => "单张",
            CardPatternKind.Pair => "对子",
            CardPatternKind.Triple => "三张",
            CardPatternKind.TripleWithSingle => "三带一",
            CardPatternKind.TripleWithPair => "三带二",
            CardPatternKind.Straight => "顺子",
            CardPatternKind.PairStraight => "连对",
            CardPatternKind.Airplane => "飞机",
            CardPatternKind.AirplaneWithSingles => "飞机带单",
            CardPatternKind.AirplaneWithPairs => "飞机带对",
            CardPatternKind.FourWithSingles => "四带二单",
            CardPatternKind.FourWithPairs => "四带二对",
            CardPatternKind.Bomb => "炸弹",
            CardPatternKind.Rocket => "王炸",
            _ => kind.ToString(),
        };
    }

    private static string FormatWinningTeam(DoudizhuWinningTeam winningTeam)
    {
        return winningTeam == DoudizhuWinningTeam.Landlord ? "地主获胜" : "农民获胜";
    }

    private static string FormatSpring(DoudizhuSpringKind springKind)
    {
        return springKind switch
        {
            DoudizhuSpringKind.Spring => "春天",
            DoudizhuSpringKind.CounterSpring => "反春天",
            _ => "无春天加倍",
        };
    }

    private static string FormatSigned(long? value)
    {
        return value is null ? "-" : value >= 0 ? $"+{value:N0}" : $"{value:N0}";
    }

    private static string FormatSeatName(int playerIndex)
    {
        return playerIndex switch
        {
            0 => "你",
            1 => "右家",
            2 => "左家",
            _ => $"座位 {playerIndex}",
        };
    }

    private static bool IsRed(Card card)
    {
        return card.Suit is CardSuit.Diamonds or CardSuit.Hearts || card.Rank == CardRank.BigJoker;
    }

    private static string FormatCard(Card card)
    {
        return card.IsJoker ? FormatRank(card.Rank) : $"{FormatRank(card.Rank)}{FormatSuit(card.Suit)}";
    }

    private static string FormatCardFace(Card card)
    {
        return card.IsJoker ? FormatRank(card.Rank).Replace("王", "\n王") : $"{FormatRank(card.Rank)}\n{FormatSuit(card.Suit)}";
    }

    private static string FormatRank(CardRank rank)
    {
        return rank switch
        {
            CardRank.Jack => "J",
            CardRank.Queen => "Q",
            CardRank.King => "K",
            CardRank.Ace => "A",
            CardRank.Two => "2",
            CardRank.SmallJoker => "小王",
            CardRank.BigJoker => "大王",
            _ => ((int)rank).ToString(),
        };
    }

    private static string FormatSuit(CardSuit suit)
    {
        return suit switch
        {
            CardSuit.Clubs => "♣",
            CardSuit.Diamonds => "♦",
            CardSuit.Hearts => "♥",
            CardSuit.Spades => "♠",
            _ => string.Empty,
        };
    }
}
