namespace Game.Mahjong.Sichuan.State;

public enum SichuanMahjongPhase
{
    ExchangeThree,
    DeclareVoidSuit,
    AwaitingDiscard,
    AwaitingReaction,
    Finished,
}

public enum SichuanExchangeDirection
{
    Clockwise = 1,
    Opposite = 2,
    CounterClockwise = 3,
}
