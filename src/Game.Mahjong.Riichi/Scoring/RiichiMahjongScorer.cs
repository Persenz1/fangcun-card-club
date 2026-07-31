using Game.Mahjong.Analysis;
using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Scoring;

public static class RiichiMahjongScorer
{
    private static readonly MahjongWinningOptions WinningOptions = new(
        AllowSevenPairs: true,
        AllowThirteenOrphans: true);

    private static readonly HashSet<MahjongTileKind> GreenKinds =
    [
        MahjongTileKind.Bamboo2,
        MahjongTileKind.Bamboo3,
        MahjongTileKind.Bamboo4,
        MahjongTileKind.Bamboo6,
        MahjongTileKind.Bamboo8,
        MahjongTileKind.Green,
    ];

    public static bool TryEvaluate(
        IEnumerable<MahjongTileKind> concealedWinningKinds,
        IReadOnlyList<MahjongMeld> melds,
        RiichiWinContext context,
        out RiichiHandScore? score)
    {
        ArgumentNullException.ThrowIfNull(concealedWinningKinds);
        ArgumentNullException.ThrowIfNull(melds);
        ArgumentNullException.ThrowIfNull(context);

        var concealedKinds = concealedWinningKinds.ToArray();
        var shapes = MahjongHandAnalyzer.Analyze(concealedKinds, melds.Count, WinningOptions);
        var candidates = new List<RiichiHandScore>();
        foreach (var shape in shapes)
        {
            foreach (var interpretation in CreateInterpretations(shape, context.WinningKind))
            {
                var candidate = ScoreInterpretation(
                    concealedKinds,
                    melds,
                    context,
                    interpretation);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }
        }

        score = candidates
            .OrderByDescending(candidate => candidate.BasicPoints)
            .ThenByDescending(candidate => candidate.Han)
            .ThenByDescending(candidate => candidate.Fu)
            .ThenBy(candidate => string.Join('|', candidate.Yaku), StringComparer.Ordinal)
            .FirstOrDefault();
        return score is not null;
    }

    public static RiichiHandScore Evaluate(
        IEnumerable<MahjongTileKind> concealedWinningKinds,
        IReadOnlyList<MahjongMeld> melds,
        RiichiWinContext context)
    {
        if (!TryEvaluate(concealedWinningKinds, melds, context, out var score))
        {
            throw new ArgumentException("The supplied hand is not a winning Riichi hand with at least one yaku.");
        }

        return score!;
    }

    public static RiichiWinResult CalculateWin(
        IEnumerable<MahjongTileKind> concealedWinningKinds,
        IReadOnlyList<MahjongMeld> melds,
        RiichiWinContext context,
        MahjongSeat? discardSource)
    {
        if (context.SelfDraw != (discardSource is null))
        {
            throw new ArgumentException("Win context and discard source disagree about the win method.");
        }

        var score = Evaluate(concealedWinningKinds, melds, context);
        var changes = new long[4];
        if (discardSource is { } payer)
        {
            var multiplier = context.Winner == context.Dealer ? 6 : 4;
            var payment = RoundUpToHundred(score.BasicPoints * multiplier)
                + (context.Honba * 300L);
            Transfer(changes, payer, context.Winner, payment);
        }
        else if (context.Winner == context.Dealer)
        {
            var payment = RoundUpToHundred(score.BasicPoints * 2)
                + (context.Honba * 100L);
            foreach (var seat in Enum.GetValues<MahjongSeat>().Where(seat => seat != context.Winner))
            {
                Transfer(changes, seat, context.Winner, payment);
            }
        }
        else
        {
            foreach (var seat in Enum.GetValues<MahjongSeat>().Where(seat => seat != context.Winner))
            {
                var multiplier = seat == context.Dealer ? 2 : 1;
                var payment = RoundUpToHundred(score.BasicPoints * multiplier)
                    + (context.Honba * 100L);
                Transfer(changes, seat, context.Winner, payment);
            }
        }

        changes[(int)context.Winner] += context.RiichiSticksAwarded * 1000L;
        return new RiichiWinResult(context.Winner, discardSource, score, changes);
    }

