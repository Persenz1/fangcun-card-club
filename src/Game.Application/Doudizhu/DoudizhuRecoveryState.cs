using System.Text.Json.Serialization;
using Game.Core.Simulation;
using Game.Doudizhu.Cards;
using Game.Doudizhu.Commands;

namespace Game.Application.Doudizhu;

public sealed class DoudizhuRecoveryState
{
    [JsonPropertyName("seed")]
    public ulong Seed { get; set; }

    [JsonPropertyName("base_score")]
    public int BaseScore { get; set; } = 10;

    [JsonPropertyName("human_player_index")]
    public int HumanPlayerIndex { get; set; }

    [JsonPropertyName("accepted_commands")]
    public List<DoudizhuCommandRecord> AcceptedCommands { get; set; } = [];

    public void Validate()
    {
        if (BaseScore <= 0 || HumanPlayerIndex is < 0 or >= 3 || AcceptedCommands is null)
        {
            throw new InvalidDataException("斗地主恢复记录配置无效。");
        }

        foreach (var command in AcceptedCommands)
        {
            _ = command?.ToCommand() ?? throw new InvalidDataException("斗地主恢复记录包含空命令。");
        }
    }
}

public enum DoudizhuStoredCommandKind
{
    Bid,
    Play,
    Pass,
}

public sealed class DoudizhuCommandRecord
{
    [JsonPropertyName("kind")]
    public DoudizhuStoredCommandKind Kind { get; set; }

    [JsonPropertyName("player_index")]
    public int PlayerIndex { get; set; }

    [JsonPropertyName("bid_action")]
    public DoudizhuBidAction? BidAction { get; set; }

    [JsonPropertyName("cards")]
    public List<DoudizhuStoredCard> Cards { get; set; } = [];

    public static DoudizhuCommandRecord FromCommand(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            BidCommand bid => new DoudizhuCommandRecord
            {
                Kind = DoudizhuStoredCommandKind.Bid,
                PlayerIndex = bid.PlayerIndex,
                BidAction = bid.Action,
            },
            PlayCardsCommand play => new DoudizhuCommandRecord
            {
                Kind = DoudizhuStoredCommandKind.Play,
                PlayerIndex = play.PlayerIndex,
                Cards = play.Cards.Select(DoudizhuStoredCard.FromCard).ToList(),
            },
            PassCommand pass => new DoudizhuCommandRecord
            {
                Kind = DoudizhuStoredCommandKind.Pass,
                PlayerIndex = pass.PlayerIndex,
            },
            _ => throw new ArgumentException("不是可保存的斗地主命令。", nameof(command)),
        };
    }

    public IGameCommand ToCommand()
    {
        if (PlayerIndex is < 0 or >= 3
            || Cards is null
            || Cards.Any(card => card is null)
            || !Enum.IsDefined(Kind)
            || BidAction is { } bidAction && !Enum.IsDefined(bidAction))
        {
            throw new InvalidDataException("斗地主命令记录无效。");
        }

        return Kind switch
        {
            DoudizhuStoredCommandKind.Bid when BidAction is not null && Cards.Count == 0 =>
                new BidCommand(PlayerIndex, BidAction.Value),
            DoudizhuStoredCommandKind.Play when BidAction is null && Cards.Count > 0 =>
                new PlayCardsCommand(PlayerIndex, Cards.Select(card => card.ToCard())),
            DoudizhuStoredCommandKind.Pass when BidAction is null && Cards.Count == 0 =>
                new PassCommand(PlayerIndex),
            _ => throw new InvalidDataException("斗地主命令记录字段组合无效。"),
        };
    }
}

public sealed class DoudizhuStoredCard
{
    [JsonPropertyName("suit")]
    public CardSuit Suit { get; set; }

    [JsonPropertyName("rank")]
    public CardRank Rank { get; set; }

    public static DoudizhuStoredCard FromCard(Card card)
    {
        return new DoudizhuStoredCard { Suit = card.Suit, Rank = card.Rank };
    }

    public Card ToCard()
    {
        if (!Enum.IsDefined(Suit) || !Enum.IsDefined(Rank))
        {
            throw new InvalidDataException("斗地主命令记录包含未定义的牌张枚举值。");
        }

        try
        {
            return new Card(Suit, Rank);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("斗地主命令记录包含无效牌张。", exception);
        }
    }
}
