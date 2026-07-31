using Game.Doudizhu.Settlement;

namespace Game.Doudizhu.Tests;

public sealed class SettlementCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 3, 0, DoudizhuWinningTeam.Landlord, DoudizhuSpringKind.Spring, 8, 160, -80, -80)]
    [InlineData(1, 2, 1, 4, DoudizhuWinningTeam.Farmers, DoudizhuSpringKind.CounterSpring, 8, 80, -160, 80)]
    [InlineData(2, 2, 2, 3, DoudizhuWinningTeam.Landlord, DoudizhuSpringKind.None, 4, -40, -40, 80)]
    public void Calculates_spring_and_zero_sum_changes(
        int landlordIndex,
        int winnerIndex,
        int landlordPlayCount,
        int farmerPlayCount,
        DoudizhuWinningTeam expectedTeam,
        DoudizhuSpringKind expectedSpring,
        int expectedMultiplier,
        long playerZeroChange,
        long playerOneChange,
        long playerTwoChange)
    {
        var playCounts = Enumerable.Repeat(farmerPlayCount, 3).ToArray();
        playCounts[landlordIndex] = landlordPlayCount;

        var settlement = SettlementCalculator.Calculate(10, 4, landlordIndex, winnerIndex, playCounts);

        Assert.Equal(expectedTeam, settlement.WinningTeam);
        Assert.Equal(expectedSpring, settlement.SpringKind);
        Assert.Equal(expectedMultiplier, settlement.FinalMultiplier);
        Assert.Equal([playerZeroChange, playerOneChange, playerTwoChange], settlement.ScoreChanges);
        Assert.Equal(0, settlement.ScoreChanges.Sum());
    }
}
