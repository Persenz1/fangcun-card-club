using Game.Mahjong.Tiles;

namespace Game.Mahjong.Analysis;

public static class MahjongHandAnalyzer
{
    private static readonly MahjongTileKind[] OrphanKinds = MahjongTileKinds.All
        .Where(kind => kind.IsTerminalOrHonor())
        .ToArray();

    public static IReadOnlyList<MahjongWinningShape> Analyze(
        IEnumerable<MahjongTileKind> concealedKinds,
        int openMeldCount = 0,
        MahjongWinningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(concealedKinds);
        if (openMeldCount is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(openMeldCount));
        }

        options ??= new MahjongWinningOptions();
        var counts = CreateCounts(concealedKinds);
        var concealedTileCount = counts.Sum();
        var requiredGroupCount = 4 - openMeldCount;
        if (concealedTileCount != (requiredGroupCount * 3) + 2)
        {
            return [];
        }

        var shapes = FindStandardShapes(counts, requiredGroupCount).ToList();
        if (openMeldCount == 0
            && options.AllowSevenPairs
            && IsSevenPairs(counts, options.SevenPairsRequireDistinctKinds))
        {
            shapes.Add(new MahjongWinningShape(MahjongWinningShapeKind.SevenPairs));
        }

        if (openMeldCount == 0 && options.AllowThirteenOrphans && IsThirteenOrphans(counts))
        {
            shapes.Add(new MahjongWinningShape(MahjongWinningShapeKind.ThirteenOrphans));
        }

        return shapes;
    }

    public static bool IsWinning(
        IEnumerable<MahjongTileKind> concealedKinds,
        int openMeldCount = 0,
        MahjongWinningOptions? options = null)
    {
        return Analyze(concealedKinds, openMeldCount, options).Count > 0;
    }

    public static IReadOnlyList<MahjongTileKind> GetWinningKinds(
        IEnumerable<MahjongTileKind> concealedKinds,
        int openMeldCount = 0,
        MahjongWinningOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(concealedKinds);
        var kinds = concealedKinds.ToArray();
        var counts = CreateCounts(kinds);
        var winningKinds = new List<MahjongTileKind>();

        foreach (var candidate in MahjongTileKinds.All)
        {
            if (counts[(int)candidate] == 4)
            {
                continue;
            }

            if (IsWinning(kinds.Append(candidate), openMeldCount, options))
            {
                winningKinds.Add(candidate);
            }
        }

        return winningKinds;
    }

    private static IEnumerable<MahjongWinningShape> FindStandardShapes(int[] counts, int requiredGroupCount)
    {
        for (var pairIndex = 0; pairIndex < counts.Length; pairIndex++)
        {
            if (counts[pairIndex] < 2)
            {
                continue;
            }

            counts[pairIndex] -= 2;
            var groups = new List<MahjongGroup>(requiredGroupCount);
            foreach (var completedGroups in FindGroups(counts, requiredGroupCount, groups))
            {
                yield return new MahjongWinningShape(
                    MahjongWinningShapeKind.Standard,
                    (MahjongTileKind)pairIndex,
                    completedGroups);
            }

            counts[pairIndex] += 2;
        }
    }

    private static IEnumerable<IReadOnlyList<MahjongGroup>> FindGroups(
        int[] counts,
        int requiredGroupCount,
        List<MahjongGroup> groups)
    {
        var firstIndex = Array.FindIndex(counts, count => count > 0);
        if (firstIndex < 0)
        {
            if (groups.Count == requiredGroupCount)
            {
                yield return groups.ToArray();
            }

            yield break;
        }

        if (groups.Count >= requiredGroupCount)
        {
            yield break;
        }

        var kind = (MahjongTileKind)firstIndex;
        if (counts[firstIndex] >= 3)
        {
            counts[firstIndex] -= 3;
            groups.Add(new MahjongGroup(MahjongGroupType.Triplet, kind));
            foreach (var result in FindGroups(counts, requiredGroupCount, groups))
            {
                yield return result;
            }

            groups.RemoveAt(groups.Count - 1);
            counts[firstIndex] += 3;
        }

        if (kind.IsSuited()
            && kind.GetNumber() <= 7
            && counts[firstIndex + 1] > 0
            && counts[firstIndex + 2] > 0)
        {
            counts[firstIndex]--;
            counts[firstIndex + 1]--;
            counts[firstIndex + 2]--;
            groups.Add(new MahjongGroup(MahjongGroupType.Sequence, kind));
            foreach (var result in FindGroups(counts, requiredGroupCount, groups))
            {
                yield return result;
            }

            groups.RemoveAt(groups.Count - 1);
            counts[firstIndex]++;
            counts[firstIndex + 1]++;
            counts[firstIndex + 2]++;
        }
    }

    private static bool IsSevenPairs(IReadOnlyList<int> counts, bool requireDistinctKinds)
    {
        return requireDistinctKinds
            ? counts.Count(count => count == 2) == 7
            : counts.All(count => count % 2 == 0) && counts.Sum(count => count / 2) == 7;
    }

    private static bool IsThirteenOrphans(IReadOnlyList<int> counts)
    {
        return OrphanKinds.All(kind => counts[(int)kind] >= 1)
            && OrphanKinds.Count(kind => counts[(int)kind] == 2) == 1
            && counts.Where((_, index) => !((MahjongTileKind)index).IsTerminalOrHonor()).All(count => count == 0);
    }

    private static int[] CreateCounts(IEnumerable<MahjongTileKind> kinds)
    {
        var counts = new int[MahjongTileKinds.KindCount];
        foreach (var kind in kinds)
        {
            if (!Enum.IsDefined(kind) || ++counts[(int)kind] > 4)
            {
                throw new ArgumentException("A hand contains an invalid tile kind or more than four copies.", nameof(kinds));
            }
        }

        return counts;
    }
}
