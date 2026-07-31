namespace Game.Mahjong.Riichi.State;

public enum RiichiMahjongPhase
{
    AwaitingDiscard,
    AwaitingReaction,
    Finished,
}

public enum RiichiHandEndReason
{
    Ron,
    Tsumo,
    ExhaustiveDraw,
    NagashiMangan,
    NineTerminals,
    FourWinds,
    FourRiichi,
    FourKongs,
}
