using Game.Core.Simulation;
using Game.Mahjong.Hands;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Commands;

public sealed record DiscardMahjongTileCommand(int PlayerIndex, MahjongTile Tile) : IGameCommand;

public sealed class ClaimMahjongDiscardCommand : IGameCommand
{
    public ClaimMahjongDiscardCommand(
        int playerIndex,
        MahjongMeldType meldType,
        IEnumerable<MahjongTile> concealedTiles)
    {
        ArgumentNullException.ThrowIfNull(concealedTiles);
        PlayerIndex = playerIndex;
        MeldType = meldType;
        ConcealedTiles = Array.AsReadOnly(concealedTiles.ToArray());
    }

    public int PlayerIndex { get; }

    public MahjongMeldType MeldType { get; }

    public IReadOnlyList<MahjongTile> ConcealedTiles { get; }
}

public sealed class DeclareConcealedKongCommand : IGameCommand
{
    public DeclareConcealedKongCommand(int playerIndex, IEnumerable<MahjongTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        PlayerIndex = playerIndex;
        Tiles = Array.AsReadOnly(tiles.ToArray());
    }

    public int PlayerIndex { get; }

    public IReadOnlyList<MahjongTile> Tiles { get; }
}

public sealed record DeclareAddedKongCommand(int PlayerIndex, MahjongTile FourthTile) : IGameCommand;

public sealed record DeclareMahjongWinCommand(int PlayerIndex) : IGameCommand;

public sealed record PassMahjongCommand(int PlayerIndex) : IGameCommand;