    public static IReadOnlyList<MahjongTileKind> GetWinningKinds(
        IEnumerable<MahjongTileKind> concealedKinds,
        int meldCount)
    {
        return MahjongHandAnalyzer.GetWinningKinds(concealedKinds, meldCount, WinningOptions);
    }

    public static IReadOnlyList<long> CalculateNotenPayments(
        IEnumerable<MahjongSeat> tenpaiSeats)
    {
        ArgumentNullException.ThrowIfNull(tenpaiSeats);
        var tenpai = tenpaiSeats.Distinct().ToArray();
        if (tenpai.Any(seat => !Enum.IsDefined(seat)))
        {
            throw new ArgumentOutOfRangeException(nameof(tenpaiSeats));
        }

        var changes = new long[4];
        if (tenpai.Length is 0 or 4)
        {
            return Array.AsReadOnly(changes);
        }

        var noten = Enum.GetValues<MahjongSeat>().Except(tenpai).ToArray();
        var gain = 3000 / tenpai.Length;
        var loss = 3000 / noten.Length;
        foreach (var seat in tenpai)
        {
            changes[(int)seat] += gain;
        }

        foreach (var seat in noten)
        {
            changes[(int)seat] -= loss;
        }

        return Array.AsReadOnly(changes);
    }

    public static MahjongTileKind GetDoraKind(MahjongTileKind indicator)
    {
        if (indicator.IsSuited())
        {
            var nextNumber = indicator.GetNumber() == 9 ? 1 : indicator.GetNumber() + 1;
            return MahjongTileKinds.FromSuitAndNumber(indicator.GetSuit(), nextNumber);
        }

        return indicator switch
        {
            MahjongTileKind.East => MahjongTileKind.South,
            MahjongTileKind.South => MahjongTileKind.West,
            MahjongTileKind.West => MahjongTileKind.North,
            MahjongTileKind.North => MahjongTileKind.East,
            MahjongTileKind.White => MahjongTileKind.Green,
            MahjongTileKind.Green => MahjongTileKind.Red,
            MahjongTileKind.Red => MahjongTileKind.White,
            _ => throw new ArgumentOutOfRangeException(nameof(indicator)),
        };
    }

    private static RiichiHandScore? ScoreInterpretation(
        IReadOnlyList<MahjongTileKind> concealedKinds,
        IReadOnlyList<MahjongMeld> melds,
        RiichiWinContext context,
        WinningInterpretation interpretation)
    {
        var shape = interpretation.Shape;
        var allKinds = concealedKinds
            .Concat(melds.SelectMany(meld => meld.Tiles.Select(tile => tile.Kind)))
            .ToArray();
        var groups = CreateGroups(shape, melds, context, interpretation).ToArray();
        var yakuman = FindYakuman(shape, groups, concealedKinds, allKinds, melds, context);
        if (yakuman.Count > 0)
        {
            return new RiichiHandScore(
                0,
                0,
                yakuman.Count,
                0,
                8000L * yakuman.Count,
                yakuman.Count == 1 ? "役满" : $"{yakuman.Count}倍役满",
                yakuman);
        }

        var closedHand = melds.All(meld => !meld.IsOpen);
        var yaku = new List<string>();
        var han = AddContextYaku(yaku, context, closedHand);

        if (allKinds.All(kind => !kind.IsTerminalOrHonor()))
        {
            han++;
            yaku.Add("断幺九");
        }

        if (allKinds.All(kind => kind.IsTerminalOrHonor()))
        {
            han += 2;
            yaku.Add("混老头");
        }

        han += AddFlushYaku(yaku, allKinds, closedHand);

        if (shape.Kind == MahjongWinningShapeKind.SevenPairs)
        {
            han += 2;
            yaku.Add("七对子");
        }
        else if (shape.Kind == MahjongWinningShapeKind.Standard)
        {
            han += AddStandardYaku(yaku, shape, groups, allKinds, context, closedHand, interpretation.Wait);
        }

        if (han == 0)
        {
            return null;
        }

        var (visibleDora, uraDora) = CountDora(allKinds, context);
        var doraCount = visibleDora + uraDora;
        if (visibleDora > 0)
        {
            yaku.Add($"宝牌×{visibleDora}");
        }

        if (uraDora > 0)
        {
            yaku.Add($"里宝牌×{uraDora}");
        }

        han += doraCount;
        var fu = CalculateFu(shape, groups, context, interpretation, closedHand);
        var (basicPoints, limitName) = CalculateBasicPoints(han, fu);
        return new RiichiHandScore(han, fu, 0, doraCount, basicPoints, limitName, yaku);
    }

