using Game.Mahjong.Table;

namespace Game.Mahjong.Standard.Scoring;

public sealed class StandardMahjongSettlement
{
    public StandardMahjongSettlement(
        bool isDraw,
        MahjongSeat? winner,
        MahjongSeat? discardSource,
        bool selfDraw,
        int fan,
        IEnumerable<string> patterns,
        IEnumerable<long> scoreChanges)
    {
        IsDraw = isDraw;
        Winner = winner;
        DiscardSource = discardSource;
        SelfDraw = selfDraw;
        Fan = fan;
        Patterns = Array.AsReadOnly(patterns.ToArray());
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
    }

    public bool IsDraw { get; }

    public MahjongSeat? Winner { get; }

    public MahjongSeat? DiscardSource { get; }

    public bool SelfDraw { get; }

    public int Fan { get; }

    public IReadOnlyList<string> Patterns { get; }

    public IReadOnlyList<long> ScoreChanges { get; }

    public static StandardMahjongSettlement Draw()
    {
        return new StandardMahjongSettlement(true, null, null, false, 0, [], [0, 0, 0, 0]);
    }
}
