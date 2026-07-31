using Game.Core.Random;

namespace Game.Mahjong.Tiles;

public static class MahjongTileSet
{
    public static IReadOnlyList<MahjongTile> CreateOrdered()
    {
        return CreateKinds(MahjongTileKinds.All);
    }

    public static IReadOnlyList<MahjongTile> CreateSuitedOrdered()
    {
        return CreateKinds(MahjongTileKinds.All.Where(kind => kind.IsSuited()));
    }

    public static IReadOnlyList<MahjongTile> CreateSuitedShuffled(IDeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        return Shuffle(CreateSuitedOrdered(), random);
    }

    private static IReadOnlyList<MahjongTile> CreateKinds(IEnumerable<MahjongTileKind> kinds)
    {
        return kinds
            .SelectMany(kind => Enumerable.Range(0, 4)
                .Select(copyIndex => new MahjongTile(kind, (byte)copyIndex)))
            .ToArray();
    }

    public static IReadOnlyList<MahjongTile> CreateShuffled(IDeterministicRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return Shuffle(CreateOrdered(), random);
    }

    private static IReadOnlyList<MahjongTile> Shuffle(
        IReadOnlyList<MahjongTile> source,
        IDeterministicRandom random)
    {
        var tiles = source.ToArray();
        for (var index = tiles.Length - 1; index > 0; index--)
        {
            var otherIndex = random.NextInt(index + 1);
            (tiles[index], tiles[otherIndex]) = (tiles[otherIndex], tiles[index]);
        }

        return tiles;
    }
}