    private static int AddContextYaku(
        ICollection<string> yaku,
        RiichiWinContext context,
        bool closedHand)
    {
        var han = 0;
        if (closedHand && context.IsDoubleRiichi)
        {
            han += 2;
            yaku.Add("双立直");
        }
        else if (closedHand && context.IsRiichi)
        {
            han++;
            yaku.Add("立直");
        }

        if (closedHand && (context.IsRiichi || context.IsDoubleRiichi) && context.IsIppatsu)
        {
            han++;
            yaku.Add("一发");
        }

        if (closedHand && context.SelfDraw)
        {
            han++;
            yaku.Add("门前清自摸和");
        }

        if (context.IsRinshan)
        {
            han++;
            yaku.Add("岭上开花");
        }

        if (context.IsChankan)
        {
            han++;
            yaku.Add("抢杠");
        }

        if (context.IsHaitei)
        {
            han++;
            yaku.Add("海底摸月");
        }

        if (context.IsHoutei)
        {
            han++;
            yaku.Add("河底捞鱼");
        }

        return han;
    }

    private static int AddFlushYaku(
        ICollection<string> yaku,
        IReadOnlyList<MahjongTileKind> allKinds,
        bool closedHand)
    {
        var suits = allKinds.Where(kind => kind.IsSuited()).Select(kind => kind.GetSuit()).Distinct().ToArray();
        if (suits.Length != 1)
        {
            return 0;
        }

        if (allKinds.Any(kind => kind.IsHonor()))
        {
            yaku.Add("混一色");
            return closedHand ? 3 : 2;
        }

        yaku.Add("清一色");
        return closedHand ? 6 : 5;
    }

