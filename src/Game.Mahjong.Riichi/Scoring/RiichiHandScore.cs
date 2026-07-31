using Game.Mahjong.Table;

namespace Game.Mahjong.Riichi.Scoring;

public sealed class RiichiHandScore
{
    public RiichiHandScore(
        int han,
        int fu,
        int yakumanCount,
        int doraCount,
        long basicPoints,
        string limitName,
        IEnumerable<string> yaku)
    {
        Han = han;
        Fu = fu;
        YakumanCount = yakumanCount;
        DoraCount = doraCount;
        BasicPoints = basicPoints;
        LimitName = limitName;
        Yaku = Array.AsReadOnly(yaku.ToArray());
    }

    public int Han { get; }

    public int Fu { get; }

    public int YakumanCount { get; }

    public int DoraCount { get; }

    public long BasicPoints { get; }

    public string LimitName { get; }

    public IReadOnlyList<string> Yaku { get; }
}

public sealed class RiichiWinResult
{
    public RiichiWinResult(
        MahjongSeat winner,
        MahjongSeat? discardSource,
        RiichiHandScore handScore,
        IEnumerable<long> scoreChanges)
    {
        Winner = winner;
        DiscardSource = discardSource;
        HandScore = handScore;
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
    }

    public MahjongSeat Winner { get; }

    public MahjongSeat? DiscardSource { get; }

    public bool SelfDraw => DiscardSource is null;

    public RiichiHandScore HandScore { get; }

    public IReadOnlyList<long> ScoreChanges { get; }
}
