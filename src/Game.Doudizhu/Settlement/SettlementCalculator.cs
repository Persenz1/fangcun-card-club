namespace Game.Doudizhu.Settlement;

public static class SettlementCalculator
{
    public static DoudizhuSettlement Calculate(
        int baseScore,
        int multiplier,
        int landlordIndex,
        int winnerIndex,
        IReadOnlyList<int> successfulPlayCounts)
    {
        var winningTeam = winnerIndex == landlordIndex
            ? DoudizhuWinningTeam.Landlord
            : DoudizhuWinningTeam.Farmers;
        var springKind = GetSpringKind(winningTeam, landlordIndex, successfulPlayCounts);
        var finalMultiplier = springKind == DoudizhuSpringKind.None
            ? multiplier
            : checked(multiplier * 2);
        var singleShare = checked((long)baseScore * finalMultiplier);
        var landlordChange = winningTeam == DoudizhuWinningTeam.Landlord
            ? singleShare * 2
            : -singleShare * 2;
        var farmerChange = -landlordChange / 2;
        var scoreChanges = Enumerable.Range(0, 3)
            .Select(playerIndex => playerIndex == landlordIndex ? landlordChange : farmerChange)
            .ToArray();

        return new DoudizhuSettlement(winningTeam, springKind, finalMultiplier, scoreChanges);
    }

    private static DoudizhuSpringKind GetSpringKind(
        DoudizhuWinningTeam winningTeam,
        int landlordIndex,
        IReadOnlyList<int> successfulPlayCounts)
    {
        if (winningTeam == DoudizhuWinningTeam.Landlord
            && successfulPlayCounts
                .Where((_, playerIndex) => playerIndex != landlordIndex)
                .All(count => count == 0))
        {
            return DoudizhuSpringKind.Spring;
        }

        if (winningTeam == DoudizhuWinningTeam.Farmers
            && successfulPlayCounts[landlordIndex] == 1)
        {
            return DoudizhuSpringKind.CounterSpring;
        }

        return DoudizhuSpringKind.None;
    }
}
