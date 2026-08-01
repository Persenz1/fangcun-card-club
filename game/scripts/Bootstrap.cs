using FangcunCardClub.Game.Doudizhu;
using FangcunCardClub.Game.Mahjong;
using Game.Application.Profiles;
using Godot;

namespace FangcunCardClub.Game;

public partial class Bootstrap : Control
{
    private const string LobbyScenePath = "res://game/scenes/lobby/Lobby.tscn";
    private const string DoudizhuScenePath = "res://game/scenes/doudizhu/DoudizhuTable.tscn";
    private const string MahjongScenePath = "res://game/scenes/mahjong/MahjongTable.tscn";
    private Control? _currentScreen;
    private bool _doudizhuAutoPlay;
    private double _doudizhuTurnDelaySeconds = 0.42;
    private LocalPlayerProfile _profile = null!;
    private JsonProfileStore? _profileStore;
    private Label _resolutionBadge = null!;
    private Control _screenHost = null!;

    public override void _Ready()
    {
        _screenHost = GetNode<Control>("%ScreenHost");
        _resolutionBadge = GetNode<Label>("%ResolutionBadge");

        GetViewport().SizeChanged += UpdateResolutionBadge;
        UpdateResolutionBadge();

        var userArguments = OS.GetCmdlineUserArgs();
        var previewArgument = userArguments.FirstOrDefault(argument => argument.StartsWith("--preview=", StringComparison.Ordinal));
        _doudizhuAutoPlay = userArguments.Contains("--autoplay", StringComparer.Ordinal);
        if (userArguments.Contains("--fast-autoplay", StringComparer.Ordinal))
        {
            _doudizhuTurnDelaySeconds = 0.01;
        }
        if (previewArgument is null)
        {
            var profilePath = ProjectSettings.GlobalizePath("user://profile-v1.json");
            _profileStore = new JsonProfileStore(profilePath);
            _profile = _profileStore.Load();
        }
        else
        {
            _profile = new LocalPlayerProfile();
        }

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
        var supplyButton = lobby.GetNode<Button>("%SupplyButton");
        var doudizhuButton = lobby.GetNode<Button>("%DoudizhuEntryButton");

        var needsSupply = LocalProfileEconomy.CanClaimFreeSupply(_profile);
        beanLabel.Text = $"豆子 {_profile.Beans:N0}";
        supplyButton.Disabled = !needsSupply;
        supplyButton.Text = supplyButton.Disabled ? "豆子充足" : "免费补给";
        doudizhuButton.Disabled = needsSupply && _profile.ActiveDoudizhu is null;
        doudizhuButton.Text = _profile.ActiveDoudizhu is not null
            ? "斗地主\n\n检测到未完成牌局\n\n继续游戏"
            : needsSupply
                ? "斗地主\n\n豆子不足\n\n请先免费补给"
                : "斗地主\n\n经典三人叫地主 · 无癞子\n\n开始游戏";

        doudizhuButton.Pressed += ShowDoudizhu;
        lobby.GetNode<Button>("%MahjongEntryButton").Pressed += ShowMahjong;
        supplyButton.Pressed += () =>
        {
            if (LocalProfileEconomy.ClaimFreeSupply(_profile))
            {
                SaveProfile();
                beanLabel.Text = $"豆子 {_profile.Beans:N0}";
                supplyButton.Disabled = true;
                supplyButton.Text = "豆子充足";
                status.Text = "免费补给完成：无广告、无等待、无次数限制。";
            }
        };

        status.Text = _profile.ActiveDoudizhu is null
            ? $"斗地主战绩：{_profile.DoudizhuStatistics.GamesWon} 胜 / {_profile.DoudizhuStatistics.GamesPlayed} 局。"
            : "上次斗地主牌局已保存，进入后从原进度继续。";
    }

    private void ShowDoudizhu()
    {
        var table = (DoudizhuTableController)ChangeScreen(DoudizhuScenePath);
        table.Initialize(
            _profile,
            SaveProfile,
            ShowLobby,
            _doudizhuAutoPlay,
            _doudizhuTurnDelaySeconds);
    }

    private void ShowMahjong()
    {
        var table = ChangeScreen(MahjongScenePath);
        var board = table.GetNode<MahjongBoard3D>("%MahjongBoard3D");
        var tableGuide = table.GetNode<Control>("%TableGuide");
        var status = table.GetNode<Label>("%StatusLabel");

        board.PlayerTileSelected += (tileIndex, displayName) =>
        {
            status.Text = tileIndex >= 0
                ? $"已选择 {displayName}；3D 层只上抬牌，合法性以后由麻将规则层提供。"
                : "未选择牌；点击 3D 手牌可以切换选中状态。";
        };

        table.GetNode<Button>("%BackButton").Pressed += ShowLobby;
        table.GetNode<Button>("%HintButton").Pressed += () =>
        {
            board.SelectPlayerTile(7);
            status.Text = "提示占位：推荐打八筒；正式版本同时显示向听数和进张。";
        };
        BindAutoButton(table.GetNode<Button>("%AutoButton"), status);
        var guideButton = table.GetNode<Button>("%GuideButton");
        guideButton.Pressed += () =>
        {
            tableGuide.Visible = !tableGuide.Visible;
            guideButton.Text = tableGuide.Visible ? "标线：开" : "标线：关";
        };
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

    private void UpdateResolutionBadge()
    {
        var windowSize = DisplayServer.WindowGetSize();
        _resolutionBadge.Text = $"安全区 960×540｜窗口 {windowSize.X}×{windowSize.Y}";
    }

    private void SaveProfile()
    {
        _profileStore?.Save(_profile);
    }
}