    private static int AddStandardYaku(
        ICollection<string> yaku,
        MahjongWinningShape shape,
        IReadOnlyList<GroupInfo> groups,
        IReadOnlyList<MahjongTileKind> allKinds,
        RiichiWinContext context,
        bool closedHand,
        RiichiWaitType wait)
    {
        var han = 0;
        var pairKind = shape.PairKind!.Value;
        var tripletGroups = groups.Where(group => !group.IsSequence).ToArray();
        var sequenceGroups = groups.Where(group => group.IsSequence).ToArray();
        var seatWind = SeatWind(context.Winner, context.Dealer);
        var roundWind = context.RoundWind == RiichiRoundWind.East
            ? MahjongTileKind.East
            : MahjongTileKind.South;

        foreach (var dragon in new[] { MahjongTileKind.White, MahjongTileKind.Green, MahjongTileKind.Red })
        {
            if (tripletGroups.Any(group => group.FirstKind == dragon))
            {
                han++;
                yaku.Add($"役牌:{dragon}");
            }
        }

        if (tripletGroups.Any(group => group.FirstKind == seatWind))
        {
            han++;
            yaku.Add("自风牌");
        }

        if (tripletGroups.Any(group => group.FirstKind == roundWind))
        {
            han++;
            yaku.Add("场风牌");
        }

        var pinfu = closedHand
            && groups.All(group => group.IsSequence)
            && !IsValuePair(pairKind, seatWind, roundWind)
            && wait == RiichiWaitType.Ryanmen;
        if (pinfu)
        {
            han++;
            yaku.Add("平和");
        }

        if (closedHand)
        {
            var identicalSequencePairs = sequenceGroups
                .GroupBy(group => group.FirstKind)
                .Sum(group => group.Count() / 2);
            if (identicalSequencePairs >= 2)
            {
                han += 3;
                yaku.Add("二杯口");
            }
            else if (identicalSequencePairs == 1)
            {
                han++;
                yaku.Add("一杯口");
            }
        }

        var sequenceStarts = sequenceGroups.Select(group => group.FirstKind).ToHashSet();
        for (var number = 1; number <= 7; number++)
        {
            if (Enum.GetValues<MahjongTileSuit>()
                .Where(suit => suit != MahjongTileSuit.Honors)
                .All(suit => sequenceStarts.Contains(MahjongTileKinds.FromSuitAndNumber(suit, number))))
            {
                han += closedHand ? 2 : 1;
                yaku.Add("三色同顺");
                break;
            }
        }

        foreach (var suit in Enum.GetValues<MahjongTileSuit>().Where(suit => suit != MahjongTileSuit.Honors))
        {
            if (new[] { 1, 4, 7 }.All(number =>
                sequenceStarts.Contains(MahjongTileKinds.FromSuitAndNumber(suit, number))))
            {
                han += closedHand ? 2 : 1;
                yaku.Add("一气通贯");
                break;
            }
        }

        var everyGroupHasOutside = groups.All(GroupHasTerminalOrHonor)
            && pairKind.IsTerminalOrHonor();
        if (everyGroupHasOutside && sequenceGroups.Length > 0)
        {
            if (allKinds.All(kind => !kind.IsHonor()))
            {
                han += closedHand ? 3 : 2;
                yaku.Add("纯全带幺九");
            }
            else
            {
                han += closedHand ? 2 : 1;
                yaku.Add("混全带幺九");
            }
        }

        if (groups.All(group => !group.IsSequence))
        {
            han += 2;
            yaku.Add("对对和");
        }

        if (tripletGroups.Count(group => !group.IsOpenForFu) >= 3)
        {
            han += 2;
            yaku.Add("三暗刻");
        }

        if (groups.Count(group => group.IsKong) >= 3)
        {
            han += 2;
            yaku.Add("三杠子");
        }

        for (var number = 1; number <= 9; number++)
        {
            if (Enum.GetValues<MahjongTileSuit>()
                .Where(suit => suit != MahjongTileSuit.Honors)
                .All(suit => tripletGroups.Any(group =>
                    group.FirstKind == MahjongTileKinds.FromSuitAndNumber(suit, number))))
            {
                han += 2;
                yaku.Add("三色同刻");
                break;
            }
        }

        var dragonTriplets = tripletGroups.Count(group => group.FirstKind is
            MahjongTileKind.White or MahjongTileKind.Green or MahjongTileKind.Red);
        if (dragonTriplets == 2 && pairKind is
            MahjongTileKind.White or MahjongTileKind.Green or MahjongTileKind.Red)
        {
            han += 2;
            yaku.Add("小三元");
        }

        return han;
    }

    private static IReadOnlyList<string> FindYakuman(
        MahjongWinningShape shape,
        IReadOnlyList<GroupInfo> groups,
        IReadOnlyList<MahjongTileKind> concealedKinds,
        IReadOnlyList<MahjongTileKind> allKinds,
        IReadOnlyList<MahjongMeld> melds,
        RiichiWinContext context)
    {
        var yakuman = new List<string>();
        if (context.IsTenhou)
        {
            yakuman.Add("天和");
        }

        if (context.IsChiihou)
        {
            yakuman.Add("地和");
        }

        if (shape.Kind == MahjongWinningShapeKind.ThirteenOrphans)
        {
            yakuman.Add("国士无双");
        }

        var triplets = groups.Where(group => !group.IsSequence).ToArray();
        if (shape.Kind == MahjongWinningShapeKind.Standard
            && triplets.Count(group => !group.IsOpenForFu) == 4)
        {
            yakuman.Add("四暗刻");
        }

        if (triplets.Count(group => group.FirstKind is
            MahjongTileKind.White or MahjongTileKind.Green or MahjongTileKind.Red) == 3)
        {
            yakuman.Add("大三元");
        }

        var windTriplets = triplets.Count(group => group.FirstKind is >= MahjongTileKind.East and <= MahjongTileKind.North);
        if (windTriplets == 4)
        {
            yakuman.Add("大四喜");
        }
        else if (windTriplets == 3
            && shape.PairKind is >= MahjongTileKind.East and <= MahjongTileKind.North)
        {
            yakuman.Add("小四喜");
        }

        if (allKinds.All(kind => kind.IsHonor()))
        {
            yakuman.Add("字一色");
        }

        if (allKinds.All(kind => kind.IsTerminal()))
        {
            yakuman.Add("清老头");
        }

        if (allKinds.All(GreenKinds.Contains))
        {
            yakuman.Add("绿一色");
        }

        if (melds.All(meld => !meld.IsOpen) && IsNineGates(concealedKinds))
        {
            yakuman.Add("九莲宝灯");
        }

        if (groups.Count(group => group.IsKong) == 4)
        {
            yakuman.Add("四杠子");
        }

        return yakuman;
    }

