using Game.Mahjong.Table;

namespace Game.Mahjong.Sichuan.Scoring;

public sealed class SichuanHandScore
{
    public SichuanHandScore(int fan, IEnumerable<string> patterns, long unit)
    {
        Fan = fan;
        Patterns = Array.AsReadOnly(patterns.ToArray());
        Unit = unit;
    }

    public int Fan { get; }

    public IReadOnlyList<string> Patterns { get; }

    public long Unit { get; }
}

public sealed class SichuanWinResult
{
    public SichuanWinResult(
        MahjongSeat winner,
        MahjongSeat? discardSource,
        int fan,
        IEnumerable<string> patterns,
        long unit,
        IEnumerable<long> scoreChanges)
    {
        Winner = winner;
        DiscardSource = discardSource;
        Fan = fan;
        Patterns = Array.AsReadOnly(patterns.ToArray());
        Unit = unit;
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
    }

    public MahjongSeat Winner { get; }

    public MahjongSeat? DiscardSource { get; }

    public bool SelfDraw => DiscardSource is null;

    public int Fan { get; }

    public IReadOnlyList<string> Patterns { get; }

    public long Unit { get; }

    public IReadOnlyList<long> ScoreChanges { get; }
}

public sealed class SichuanExhaustiveResult
{
    public SichuanExhaustiveResult(
        IEnumerable<long> scoreChanges,
        IEnumerable<MahjongSeat> flowerPigSeats,
        IEnumerable<MahjongSeat> tenpaiSeats)
    {
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
        FlowerPigSeats = Array.AsReadOnly(flowerPigSeats.ToArray());
        TenpaiSeats = Array.AsReadOnly(tenpaiSeats.ToArray());
    }

    public IReadOnlyList<long> ScoreChanges { get; }

    public IReadOnlyList<MahjongSeat> FlowerPigSeats { get; }

    public IReadOnlyList<MahjongSeat> TenpaiSeats { get; }
}

public sealed class SichuanMahjongSettlement
{
    public SichuanMahjongSettlement(
        bool isExhaustiveDraw,
        IEnumerable<SichuanWinResult> wins,
        IEnumerable<long> scoreChanges,
        IEnumerable<MahjongSeat>? flowerPigSeats = null,
        IEnumerable<MahjongSeat>? tenpaiSeats = null)
    {
        IsExhaustiveDraw = isExhaustiveDraw;
        Wins = Array.AsReadOnly(wins.ToArray());
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
        FlowerPigSeats = Array.AsReadOnly((flowerPigSeats ?? []).ToArray());
        TenpaiSeats = Array.AsReadOnly((tenpaiSeats ?? []).ToArray());
    }

    public bool IsExhaustiveDraw { get; }

    public IReadOnlyList<SichuanWinResult> Wins { get; }

    public IReadOnlyList<long> ScoreChanges { get; }

    public IReadOnlyList<MahjongSeat> FlowerPigSeats { get; }

    public IReadOnlyList<MahjongSeat> TenpaiSeats { get; }
}
