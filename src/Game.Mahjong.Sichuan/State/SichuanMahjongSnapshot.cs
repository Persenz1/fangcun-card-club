using Game.Mahjong.Sichuan.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.State;

public sealed class SichuanMahjongSnapshot
{
    public SichuanMahjongSnapshot(
        SichuanMahjongPhase phase,
        SichuanExchangeDirection exchangeDirection,
        MahjongTableSnapshot table,
        IEnumerable<bool> exchangeSubmitted,
        IEnumerable<MahjongTileSuit?> voidSuits,
        IEnumerable<bool> activeSeats,
        MahjongSeat? offeredReactionSeat,
        IEnumerable<long> scoreChanges,
        IEnumerable<SichuanWinResult> wins,
        SichuanMahjongSettlement? settlement)
    {
        Phase = phase;
        ExchangeDirection = exchangeDirection;
        Table = table;
        ExchangeSubmitted = Array.AsReadOnly(exchangeSubmitted.ToArray());
        VoidSuits = Array.AsReadOnly(voidSuits.ToArray());
        ActiveSeats = Array.AsReadOnly(activeSeats.ToArray());
        OfferedReactionSeat = offeredReactionSeat;
        ScoreChanges = Array.AsReadOnly(scoreChanges.ToArray());
        Wins = Array.AsReadOnly(wins.ToArray());
        Settlement = settlement;
    }

    public SichuanMahjongPhase Phase { get; }

    public SichuanExchangeDirection ExchangeDirection { get; }

    public MahjongTableSnapshot Table { get; }

    public IReadOnlyList<bool> ExchangeSubmitted { get; }

    public IReadOnlyList<MahjongTileSuit?> VoidSuits { get; }

    public IReadOnlyList<bool> ActiveSeats { get; }

    public MahjongSeat? OfferedReactionSeat { get; }

    public IReadOnlyList<long> ScoreChanges { get; }

    public IReadOnlyList<SichuanWinResult> Wins { get; }

    public SichuanMahjongSettlement? Settlement { get; }
}
