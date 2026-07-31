using Game.Core.Random;
using Game.Doudizhu.Cards;
using Godot;

namespace FangcunCardClub.Game;

public partial class Bootstrap : Control
{
    private const ulong PreviewSeed = 20260801;
    private const int SupplyAmount = 3_000;

    private Label _statusLabel = null!;
    private Label _beanLabel = null!;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label>("%StatusLabel");
        _beanLabel = GetNode<Label>("%BeanLabel");

        GetNode<Button>("%DoudizhuButton").Pressed += ShowDoudizhuStatus;
        GetNode<Button>("%MahjongButton").Pressed += ShowMahjongStatus;
        GetNode<Button>("%SupplyButton").Pressed += SupplyBeans;

        var previewDeck = CardDeck.CreateShuffled(new SplitMix64Random(PreviewSeed));
        var preview = string.Join(" · ", previewDeck.Take(3).Select(FormatCard));
        _statusLabel.Text = $"规则核心已加载｜固定种子牌堆预览：{preview}";
    }

    private void ShowDoudizhuStatus()
    {
        _statusLabel.Text = "斗地主纵向切片：工程骨架已就绪，下一步实现牌型与叫抢状态机。";
    }

    private void ShowMahjongStatus()
    {
        _statusLabel.Text = "麻将入口已预留，将在斗地主纵向切片稳定后接入公共麻将内核。";
    }

    private void SupplyBeans()
    {
        _beanLabel.Text = $"豆子 {SupplyAmount:N0}";
        _statusLabel.Text = "免费补给已触发：无广告、无等待、无次数限制。";
    }

    private static string FormatCard(Card card)
    {
        return card.IsJoker ? card.Rank.ToString() : $"{card.Rank}/{card.Suit}";
    }
}
