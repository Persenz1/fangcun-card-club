using System.Text.Json.Serialization;
using Game.Application.Doudizhu;

namespace Game.Application.Profiles;

public sealed class LocalPlayerProfile
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("beans")]
    public long Beans { get; set; } = LocalProfileEconomy.SupplyAmount;

    [JsonPropertyName("doudizhu_statistics")]
    public DoudizhuStatistics DoudizhuStatistics { get; set; } = new();

    [JsonPropertyName("active_doudizhu")]
    public DoudizhuRecoveryState? ActiveDoudizhu { get; set; }
}

public sealed class DoudizhuStatistics
{
    [JsonPropertyName("games_played")]
    public int GamesPlayed { get; set; }

    [JsonPropertyName("games_won")]
    public int GamesWon { get; set; }
}
