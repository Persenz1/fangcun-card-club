using Game.Mahjong.Hands;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Table;

public sealed class MahjongTableSnapshot
{
    public MahjongTableSnapshot(
        MahjongSeat dealer,
        MahjongSeat currentSeat,
        IEnumerable<IEnumerable<MahjongTile>> hands,
        IEnumerable<IEnumerable<MahjongMeld>> melds,
        IEnumerable<IEnumerable<MahjongRiverTile>> rivers,
        MahjongTile? lastDrawnTile,
        MahjongSeat? lastDiscardSeat,
        MahjongRiverTile? lastDiscard,
        int liveTilesRemaining,
        int replacementTilesRemaining)
    {
        Dealer = dealer;
        CurrentSeat = currentSeat;
        Hands = Array.AsReadOnly(hands
            .Select(hand => (IReadOnlyList<MahjongTile>)Array.AsReadOnly(hand.ToArray()))
            .ToArray());
        Melds = Array.AsReadOnly(melds
            .Select(seatMelds => (IReadOnlyList<MahjongMeld>)Array.AsReadOnly(seatMelds.ToArray()))
            .ToArray());
        Rivers = Array.AsReadOnly(rivers
            .Select(river => (IReadOnlyList<MahjongRiverTile>)Array.AsReadOnly(river.ToArray()))
            .ToArray());
        LastDrawnTile = lastDrawnTile;
        LastDiscardSeat = lastDiscardSeat;
        LastDiscard = lastDiscard;
        LiveTilesRemaining = liveTilesRemaining;
        ReplacementTilesRemaining = replacementTilesRemaining;
    }

    public MahjongSeat Dealer { get; }

    public MahjongSeat CurrentSeat { get; }

    public IReadOnlyList<IReadOnlyList<MahjongTile>> Hands { get; }

    public IReadOnlyList<IReadOnlyList<MahjongMeld>> Melds { get; }

    public IReadOnlyList<IReadOnlyList<MahjongRiverTile>> Rivers { get; }

    public MahjongTile? LastDrawnTile { get; }

    public MahjongSeat? LastDiscardSeat { get; }

    public MahjongRiverTile? LastDiscard { get; }

    public int LiveTilesRemaining { get; }

    public int ReplacementTilesRemaining { get; }
}
