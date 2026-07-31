namespace Game.Doudizhu.Settlement;

public enum DoudizhuWinningTeam
{
    Landlord,
    Farmers,
}

public enum DoudizhuSpringKind
{
    None,
    Spring,
    CounterSpring,
}

public sealed class DoudizhuSettlement
{
    public DoudizhuSettlement(
        DoudizhuWinningTeam winningTeam,
        DoudizhuSpringKind springKind,
        int finalMultiplier,
        IEnumerable<long> scoreChanges)
    {
        WinningTeam = winningTeam;
        SpringKind = springKind;
        FinalMultiplier = finalMultiplier;
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
    }

    public DoudizhuWinningTeam WinningTeam { get; }

    public DoudizhuSpringKind SpringKind { get; }

    public int FinalMultiplier { get; }

    public IReadOnlyList<long> ScoreChanges { get; }
}
