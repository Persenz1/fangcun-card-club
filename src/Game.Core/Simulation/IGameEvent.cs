namespace Game.Core.Simulation;

/// <summary>
/// An accepted, immutable fact produced by a rule engine.
/// </summary>
public interface IGameEvent
{
    long Sequence { get; }
}
