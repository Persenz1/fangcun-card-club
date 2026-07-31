using Game.Core.Random;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Table;

public sealed class MahjongWall
{
    private readonly int _deadWallSize;
    private readonly int _replacementLimit;
    private readonly MahjongTile[] _tiles;
    private int _liveDrawIndex;
    private int _liveEndExclusive;
    private int _replacementDrawCount;

    public MahjongWall(
        IDeterministicRandom random,
        int deadWallSize = 0,
        int replacementLimit = 4)
        : this(MahjongTileSet.CreateShuffled(random), deadWallSize, replacementLimit)
    {
    }

    public MahjongWall(
        IEnumerable<MahjongTile> tiles,
        int deadWallSize = 0,
        int replacementLimit = 4)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        _tiles = tiles.ToArray();
        if (_tiles.Length != 136 || _tiles.Distinct().Count() != 136)
        {
            throw new ArgumentException("A Mahjong wall must contain 136 unique physical tiles.", nameof(tiles));
        }

        if (deadWallSize is < 0 or > 14)
        {
            throw new ArgumentOutOfRangeException(nameof(deadWallSize));
        }

        if (replacementLimit is < 0 or > 4 || replacementLimit > deadWallSize && deadWallSize > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(replacementLimit));
        }

        _deadWallSize = deadWallSize;
        _replacementLimit = replacementLimit;
        _liveEndExclusive = _tiles.Length - deadWallSize;
    }

    public int LiveTilesRemaining => _liveEndExclusive - _liveDrawIndex;

    public int ReplacementTilesRemaining => _replacementLimit - _replacementDrawCount;

    public IReadOnlyList<MahjongTile> DeadWall => Array.AsReadOnly(
        _tiles.Skip(_tiles.Length - _deadWallSize).ToArray());

    public MahjongTile DrawLive()
    {
        if (LiveTilesRemaining == 0)
        {
            throw new InvalidOperationException("The live wall is empty.");
        }

        return _tiles[_liveDrawIndex++];
    }

    public MahjongTile DrawReplacement()
    {
        if (ReplacementTilesRemaining == 0 || LiveTilesRemaining == 0)
        {
            throw new InvalidOperationException("No replacement draw is available.");
        }

        var tile = _tiles[_tiles.Length - 1 - _replacementDrawCount];
        _replacementDrawCount++;
        _liveEndExclusive--;
        return tile;
    }
}
