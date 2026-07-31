using Game.Core.Random;
using Game.Doudizhu.Cards;
using Godot;

namespace FangcunCardClub.Game;

public partial class Bootstrap : Control
{
    private const string LobbyScenePath = "res://game/scenes/lobby/Lobby.tscn";
    private const string DoudizhuScenePath = "res://game/scenes/doudizhu/DoudizhuTable.tscn";
    private const string MahjongScenePath = "res://game/scenes/mahjong/MahjongTable.tscn";
    private const ulong PreviewSeed = 20260801;

    private readonly Color _pieceBackground = new("f1e5cc");
    private readonly Color _pieceBorder = new("6d6257");
    private readonly Color _pieceHover = new("fff5dc");
    private readonly Color _pieceSelected = new("4fc8ad");

    private Control? _currentScreen;
    private Label _resolutionBadge = null!;
    private Control _screenHost = null!;

    public override void _Ready()
    {
        _screenHost = GetNode<Control>("%ScreenHost");
        _resolutionBadge = GetNode<Label>("%ResolutionBadge");

        GetViewport().SizeChanged += UpdateResolutionBadge;
        UpdateResolutionBadge();

        var previewArgument = OS.GetCmdlineUserArgs().FirstOrDefault(argument => argument.StartsWith("--preview=", StringComparison.Ordinal));
        switch (previewArgument)
        {
            case "--preview=doudizhu":
                ShowDoudizhu();
                break;
            case "--preview=mahjong":
                ShowMahjong();
                break;
            default:
                ShowLobby();
                break;
        }
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= UpdateResolutionBadge;
    }

    private void ShowLobby()
    {
        var lobby = ChangeScreen(LobbyScenePath);
        var status = lobby.GetNode<Label>("%StatusLabel");
        var beanLabel = lobby.GetNode<Label>("%BeanLabel");

        lobby.GetNode<Button>("%DoudizhuEntryButton").Pressed += ShowDoudizhu;
        lobby.GetNode<Button>("%MahjongEntryButton").Pressed += ShowMahjong;
        lobby.GetNode<Button>("%SupplyButton").Pressed += () =>
        {
            beanLabel.Text = "豆子 3,000";
            status.Text = "免费补给完成：无广告、无等待、无次数限制。";
        };

        var previewDeck = CardDeck.CreateShuffled(new SplitMix64Random(PreviewSeed));
        status.Text = $"布局骨架已加载｜固定种子牌堆：{string.Join(" · ", previewDeck.Take(3).Select(FormatCard))}";
    }

    private void ShowDoudizhu()
    {
        var table = ChangeScreen(DoudizhuScenePath);
        var hand = table.GetNode<HBoxContainer>("%PlayerHand");
        var status = table.GetNode<Label>("%StatusLabel");
        var cardButtons = new List<Button>();

        var cards = CardDeck.CreateShuffled(new SplitMix64Random(PreviewSeed))
            .Take(20)
            .OrderByDescending(card => card.Rank)
            .ThenBy(card => card.Suit);

        foreach (var card in cards)
        {
            var button = CreatePieceButton(FormatCardFace(card), new Vector2(45, 88));
            button.TooltipText = FormatCard(card);
            button.AddThemeColorOverride("font_color", IsRed(card) ? new Color("9c3030") : new Color("18202a"));
            button.AddThemeColorOverride("font_pressed_color", new Color("102521"));
            button.Toggled += _ =>
            {
                var selectedCount = cardButtons.Count(candidate => candidate.ButtonPressed);
                status.Text = selectedCount == 0
                    ? "未选择手牌。灰盒只演示交互，不在表现层判断牌型。"
                    : $"已选择 {selectedCount} 张牌；合法性以后由斗地主规则层提供。";
            };

            cardButtons.Add(button);
            hand.AddChild(button);
        }

        table.GetNode<Button>("%BackButton").Pressed += ShowLobby;
        table.GetNode<Button>("%HintButton").Pressed += () =>
        {
            foreach (var button in cardButtons)
            {
                button.ButtonPressed = false;
            }

            foreach (var button in cardButtons.Take(3))
            {
                button.ButtonPressed = true;
            }

            status.Text = "提示占位：规则层将返回合法方案，界面只负责高亮结果。";
        };
        table.GetNode<Button>("%PassButton").Pressed += () =>
        {
            ClearSelection(cardButtons);
            status.Text = "不出演示：等待规则层接受命令后再播放过场。";
        };
        table.GetNode<Button>("%PlayButton").Pressed += () =>
        {
            var selectedCount = cardButtons.Count(button => button.ButtonPressed);
            status.Text = selectedCount == 0
                ? "请先选择手牌。"
                : $"出牌按钮命中：提交 {selectedCount} 张牌的玩家意图，不在界面计算牌型。";
        };
        BindAutoButton(table.GetNode<Button>("%AutoButton"), status);
    }

