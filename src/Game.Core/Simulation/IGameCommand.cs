namespace Game.Core.Simulation;

/// <summary>
/// A player's intent. Rules must validate a command before changing state.
/// </summary>
public interface IGameCommand
{
    int PlayerIndex { get; }
}
