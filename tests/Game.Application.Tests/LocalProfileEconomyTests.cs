using Game.Application.Mahjong;
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

    [Theory]
    [InlineData(MahjongMode.Standard, 120L, true, 120L)]
    [InlineData(MahjongMode.Sichuan, -40L, false, -40L)]
    [InlineData(MahjongMode.Riichi, 2400L, true, 24L)]
    public void Mahjong_result_updates_only_its_mode_and_applies_local_bean_feedback(
        MahjongMode mode,
        long scoreChange,
        bool won,
        long expectedBeanChange)
    {
        var profile = new LocalPlayerProfile
        {
            Beans = 1_000,
            ActiveMahjong = MahjongSessionFactory.Start(mode, 9).CreateRecoveryState(),
        };

        var actualBeanChange = LocalProfileEconomy.ApplyMahjongOutcome(
            profile,
            mode,
            new MahjongLocalOutcome(scoreChange, won));

        var statistics = profile.MahjongStatistics.For(mode);
        Assert.Equal(expectedBeanChange, actualBeanChange);
        Assert.Equal(1_000 + expectedBeanChange, profile.Beans);
        Assert.Equal(1, statistics.GamesPlayed);
        Assert.Equal(won ? 1 : 0, statistics.GamesWon);
        Assert.Equal(scoreChange, statistics.TotalScoreChange);
        Assert.Null(profile.ActiveMahjong);
        Assert.Equal(1, new[]
        {
            profile.MahjongStatistics.Standard,
            profile.MahjongStatistics.Sichuan,
            profile.MahjongStatistics.Riichi,
        }.Count(item => item.GamesPlayed == 1));
    }
}
