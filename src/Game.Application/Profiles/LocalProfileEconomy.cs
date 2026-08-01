using Game.Application.Mahjong;
using Game.Doudizhu.Settlement;

namespace Game.Application.Profiles;

public static class LocalProfileEconomy
{
    public const long SupplyAmount = 3_000;
    public const long MinimumTableEntry = 10;

    public static bool CanClaimFreeSupply(LocalPlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.Beans < MinimumTableEntry;
    }

    public static bool ClaimFreeSupply(LocalPlayerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!CanClaimFreeSupply(profile))
        {
            return false;
        }

        profile.Beans = SupplyAmount;
        return true;
    }

    public static void ApplyDoudizhuSettlement(
        LocalPlayerProfile profile,
        DoudizhuSettlement settlement,
        int playerIndex,
        int landlordIndex)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentOutOfRangeException.ThrowIfLessThan(playerIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(playerIndex, 3);

        profile.Beans = Math.Max(0, profile.Beans + settlement.ScoreChanges[playerIndex]);
        profile.DoudizhuStatistics.GamesPlayed++;

        var playerWon = settlement.WinningTeam == DoudizhuWinningTeam.Landlord
            ? playerIndex == landlordIndex
            : playerIndex != landlordIndex;
        if (playerWon)
        {
            profile.DoudizhuStatistics.GamesWon++;
        }

        profile.ActiveDoudizhu = null;
    }

    public static long ApplyMahjongOutcome(
        LocalPlayerProfile profile,
        MahjongMode mode,
        MahjongLocalOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(outcome);
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var statistics = profile.MahjongStatistics.For(mode);
        statistics.GamesPlayed++;
        statistics.TotalScoreChange += outcome.ScoreChange;
        if (outcome.Won)
        {
            statistics.GamesWon++;
        }

        var beanChange = mode == MahjongMode.Riichi
            ? outcome.ScoreChange / 100
            : outcome.ScoreChange;
        var previousBeans = profile.Beans;
        profile.Beans = Math.Max(0, profile.Beans + beanChange);
        profile.ActiveMahjong = null;
        return profile.Beans - previousBeans;
    }
}
