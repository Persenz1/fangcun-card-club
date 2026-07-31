using Game.Mahjong.Standard.Scoring;
using Game.Mahjong.Table;

namespace Game.Mahjong.Standard.State;

public sealed record StandardMahjongSnapshot(
    StandardMahjongPhase Phase,
    MahjongTableSnapshot Table,
    MahjongSeat? OfferedReactionSeat,
    StandardMahjongSettlement? Settlement);