    private static int CalculateFu(
        MahjongWinningShape shape,
        IReadOnlyList<GroupInfo> groups,
        RiichiWinContext context,
        WinningInterpretation interpretation,
        bool closedHand)
    {
        if (shape.Kind == MahjongWinningShapeKind.SevenPairs)
        {
            return 25;
        }

        if (shape.Kind != MahjongWinningShapeKind.Standard)
        {
            return 0;
        }

        var seatWind = SeatWind(context.Winner, context.Dealer);
        var roundWind = context.RoundWind == RiichiRoundWind.East
            ? MahjongTileKind.East
            : MahjongTileKind.South;
        var pinfu = closedHand
            && groups.All(group => group.IsSequence)
            && !IsValuePair(shape.PairKind!.Value, seatWind, roundWind)
            && interpretation.Wait == RiichiWaitType.Ryanmen;
        if (pinfu && context.SelfDraw)
        {
            return 20;
        }

        var fu = 20;
        if (!context.SelfDraw && closedHand)
        {
            fu += 10;
        }

        if (context.SelfDraw)
        {
            fu += 2;
        }

        var pairKind = shape.PairKind!.Value;
        if (pairKind is MahjongTileKind.White or MahjongTileKind.Green or MahjongTileKind.Red)
        {
            fu += 2;
        }

        if (pairKind == seatWind)
        {
            fu += 2;
        }

        if (pairKind == roundWind)
        {
            fu += 2;
        }

        foreach (var group in groups.Where(group => !group.IsSequence))
        {
            var outside = group.FirstKind.IsTerminalOrHonor();
            fu += group switch
            {
                { IsKong: true, IsOpenForFu: true } => outside ? 16 : 8,
                { IsKong: true } => outside ? 32 : 16,
                { IsOpenForFu: true } => outside ? 4 : 2,
                _ => outside ? 8 : 4,
            };
        }

        if (interpretation.Wait is RiichiWaitType.Tanki or RiichiWaitType.Kanchan or RiichiWaitType.Penchan)
        {
            fu += 2;
        }

        if (fu == 20 && !context.SelfDraw)
        {
            fu = 30;
        }

        return ((fu + 9) / 10) * 10;
    }

    private static (long BasicPoints, string LimitName) CalculateBasicPoints(int han, int fu)
    {
        if (han >= 13)
        {
            return (8000, "累计役满");
        }

        if (han >= 11)
        {
            return (6000, "三倍满");
        }

        if (han >= 8)
        {
            return (4000, "倍满");
        }

        if (han >= 6)
        {
            return (3000, "跳满");
        }

        if (han >= 5 || han == 4 && fu >= 40 || han == 3 && fu >= 70)
        {
            return (2000, "满贯");
        }

        return ((long)fu << (han + 2), string.Empty);
    }

    private static (int Visible, int Ura) CountDora(
        IReadOnlyList<MahjongTileKind> allKinds,
        RiichiWinContext context)
    {
        var visible = context.DoraIndicators
            .Select(GetDoraKind)
            .Sum(dora => allKinds.Count(kind => kind == dora));
        var ura = context.IsRiichi || context.IsDoubleRiichi
            ? context.UraDoraIndicators
                .Select(GetDoraKind)
                .Sum(dora => allKinds.Count(kind => kind == dora))
            : 0;
        return (visible, ura);
    }

    private static IEnumerable<GroupInfo> CreateGroups(
        MahjongWinningShape shape,
        IReadOnlyList<MahjongMeld> melds,
        RiichiWinContext context,
        WinningInterpretation interpretation)
    {
        for (var index = 0; index < shape.ConcealedGroups.Count; index++)
        {
            var group = shape.ConcealedGroups[index];
            var completedByRon = !context.SelfDraw
                && interpretation.WinningGroupIndex == index
                && group.Type == MahjongGroupType.Triplet;
            yield return new GroupInfo(
                group.Type == MahjongGroupType.Sequence,
                group.FirstKind,
                false,
                false,
                completedByRon);
        }

        foreach (var meld in melds)
        {
            yield return new GroupInfo(
                meld.Type == MahjongMeldType.Chow,
                meld.Tiles[0].Kind,
                meld.IsOpen,
                meld.Type is MahjongMeldType.OpenKong or MahjongMeldType.ConcealedKong or MahjongMeldType.AddedKong,
                false);
        }
    }

