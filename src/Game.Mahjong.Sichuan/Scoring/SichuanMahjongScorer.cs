using Game.Mahjong.Analysis;
using Game.Mahjong.Hands;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.Scoring;

public static class SichuanMahjongScorer
{
    private static readonly MahjongWinningOptions WinningOptions = new(
        AllowSevenPairs: true,
        SevenPairsRequireDistinctKinds: false);

    public static bool CanWin(
        IEnumerable<MahjongTileKind> concealedKinds,
        int meldCount,
        MahjongTileSuit voidSuit)
    {
        ArgumentNullException.ThrowIfNull(concealedKinds);
        ValidateVoidSuit(voidSuit);
        var kinds = concealedKinds.ToArray();
        return kinds.All(kind => kind.GetSuit() != voidSuit)
            && MahjongHandAnalyzer.IsWinning(kinds, meldCount, WinningOptions);
    }

    public static SichuanHandScore Evaluate(
        IEnumerable<MahjongTileKind> concealedWinningKinds,
        IReadOnlyList<MahjongMeld> melds,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(concealedWinningKinds);
        ArgumentNullException.ThrowIfNull(melds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);

        var concealedKinds = concealedWinningKinds.ToArray();
        if (concealedKinds.Any(kind => kind.IsHonor())
            || melds.SelectMany(meld => meld.Tiles).Any(tile => tile.Kind.IsHonor()))
        {
            throw new ArgumentException("Sichuan Mahjong uses suited tiles only.");
        }

        var shapes = MahjongHandAnalyzer.Analyze(concealedKinds, melds.Count, WinningOptions);
        if (shapes.Count == 0)
        {
            throw new ArgumentException("The supplied hand is not a winning Sichuan Mahjong hand.");
        }

        var best = shapes
            .Select(shape => ScoreShape(shape, concealedKinds, melds, baseScore))
            .OrderByDescending(score => score.Fan)
            .ThenBy(score => string.Join('|', score.Patterns), StringComparer.Ordinal)
            .First();
        return best;
    }

    public static SichuanWinResult CalculateWin(
        IEnumerable<MahjongTileKind> concealedWinningKinds,
        IReadOnlyList<MahjongMeld> melds,
        MahjongSeat winner,
        MahjongSeat? discardSource,
        IEnumerable<MahjongSeat> activeSeats,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(activeSeats);
        var active = activeSeats.Distinct().ToArray();
        if (!active.Contains(winner)
            || discardSource is { } source && (!active.Contains(source) || source == winner))
        {
            throw new ArgumentException("Winner and discard source must be distinct active seats.");
        }

        var handScore = Evaluate(concealedWinningKinds, melds, baseScore);
        var changes = new long[4];
        IEnumerable<MahjongSeat> payers = discardSource is { } discardPayer
            ? [discardPayer]
            : active.Where(seat => seat != winner);
        foreach (var payer in payers)
        {
            changes[(int)payer] -= handScore.Unit;
            changes[(int)winner] += handScore.Unit;
        }

        return new SichuanWinResult(
            winner,
            discardSource,
            handScore.Fan,
            handScore.Patterns,
            handScore.Unit,
            changes);
    }

    public static IReadOnlyList<long> CalculateKongPayment(
        MahjongMeldType kongType,
        MahjongSeat declarer,
        MahjongSeat? discardSource,
        IEnumerable<MahjongSeat> activeSeats,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(activeSeats);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);
        var active = activeSeats.Distinct().ToArray();
        if (!active.Contains(declarer))
        {
            throw new ArgumentException("The kong declarer must be active.", nameof(activeSeats));
        }

        IEnumerable<MahjongSeat> payers;
        long payment;
        switch (kongType)
        {
            case MahjongMeldType.OpenKong
                when discardSource is { } source && source != declarer && active.Contains(source):
                payers = [source];
                payment = baseScore * 2L;
                break;
            case MahjongMeldType.AddedKong when discardSource is null:
                payers = active.Where(seat => seat != declarer);
                payment = baseScore;
                break;
            case MahjongMeldType.ConcealedKong when discardSource is null:
                payers = active.Where(seat => seat != declarer);
                payment = baseScore * 2L;
                break;
            default:
                throw new ArgumentException("The requested meld is not a payable Sichuan kong.", nameof(kongType));
        }

        var changes = new long[4];
        foreach (var payer in payers)
        {
            Transfer(changes, payer, declarer, payment);
        }

