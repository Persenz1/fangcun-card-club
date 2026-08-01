using System.Text.Json.Serialization;
using Game.Application.Doudizhu;
using Game.Application.Mahjong;

namespace Game.Application.Profiles;

public sealed class LocalPlayerProfile
{
    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("beans")]
    public long Beans { get; set; } = LocalProfileEconomy.SupplyAmount;

    [JsonPropertyName("doudizhu_statistics")]
    public DoudizhuStatistics DoudizhuStatistics { get; set; } = new();

    [JsonPropertyName("active_doudizhu")]
    public DoudizhuRecoveryState? ActiveDoudizhu { get; set; }

    [JsonPropertyName("mahjong_statistics")]
    public MahjongStatistics MahjongStatistics { get; set; } = new();

    [JsonPropertyName("active_mahjong")]
    public MahjongRecoveryState? ActiveMahjong { get; set; }
}

public sealed class DoudizhuStatistics
{
    [JsonPropertyName("games_played")]
    public int GamesPlayed { get; set; }

    [JsonPropertyName("games_won")]
    public int GamesWon { get; set; }
}

public sealed class MahjongStatistics
{
    [JsonPropertyName("standard")]
    public MahjongModeStatistics Standard { get; set; } = new();

    [JsonPropertyName("sichuan")]
    public MahjongModeStatistics Sichuan { get; set; } = new();

    [JsonPropertyName("riichi")]
    public MahjongModeStatistics Riichi { get; set; } = new();

    public MahjongModeStatistics For(MahjongMode mode)
    {
        return mode switch
        {
            MahjongMode.Standard => Standard,
            MahjongMode.Sichuan => Sichuan,
            MahjongMode.Riichi => Riichi,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}

public sealed class MahjongModeStatistics
{
    [JsonPropertyName("games_played")]
    public int GamesPlayed { get; set; }

    [JsonPropertyName("games_won")]
    public int GamesWon { get; set; }

    [JsonPropertyName("total_score_change")]
    public long TotalScoreChange { get; set; }
}
