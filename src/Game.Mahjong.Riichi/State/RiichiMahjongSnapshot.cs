using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.State;

public sealed class RiichiMahjongSnapshot
{
    public RiichiMahjongSnapshot(
        RiichiMahjongPhase phase,
        RiichiRoundWind roundWind,
        int handNumber,
        MahjongSeat dealer,
        int honba,
        int riichiSticks,
        IEnumerable<long> scores,
        MahjongTableSnapshot table,
        IEnumerable<bool> riichiDeclared,
        IEnumerable<bool> doubleRiichiDeclared,
        IEnumerable<bool> furitenSeats,
        MahjongSeat? pendingRiichiSeat,
        MahjongSeat? offeredReactionSeat,
        IEnumerable<MahjongTileKind> doraIndicators,
        RiichiHandResult? lastHandResult,
        RiichiMatchResult? matchResult)
    {
        Phase = phase;
        RoundWind = roundWind;
        HandNumber = handNumber;
        Dealer = dealer;
        Honba = honba;
        RiichiSticks = riichiSticks;
        Scores = Array.AsReadOnly(scores.ToArray());
        Table = table;
        RiichiDeclared = Array.AsReadOnly(riichiDeclared.ToArray());
        DoubleRiichiDeclared = Array.AsReadOnly(doubleRiichiDeclared.ToArray());
        FuritenSeats = Array.AsReadOnly(furitenSeats.ToArray());
        PendingRiichiSeat = pendingRiichiSeat;
        OfferedReactionSeat = offeredReactionSeat;
        DoraIndicators = Array.AsReadOnly(doraIndicators.ToArray());
        LastHandResult = lastHandResult;
        MatchResult = matchResult;
    }

    public RiichiMahjongPhase Phase { get; }

    public RiichiRoundWind RoundWind { get; }

    public int HandNumber { get; }

    public MahjongSeat Dealer { get; }

    public int Honba { get; }

    public int RiichiSticks { get; }

    public IReadOnlyList<long> Scores { get; }

    public MahjongTableSnapshot Table { get; }

    public IReadOnlyList<bool> RiichiDeclared { get; }

    public IReadOnlyList<bool> DoubleRiichiDeclared { get; }

    public IReadOnlyList<bool> FuritenSeats { get; }

    public MahjongSeat? PendingRiichiSeat { get; }

    public MahjongSeat? OfferedReactionSeat { get; }

    public IReadOnlyList<MahjongTileKind> DoraIndicators { get; }

    public RiichiHandResult? LastHandResult { get; }

    public RiichiMatchResult? MatchResult { get; }
}
