using Game.Core.Simulation;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Commands;

public sealed record DeclareRiichiCommand(
    int PlayerIndex,
    MahjongTile DiscardTile) : IGameCommand;

public sealed record DeclareNineTerminalsDrawCommand(
    int PlayerIndex) : IGameCommand;
