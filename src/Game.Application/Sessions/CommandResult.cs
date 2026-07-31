using Game.Core.Simulation;

namespace Game.Application.Sessions;

public sealed record CommandResult<TSnapshot>(
    bool Accepted,
    TSnapshot Snapshot,
    IReadOnlyList<IGameEvent> Events,
    string? Error = null);
