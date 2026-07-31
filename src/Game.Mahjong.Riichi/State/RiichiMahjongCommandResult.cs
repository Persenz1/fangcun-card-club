using Game.Core.Simulation;

namespace Game.Mahjong.Riichi.State;

public sealed record RiichiMahjongCommandResult(
    bool Accepted,
    RiichiMahjongSnapshot Snapshot,
    IReadOnlyList<IGameEvent> Events,
    string? Error = null);
