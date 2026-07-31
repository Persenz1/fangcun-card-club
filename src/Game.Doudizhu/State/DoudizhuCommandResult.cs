using Game.Core.Simulation;

namespace Game.Doudizhu.State;

public sealed record DoudizhuCommandResult(
    bool Accepted,
    DoudizhuSnapshot Snapshot,
    IReadOnlyList<IGameEvent> Events,
    string? Error = null);