    private void ShowMahjong()
    {
        var table = ChangeScreen(MahjongScenePath);
        var hand = table.GetNode<HBoxContainer>("%PlayerTiles");
        var status = table.GetNode<Label>("%StatusLabel");
        var tileButtons = new List<Button>();
        string[] tiles = ["二万", "三万", "三万", "四万", "五筒", "六筒", "七筒", "八筒", "三条", "四条", "五条", "东", "发", "中"];

        foreach (var tile in tiles)
        {
            var button = CreatePieceButton(tile.Replace("万", "\n万").Replace("筒", "\n筒").Replace("条", "\n条"), new Vector2(43, 72));
            button.TooltipText = tile;
            button.AddThemeColorOverride("font_color", tile is "中" or "发" ? new Color("9c3030") : new Color("17342d"));
            button.AddThemeColorOverride("font_pressed_color", new Color("102521"));
            button.Toggled += selected =>
            {
                if (selected)
                {
                    foreach (var other in tileButtons.Where(other => other != button))
                    {
                        other.ButtonPressed = false;
                    }
                }

                status.Text = selected
                    ? $"已选择 {tile}；舍牌合法性与向听变化以后由麻将规则层提供。"
                    : "未选择牌；灰盒只验证点击尺寸和排列密度。";
            };

            tileButtons.Add(button);
            hand.AddChild(button);
        }

        table.GetNode<Button>("%BackButton").Pressed += ShowLobby;
        table.GetNode<Button>("%HintButton").Pressed += () =>
        {
            tileButtons[7].ButtonPressed = true;
            status.Text = "提示占位：推荐打八筒；正式版本同时显示向听数和进张。";
        };
        BindAutoButton(table.GetNode<Button>("%AutoButton"), status);
        BindMahjongAction(table, "%ChowButton", "吃");
        BindMahjongAction(table, "%PongButton", "碰");
        BindMahjongAction(table, "%KongButton", "杠");
        BindMahjongAction(table, "%WinButton", "和牌");
        BindMahjongAction(table, "%SkipButton", "跳过");
    }

    private Control ChangeScreen(string scenePath)
    {
        _currentScreen?.QueueFree();

        var scene = GD.Load<PackedScene>(scenePath).Instantiate<Control>();
        _screenHost.AddChild(scene);
        scene.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _currentScreen = scene;
        return scene;
    }

    private Button CreatePieceButton(string text, Vector2 minimumSize)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = minimumSize,
            ToggleMode = true,
            FocusMode = FocusModeEnum.All,
        };

        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeStyleboxOverride("normal", CreatePieceStyle(_pieceBackground, _pieceBorder));
        button.AddThemeStyleboxOverride("hover", CreatePieceStyle(_pieceHover, new Color("d3a84a")));
        button.AddThemeStyleboxOverride("pressed", CreatePieceStyle(_pieceSelected, new Color("b9f3dc")));
        button.AddThemeStyleboxOverride("focus", CreatePieceStyle(new Color(0, 0, 0, 0), new Color("f2bd55")));
        return button;
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

    private static void BindAutoButton(Button button, Label status)
    {
        var enabled = false;
        button.Pressed += () =>
        {
            enabled = !enabled;
            button.Text = enabled ? "托管：开" : "托管：关";
            status.Text = enabled
                ? "托管已开启；正式版本与提示共用同一个决策接口。"
                : "托管已关闭。";
        };
    }

    private static void BindMahjongAction(Control table, string buttonPath, string action)
    {
        var status = table.GetNode<Label>("%StatusLabel");
        table.GetNode<Button>(buttonPath).Pressed += () =>
        {
            status.Text = $"{action}按钮命中：等待麻将规则层提供可用状态和执行结果。";
        };
    }

    private static void ClearSelection(IEnumerable<Button> buttons)
    {
        foreach (var button in buttons)
        {
            button.ButtonPressed = false;
        }
    }

    private void UpdateResolutionBadge()
    {
        var windowSize = DisplayServer.WindowGetSize();
        _resolutionBadge.Text = $"安全区 960×540｜窗口 {windowSize.X}×{windowSize.Y}";
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
