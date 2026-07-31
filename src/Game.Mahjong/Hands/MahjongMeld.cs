using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Hands;

public enum MahjongMeldType
{
    Chow,
    Pong,
    OpenKong,
    ConcealedKong,
    AddedKong,
}

public sealed class MahjongMeld
{
    public MahjongMeld(
        MahjongMeldType type,
        IEnumerable<MahjongTile> tiles,
        MahjongSeat? sourceSeat = null)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var materializedTiles = tiles
            .OrderBy(tile => tile.Kind)
            .ThenBy(tile => tile.CopyIndex)
            .ToArray();
        if (!IsValid(type, materializedTiles, sourceSeat))
        {
            throw new ArgumentException("Tiles do not form the requested meld.", nameof(tiles));
        }

        Type = type;
        Tiles = Array.AsReadOnly(materializedTiles);
        SourceSeat = sourceSeat;
    }

    public MahjongMeldType Type { get; }

    public IReadOnlyList<MahjongTile> Tiles { get; }

    public MahjongSeat? SourceSeat { get; }

    public bool IsOpen => Type != MahjongMeldType.ConcealedKong;

    private static bool IsValid(
        MahjongMeldType type,
        IReadOnlyList<MahjongTile> tiles,
        MahjongSeat? sourceSeat)
    {
        if (tiles.Distinct().Count() != tiles.Count)
        {
            return false;
        }

        return type switch
        {
            MahjongMeldType.Chow => sourceSeat is not null
                && tiles.Count == 3
                && tiles.All(tile => tile.Kind.IsSuited())
                && tiles.Select(tile => tile.Kind.GetSuit()).Distinct().Count() == 1
                && tiles[1].Kind == (MahjongTileKind)((int)tiles[0].Kind + 1)
                && tiles[2].Kind == (MahjongTileKind)((int)tiles[0].Kind + 2),
            MahjongMeldType.Pong => sourceSeat is not null
                && tiles.Count == 3
                && tiles.Select(tile => tile.Kind).Distinct().Count() == 1,
            MahjongMeldType.OpenKong or MahjongMeldType.AddedKong => sourceSeat is not null
                && tiles.Count == 4
                && tiles.Select(tile => tile.Kind).Distinct().Count() == 1,
            MahjongMeldType.ConcealedKong => sourceSeat is null
                && tiles.Count == 4
                && tiles.Select(tile => tile.Kind).Distinct().Count() == 1,
            _ => false,
        };
    }
}
