using Game.Mahjong.Analysis;
using Game.Mahjong.Hands;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Standard.Scoring;

public static class StandardMahjongScorer
{
    private static readonly MahjongWinningOptions WinningOptions = new(
        AllowSevenPairs: true,
        AllowThirteenOrphans: true);

    public static StandardMahjongSettlement Calculate(
        IEnumerable<MahjongTileKind> concealedWinningKinds,
        IReadOnlyList<MahjongMeld> melds,
        MahjongSeat winner,
        MahjongSeat? discardSource,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(concealedWinningKinds);
        ArgumentNullException.ThrowIfNull(melds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);

        var concealedKinds = concealedWinningKinds.ToArray();
        var shapes = MahjongHandAnalyzer.Analyze(concealedKinds, melds.Count, WinningOptions);
        if (shapes.Count == 0)
        {
            throw new ArgumentException("The supplied hand is not a winning standard Mahjong hand.");
        }

        var selfDraw = discardSource is null;
        var scoredShapes = shapes
            .Select(shape => ScoreShape(shape, concealedKinds, melds, winner, selfDraw))
            .OrderByDescending(result => result.Fan)
            .ThenBy(result => string.Join('|', result.Patterns), StringComparer.Ordinal)
            .ToArray();
        var best = scoredShapes[0];
        var unit = checked((long)baseScore << (Math.Min(best.Fan, 13) - 1));
        var scoreChanges = new long[4];

        if (selfDraw)
        {
            for (var payerIndex = 0; payerIndex < 4; payerIndex++)
            {
                var payer = (MahjongSeat)payerIndex;
                if (payer == winner)
                {
                    continue;
                }

                var payment = winner == MahjongSeat.East || payer == MahjongSeat.East ? unit * 2 : unit;
                scoreChanges[payerIndex] -= payment;
                scoreChanges[(int)winner] += payment;
            }
        }
        else
        {
            var payer = discardSource!.Value;
            var payment = winner == MahjongSeat.East || payer == MahjongSeat.East ? unit * 2 : unit;
            scoreChanges[(int)payer] -= payment;
            scoreChanges[(int)winner] += payment;
        }

        return new StandardMahjongSettlement(
            false,
            winner,
            discardSource,
            selfDraw,
            best.Fan,
            best.Patterns,
            scoreChanges);
    }

    public static bool CanWin(IEnumerable<MahjongTileKind> concealedKinds, int meldCount)
    {
        return MahjongHandAnalyzer.IsWinning(concealedKinds, meldCount, WinningOptions);
    }

    private static ScoredShape ScoreShape(
        MahjongWinningShape shape,
        IReadOnlyList<MahjongTileKind> concealedKinds,
        IReadOnlyList<MahjongMeld> melds,
        MahjongSeat winner,
        bool selfDraw)
    {
        var patterns = new List<string> { "和牌" };
        var fan = 1;

        if (selfDraw)
        {
            fan++;
            patterns.Add("自摸");
        }

        if (melds.All(meld => !meld.IsOpen))
        {
            fan++;
            patterns.Add("门前清");
        }

        if (shape.Kind == MahjongWinningShapeKind.SevenPairs)
        {
            fan += 4;
            patterns.Add("七对");
        }
        else if (shape.Kind == MahjongWinningShapeKind.ThirteenOrphans)
        {
            fan += 13;
            patterns.Add("十三幺");
        }
        else if (shape.ConcealedGroups.All(group => group.Type == MahjongGroupType.Triplet)
            && melds.All(meld => meld.Type != MahjongMeldType.Chow))
        {
            fan += 2;
            patterns.Add("对对胡");
        }

        var allKinds = concealedKinds.Concat(melds.SelectMany(meld => meld.Tiles.Select(tile => tile.Kind))).ToArray();
        var suitedSuits = allKinds
            .Where(kind => kind.IsSuited())
            .Select(kind => kind.GetSuit())
            .Distinct()
            .ToArray();
        if (suitedSuits.Length == 1 && allKinds.All(kind => kind.IsSuited()))
        {
            fan += 6;
            patterns.Add("清一色");
        }
        else if (suitedSuits.Length == 1 && allKinds.Any(kind => kind.IsHonor()))
        {
            fan += 3;
            patterns.Add("混一色");
        }

        var tripletKinds = shape.ConcealedGroups
            .Where(group => group.Type == MahjongGroupType.Triplet)
            .Select(group => group.FirstKind)
            .Concat(melds
                .Where(meld => meld.Type != MahjongMeldType.Chow)
                .Select(meld => meld.Tiles[0].Kind))
            .ToArray();
        foreach (var dragon in new[] { MahjongTileKind.White, MahjongTileKind.Green, MahjongTileKind.Red })
        {
            if (tripletKinds.Contains(dragon))
            {
                fan++;
                patterns.Add($"三元刻:{dragon}");
            }
        }

        var seatWind = (MahjongTileKind)((int)MahjongTileKind.East + (int)winner);
        if (tripletKinds.Contains(seatWind))
        {
            fan++;
            patterns.Add("自风刻");
        }

        if (tripletKinds.Contains(MahjongTileKind.East))
        {
            fan++;
            patterns.Add("场风刻");
        }

        return new ScoredShape(fan, patterns);
    }

    private sealed record ScoredShape(int Fan, IReadOnlyList<string> Patterns);
}