        return Array.AsReadOnly(changes);
    }

    public static IReadOnlyList<MahjongTileKind> GetWinningKinds(
        IEnumerable<MahjongTileKind> concealedKinds,
        int meldCount,
        MahjongTileSuit voidSuit)
    {
        ArgumentNullException.ThrowIfNull(concealedKinds);
        ValidateVoidSuit(voidSuit);
        var kinds = concealedKinds.ToArray();
        if (kinds.Any(kind => kind.GetSuit() == voidSuit))
        {
            return [];
        }

        return MahjongHandAnalyzer.GetWinningKinds(kinds, meldCount, WinningOptions)
            .Where(kind => kind.IsSuited() && kind.GetSuit() != voidSuit)
            .ToArray();
    }

    public static SichuanExhaustiveResult CalculateExhaustive(
        IReadOnlyDictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>> concealedHands,
        IReadOnlyDictionary<MahjongSeat, IReadOnlyList<MahjongMeld>> melds,
        IReadOnlyDictionary<MahjongSeat, MahjongTileSuit> voidSuits,
        IEnumerable<MahjongSeat> remainingSeats,
        int baseScore = 10)
    {
        ArgumentNullException.ThrowIfNull(concealedHands);
        ArgumentNullException.ThrowIfNull(melds);
        ArgumentNullException.ThrowIfNull(voidSuits);
        ArgumentNullException.ThrowIfNull(remainingSeats);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(baseScore);

        var remaining = remainingSeats.Distinct().ToArray();
        var changes = new long[4];
        var flowerPigs = remaining
            .Where(seat => concealedHands[seat].Any(kind => kind.GetSuit() == voidSuits[seat]))
            .ToArray();
        var nonFlowerPigs = remaining.Except(flowerPigs).ToArray();
        foreach (var flowerPig in flowerPigs)
        {
            foreach (var recipient in nonFlowerPigs)
            {
                Transfer(changes, flowerPig, recipient, baseScore * 16L);
            }
        }

        var waitScores = nonFlowerPigs
            .Select(seat => new
            {
                Seat = seat,
                Scores = GetWinningKinds(
                        concealedHands[seat],
                        melds[seat].Count,
                        voidSuits[seat])
                    .Select(wait => Evaluate(
                        concealedHands[seat].Append(wait),
                        melds[seat],
                        baseScore))
                    .ToArray(),
            })
            .Where(candidate => candidate.Scores.Length > 0)
            .ToDictionary(
                candidate => candidate.Seat,
                candidate => candidate.Scores.Max(score => score.Unit));
        var tenpaiSeats = waitScores.Keys.OrderBy(seat => seat).ToArray();
        foreach (var noten in nonFlowerPigs.Except(tenpaiSeats))
        {
            foreach (var (tenpai, unit) in waitScores)
            {
                Transfer(changes, noten, tenpai, unit);
            }
        }

        return new SichuanExhaustiveResult(changes, flowerPigs, tenpaiSeats);
    }

    private static void ValidateVoidSuit(MahjongTileSuit voidSuit)
    {
        if (!Enum.IsDefined(voidSuit) || voidSuit == MahjongTileSuit.Honors)
        {
            throw new ArgumentOutOfRangeException(nameof(voidSuit));
        }
    }

    private static SichuanHandScore ScoreShape(
        MahjongWinningShape shape,
        IReadOnlyList<MahjongTileKind> concealedKinds,
        IReadOnlyList<MahjongMeld> melds,
        int baseScore)
    {
        var fan = 0;
        var patterns = new List<string>();
        var allKinds = concealedKinds
            .Concat(melds.SelectMany(meld => meld.Tiles.Select(tile => tile.Kind)))
            .ToArray();

        if (shape.Kind == MahjongWinningShapeKind.SevenPairs)
        {
            var hasQuad = concealedKinds.GroupBy(kind => kind).Any(group => group.Count() == 4);
            fan += hasQuad ? 3 : 2;
            patterns.Add(hasQuad ? "龙七对" : "七对");
        }
        else
        {
            if (shape.ConcealedGroups.All(group => group.Type == MahjongGroupType.Triplet)
                && melds.All(meld => meld.Type != MahjongMeldType.Chow))
            {
                fan++;
                patterns.Add("对对胡");
            }

            if (melds.Count == 4 && melds.All(meld => meld.IsOpen))
            {
                fan += 2;
                patterns.Add("金钩钓");
            }
        }

        if (allKinds.Select(kind => kind.GetSuit()).Distinct().Count() == 1)
        {
            fan += 2;
            patterns.Add("清一色");
        }

        var roots = allKinds.GroupBy(kind => kind).Count(group => group.Count() == 4);
        if (roots > 0)
        {
            fan += roots;
            patterns.Add($"根×{roots}");
        }

        if (patterns.Count == 0)
        {
            patterns.Add("平胡");
        }

        var unit = checked((long)baseScore * (1L << Math.Min(fan, 8)));
        return new SichuanHandScore(fan, patterns, unit);
    }

    private static void Transfer(
        long[] changes,
        MahjongSeat payer,
        MahjongSeat recipient,
        long amount)
    {
        changes[(int)payer] -= amount;
        changes[(int)recipient] += amount;
    }
}
