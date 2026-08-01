using FangcunCardClub.Game.Doudizhu;
using FangcunCardClub.Game.Mahjong;
using Game.Application.Mahjong;
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
    private double _doudizhuTurnDelaySeconds = DoudizhuTableController.DefaultAutomaticTurnDelaySeconds;
    private ulong? _mahjongInitialSeed;
    private MahjongMode _mahjongMode = MahjongMode.Standard;
    private double _mahjongTurnDelaySeconds = MahjongAnimationTiming.AiThinkMilliseconds / 1000.0;
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
            _mahjongTurnDelaySeconds = 0.01;
        }

        _mahjongMode = userArguments.FirstOrDefault(argument =>
                argument.StartsWith("--mahjong-mode=", StringComparison.Ordinal)) switch
        {
            "--mahjong-mode=sichuan" => MahjongMode.Sichuan,
            "--mahjong-mode=riichi" => MahjongMode.Riichi,
            _ => MahjongMode.Standard,
        };
        var mahjongSeedArgument = userArguments.FirstOrDefault(argument =>
            argument.StartsWith("--mahjong-seed=", StringComparison.Ordinal));
        if (mahjongSeedArgument is not null
            && ulong.TryParse(mahjongSeedArgument["--mahjong-seed=".Length..], out var mahjongSeed))
        {
            _mahjongInitialSeed = mahjongSeed;
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
        var doudizhuNewGameButton = lobby.GetNode<Button>("%DoudizhuNewGameButton");
        var standardButton = lobby.GetNode<Button>("%MahjongEntryButton");
        var sichuanButton = lobby.GetNode<Button>("%SichuanEntryButton");
        var riichiButton = lobby.GetNode<Button>("%RiichiEntryButton");

        var needsSupply = LocalProfileEconomy.CanClaimFreeSupply(_profile);
        beanLabel.Text = $"豆子 {_profile.Beans:N0}";
        supplyButton.Disabled = !needsSupply;
        supplyButton.Text = supplyButton.Disabled ? "豆子充足" : "免费补给";
        doudizhuButton.Disabled = needsSupply && _profile.ActiveDoudizhu is null;
        doudizhuButton.OffsetBottom = _profile.ActiveDoudizhu is null ? 376 : 310;
        doudizhuButton.Text = _profile.ActiveDoudizhu is not null
            ? "斗地主\n\n继续未完成牌局"
            : needsSupply
                ? "斗地主\n\n豆子不足\n\n请先免费补给"
                : "斗地主\n\n经典三人叫地主 · 无癞子\n\n开始游戏";
        doudizhuNewGameButton.Visible = _profile.ActiveDoudizhu is not null;
        doudizhuNewGameButton.Disabled = needsSupply;
        doudizhuNewGameButton.Text = needsSupply
            ? "先补给，再放弃续局"
            : "放弃续局并新开";

        doudizhuButton.Pressed += ShowDoudizhu;
        doudizhuNewGameButton.Pressed += StartFreshDoudizhu;
        ConfigureMahjongEntry(standardButton, MahjongMode.Standard, "大众麻将", "完整单局");
        ConfigureMahjongEntry(sichuanButton, MahjongMode.Sichuan, "四川血战", "完整单局");
        ConfigureMahjongEntry(riichiButton, MahjongMode.Riichi, "四人日麻", "完整东风战");
        standardButton.Pressed += () => ShowMahjong(MahjongMode.Standard);
        sichuanButton.Pressed += () => ShowMahjong(MahjongMode.Sichuan);
        riichiButton.Pressed += () => ShowMahjong(MahjongMode.Riichi);
        supplyButton.Pressed += () =>
        {
            if (LocalProfileEconomy.ClaimFreeSupply(_profile))
            {
                SaveProfile();
                beanLabel.Text = $"豆子 {_profile.Beans:N0}";
                supplyButton.Disabled = true;
                supplyButton.Text = "豆子充足";
                if (_profile.ActiveDoudizhu is not null)
                {
                    doudizhuNewGameButton.Disabled = false;
                    doudizhuNewGameButton.Text = "放弃续局并新开";
                }
                status.Text = "免费补给完成：无广告、无等待、无次数限制。";
            }
        };

        status.Text = _profile.ActiveMahjong is { } mahjongRecovery
            ? $"已保存{MahjongModeText(mahjongRecovery.Mode)}对局，请从对应入口继续。"
            : _profile.ActiveDoudizhu is null
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

    private void StartFreshDoudizhu()
    {
        _profile.ActiveDoudizhu = null;
        SaveProfile();
        ShowDoudizhu();
    }

    private void ConfigureMahjongEntry(
        Button button,
        MahjongMode mode,
        string title,
        string scope)
    {
        var active = _profile.ActiveMahjong;
        button.Disabled = active is not null && active.Mode != mode;
        if (active?.Mode == mode)
        {
            button.Text = $"{title}  ·  继续未完成对局";
            return;
        }

        var statistics = _profile.MahjongStatistics.For(mode);
        button.Text = active is null
            ? $"{title}  ·  {scope}\n{statistics.GamesWon}/{statistics.GamesPlayed} 胜  累计 {FormatSigned(statistics.TotalScoreChange)}"
            : $"{title}  ·  暂停新局";
    }

    private void ShowMahjong()
    {
        ShowMahjong(_mahjongMode);
    }

    private void ShowMahjong(MahjongMode mode)
    {
        _mahjongMode = mode;
        var table = (MahjongTableController)ChangeScreen(MahjongScenePath);
        table.Initialize(
            _profile,
            SaveProfile,
            _mahjongMode,
            ShowLobby,
            _doudizhuAutoPlay,
            _mahjongTurnDelaySeconds,
            _mahjongInitialSeed);
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

    private void UpdateResolutionBadge()
    {
        var windowSize = DisplayServer.WindowGetSize();
        _resolutionBadge.Text = $"安全区 960×540｜窗口 {windowSize.X}×{windowSize.Y}";
    }

    private void SaveProfile()
    {
        _profileStore?.Save(_profile);
    }

    private static string MahjongModeText(MahjongMode mode)
    {
        return mode switch
        {
            MahjongMode.Standard => "大众麻将",
            MahjongMode.Sichuan => "四川血战",
            MahjongMode.Riichi => "四人日麻",
            _ => mode.ToString(),
        };
    }

    private static string FormatSigned(long value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }
}
