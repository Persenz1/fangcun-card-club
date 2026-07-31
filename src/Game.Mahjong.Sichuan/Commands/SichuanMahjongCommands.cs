using Game.Core.Simulation;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.Commands;

public sealed class ExchangeThreeTilesCommand : IGameCommand
{
    public ExchangeThreeTilesCommand(int playerIndex, IEnumerable<MahjongTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        PlayerIndex = playerIndex;
        Tiles = Array.AsReadOnly(tiles.ToArray());
    }

    public int PlayerIndex { get; }

    public IReadOnlyList<MahjongTile> Tiles { get; }
}

public sealed record DeclareVoidSuitCommand(
    int PlayerIndex,
    MahjongTileSuit Suit) : IGameCommand;