    private static IEnumerable<WinningInterpretation> CreateInterpretations(
        MahjongWinningShape shape,
        MahjongTileKind winningKind)
    {
        if (shape.Kind != MahjongWinningShapeKind.Standard)
        {
            yield return new WinningInterpretation(shape, RiichiWaitType.Special, null);
            yield break;
        }

        if (shape.PairKind == winningKind)
        {
            yield return new WinningInterpretation(shape, RiichiWaitType.Tanki, null);
        }

        for (var index = 0; index < shape.ConcealedGroups.Count; index++)
        {
            var group = shape.ConcealedGroups[index];
            if (group.Type == MahjongGroupType.Triplet && group.FirstKind == winningKind)
            {
                yield return new WinningInterpretation(shape, RiichiWaitType.Shanpon, index);
                continue;
            }

            if (group.Type != MahjongGroupType.Sequence
                || winningKind < group.FirstKind
                || winningKind > (MahjongTileKind)((int)group.FirstKind + 2))
            {
                continue;
            }

            var offset = (int)winningKind - (int)group.FirstKind;
            var wait = offset == 1
                ? RiichiWaitType.Kanchan
                : group.FirstKind.GetNumber() == 1 && offset == 2
                    || group.FirstKind.GetNumber() == 7 && offset == 0
                        ? RiichiWaitType.Penchan
                        : RiichiWaitType.Ryanmen;
            yield return new WinningInterpretation(shape, wait, index);
        }
    }

    private static bool GroupHasTerminalOrHonor(GroupInfo group)
    {
        return group.IsSequence
            ? group.FirstKind.GetNumber() is 1 or 7
            : group.FirstKind.IsTerminalOrHonor();
    }

    private static bool IsValuePair(
        MahjongTileKind pairKind,
        MahjongTileKind seatWind,
        MahjongTileKind roundWind)
    {
        return pairKind is MahjongTileKind.White or MahjongTileKind.Green or MahjongTileKind.Red
            || pairKind == seatWind
            || pairKind == roundWind;
    }

    private static MahjongTileKind SeatWind(MahjongSeat seat, MahjongSeat dealer)
    {
        return (MahjongTileKind)((int)MahjongTileKind.East + seat.DistanceFrom(dealer));
    }

    private static bool IsNineGates(IReadOnlyList<MahjongTileKind> concealedKinds)
    {
        if (concealedKinds.Count != 14
            || concealedKinds.Any(kind => kind.IsHonor())
            || concealedKinds.Select(kind => kind.GetSuit()).Distinct().Count() != 1)
        {
            return false;
        }

        var counts = concealedKinds.GroupBy(kind => kind.GetNumber())
            .ToDictionary(group => group.Key, group => group.Count());
        return counts.GetValueOrDefault(1) >= 3
            && counts.GetValueOrDefault(9) >= 3
            && Enumerable.Range(2, 7).All(number => counts.GetValueOrDefault(number) >= 1);
    }

    private static long RoundUpToHundred(long points)
    {
        return ((points + 99) / 100) * 100;
    }

    private static void Transfer(long[] changes, MahjongSeat payer, MahjongSeat recipient, long amount)
    {
        changes[(int)payer] -= amount;
        changes[(int)recipient] += amount;
    }

    private enum RiichiWaitType
    {
        Special,
        Ryanmen,
        Kanchan,
        Penchan,
        Shanpon,
        Tanki,
    }

    private sealed record WinningInterpretation(
        MahjongWinningShape Shape,
        RiichiWaitType Wait,
        int? WinningGroupIndex);

    private sealed record GroupInfo(
        bool IsSequence,
        MahjongTileKind FirstKind,
        bool IsOpen,
        bool IsKong,
        bool CompletedByRon)
    {
        public bool IsOpenForFu => IsOpen || CompletedByRon;
    }
}
