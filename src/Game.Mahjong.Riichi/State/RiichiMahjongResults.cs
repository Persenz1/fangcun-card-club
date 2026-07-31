using Game.Mahjong.Riichi.Scoring;
using Game.Mahjong.Table;

namespace Game.Mahjong.Riichi.State;

public sealed class RiichiHandResult
{
    public RiichiHandResult(
        RiichiHandEndReason reason,
        IEnumerable<RiichiWinResult> wins,
        IEnumerable<MahjongSeat> tenpaiSeats,
        IEnumerable<long> scoreChanges,
        bool dealerRepeats)
    {
        Reason = reason;
        Wins = Array.AsReadOnly(wins.ToArray());
        TenpaiSeats = Array.AsReadOnly(tenpaiSeats.ToArray());
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
        DealerRepeats = dealerRepeats;
    }

    public RiichiHandEndReason Reason { get; }

    public IReadOnlyList<RiichiWinResult> Wins { get; }

    public IReadOnlyList<MahjongSeat> TenpaiSeats { get; }

    public IReadOnlyList<long> ScoreChanges { get; }

    public bool DealerRepeats { get; }
}

public sealed class RiichiMatchResult
{
    public RiichiMatchResult(IEnumerable<long> finalScores, IEnumerable<MahjongSeat> ranking)
    {
        FinalScores = Array.AsReadOnly(finalScores.ToArray());
        Ranking = Array.AsReadOnly(ranking.ToArray());
    }

    public IReadOnlyList<long> FinalScores { get; }

    public IReadOnlyList<MahjongSeat> Ranking { get; }
}
