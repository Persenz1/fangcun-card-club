using Game.Core.Simulation;

namespace Game.Mahjong.Sichuan.State;

public sealed record SichuanMahjongCommandResult(
    bool Accepted,
    SichuanMahjongSnapshot Snapshot,
    IReadOnlyList<IGameEvent> Events,
    string? Error = null);
