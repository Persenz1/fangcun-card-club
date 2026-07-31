using Game.Core.Simulation;

namespace Game.Mahjong.Standard.State;

public sealed record StandardMahjongCommandResult(
    bool Accepted,
    StandardMahjongSnapshot Snapshot,
    IReadOnlyList<IGameEvent> Events,
    string? Error = null);
