using Game.Application.Profiles;
using Game.Doudizhu.Settlement;

namespace Game.Application.Tests;

public sealed class LocalProfileEconomyTests
{
    [Fact]
    public void Free_supply_is_immediate_only_below_minimum_entry()
    {
        var profile = new LocalPlayerProfile { Beans = 0 };

        Assert.True(LocalProfileEconomy.CanClaimFreeSupply(profile));
        Assert.True(LocalProfileEconomy.ClaimFreeSupply(profile));
        Assert.Equal(3_000, profile.Beans);
        Assert.False(LocalProfileEconomy.ClaimFreeSupply(profile));
    }

    [Fact]
    public void Applying_result_clamps_beans_at_zero_and_updates_statistics()
    {
        var profile = new LocalPlayerProfile { Beans = 5 };
        var settlement = new DoudizhuSettlement(
            DoudizhuWinningTeam.Farmers,
            DoudizhuSpringKind.None,
            1,
            [-20, 10, 10]);

        LocalProfileEconomy.ApplyDoudizhuSettlement(profile, settlement, 0, 0);

        Assert.Equal(0, profile.Beans);
        Assert.Equal(1, profile.DoudizhuStatistics.GamesPlayed);
        Assert.Equal(0, profile.DoudizhuStatistics.GamesWon);
        Assert.Null(profile.ActiveDoudizhu);
        Assert.True(LocalProfileEconomy.ClaimFreeSupply(profile));
    }
}
