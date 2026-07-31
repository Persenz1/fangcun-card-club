using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Scoring;

public sealed class RiichiWinContext
{
    public RiichiWinContext(
        MahjongSeat winner,
        MahjongSeat dealer,
        RiichiRoundWind roundWind,
        MahjongTileKind winningKind,
        bool selfDraw,
        bool isRiichi = false,
        bool isDoubleRiichi = false,
        bool isIppatsu = false,
        bool isRinshan = false,
        bool isChankan = false,
        bool isHaitei = false,
        bool isHoutei = false,
        bool isTenhou = false,
        bool isChiihou = false,
        IEnumerable<MahjongTileKind>? doraIndicators = null,
        IEnumerable<MahjongTileKind>? uraDoraIndicators = null,
        int honba = 0,
        int riichiSticksAwarded = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(honba);
        ArgumentOutOfRangeException.ThrowIfNegative(riichiSticksAwarded);
        Winner = winner;
        Dealer = dealer;
        RoundWind = roundWind;
        WinningKind = winningKind;
        SelfDraw = selfDraw;
        IsRiichi = isRiichi;
        IsDoubleRiichi = isDoubleRiichi;
        IsIppatsu = isIppatsu;
        IsRinshan = isRinshan;
        IsChankan = isChankan;
        IsHaitei = isHaitei;
        IsHoutei = isHoutei;
        IsTenhou = isTenhou;
        IsChiihou = isChiihou;
        DoraIndicators = Array.AsReadOnly((doraIndicators ?? []).ToArray());
        UraDoraIndicators = Array.AsReadOnly((uraDoraIndicators ?? []).ToArray());
        Honba = honba;
        RiichiSticksAwarded = riichiSticksAwarded;
    }

    public MahjongSeat Winner { get; }

    public MahjongSeat Dealer { get; }

    public RiichiRoundWind RoundWind { get; }

    public MahjongTileKind WinningKind { get; }

    public bool SelfDraw { get; }

    public bool IsRiichi { get; }

    public bool IsDoubleRiichi { get; }

    public bool IsIppatsu { get; }

    public bool IsRinshan { get; }

    public bool IsChankan { get; }

    public bool IsHaitei { get; }

    public bool IsHoutei { get; }

    public bool IsTenhou { get; }

    public bool IsChiihou { get; }

    public IReadOnlyList<MahjongTileKind> DoraIndicators { get; }

    public IReadOnlyList<MahjongTileKind> UraDoraIndicators { get; }

    public int Honba { get; }

    public int RiichiSticksAwarded { get; }
}
