using Game.Core.Random;

namespace Game.Mahjong.Tiles;

public static class MahjongTileSet
{
    public static IReadOnlyList<MahjongTile> CreateOrdered()
    {
        return MahjongTileKinds.All
            .SelectMany(kind => Enumerable.Range(0, 4)
                .Select(copyIndex => new MahjongTile(kind, (byte)copyIndex)))
            .ToArray();
    }

    public static IReadOnlyList<MahjongTile> CreateShuffled(IDeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var tiles = CreateOrdered().ToArray();
        for (var index = tiles.Length - 1; index > 0; index--)
        {
            var otherIndex = random.NextInt(index + 1);
            (tiles[index], tiles[otherIndex]) = (tiles[otherIndex], tiles[index]);
        }

        return tiles;
    }
}
