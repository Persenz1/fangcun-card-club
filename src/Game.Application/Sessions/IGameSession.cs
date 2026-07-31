using Game.Core.Simulation;

namespace Game.Application.Sessions;

/// <summary>
/// Boundary consumed by the Godot presentation layer.
/// </summary>
public interface IGameSession<TSnapshot>
{
    TSnapshot Snapshot { get; }

    CommandResult<TSnapshot> Dispatch(IGameCommand command);
}
