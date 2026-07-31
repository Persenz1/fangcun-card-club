using Game.Doudizhu.Cards;

namespace Game.Doudizhu.Patterns;

public static class PatternDetector
{
    public static CardPattern? Detect(IReadOnlyCollection<Card> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        if (cards.Count == 0)
        {
            return null;
        }

        var groups = cards
            .GroupBy(card => card.Rank)
            .ToDictionary(group => group.Key, group => group.Count());

        if (cards.Count == 1)
        {
            return Create(CardPatternKind.Single, groups.Keys.Single(), cards.Count);
        }

        if (cards.Count == 2)
        {
            if (groups.Count == 2
                && groups.ContainsKey(CardRank.SmallJoker)
                && groups.ContainsKey(CardRank.BigJoker))
            {
                return Create(CardPatternKind.Rocket, CardRank.BigJoker, cards.Count);
            }

            if (TryGetOnlyRankWithCount(groups, 2, out var pairRank))
            {
                return Create(CardPatternKind.Pair, pairRank, cards.Count);
            }

            return null;
        }

        if (cards.Count == 3 && TryGetOnlyRankWithCount(groups, 3, out var tripleRank))
        {
            return Create(CardPatternKind.Triple, tripleRank, cards.Count);
        }

        if (cards.Count == 4)
        {
            if (TryGetOnlyRankWithCount(groups, 4, out var bombRank))
            {
                return Create(CardPatternKind.Bomb, bombRank, cards.Count);
            }

            if (TryGetBodyRank(groups, 3, [1], out tripleRank))
            {
                return Create(CardPatternKind.TripleWithSingle, tripleRank, cards.Count);
            }
        }

        if (cards.Count == 5 && TryGetBodyRank(groups, 3, [2], out tripleRank))
        {
            return Create(CardPatternKind.TripleWithPair, tripleRank, cards.Count);
        }

        if (TryGetExactSequence(groups, 1, 5, out var sequenceHigh, out var sequenceLength))
        {
            return new CardPattern(CardPatternKind.Straight, sequenceHigh, sequenceLength, cards.Count);
        }

        if (TryGetExactSequence(groups, 2, 3, out sequenceHigh, out sequenceLength))
        {
            return new CardPattern(CardPatternKind.PairStraight, sequenceHigh, sequenceLength, cards.Count);
        }

        if (TryGetExactSequence(groups, 3, 2, out sequenceHigh, out sequenceLength))
        {
            return new CardPattern(CardPatternKind.Airplane, sequenceHigh, sequenceLength, cards.Count);
        }

        if (TryGetAirplane(groups, cards.Count, 4, WingKind.Singles, out sequenceHigh, out sequenceLength))
        {
            return new CardPattern(CardPatternKind.AirplaneWithSingles, sequenceHigh, sequenceLength, cards.Count);
        }

        if (TryGetAirplane(groups, cards.Count, 5, WingKind.Pairs, out sequenceHigh, out sequenceLength))
        {
            return new CardPattern(CardPatternKind.AirplaneWithPairs, sequenceHigh, sequenceLength, cards.Count);
        }

        if (cards.Count == 6 && TryGetFourWithAttachments(groups, WingKind.Singles, out var fourRank))
        {
            return Create(CardPatternKind.FourWithSingles, fourRank, cards.Count);
        }

        if (cards.Count == 8 && TryGetFourWithAttachments(groups, WingKind.Pairs, out fourRank))
        {
            return Create(CardPatternKind.FourWithPairs, fourRank, cards.Count);
        }

        return null;
    }

    private static CardPattern Create(CardPatternKind kind, CardRank mainRank, int cardCount)
    {
        return new CardPattern(kind, mainRank, 1, cardCount);
    }

    private static bool TryGetOnlyRankWithCount(
        IReadOnlyDictionary<CardRank, int> groups,
        int count,
        out CardRank rank)
    {
        if (groups.Count == 1 && groups.Values.Single() == count)
        {
            rank = groups.Keys.Single();
            return true;
        }

        rank = default;
        return false;
    }

    private static bool TryGetBodyRank(
        IReadOnlyDictionary<CardRank, int> groups,
        int bodyCount,
        IReadOnlyList<int> attachmentCounts,
        out CardRank bodyRank)
    {
        var body = groups.SingleOrDefault(group => group.Value == bodyCount);
        if (body.Value != bodyCount)
        {
            bodyRank = default;
            return false;
        }

        var remainingCounts = groups
            .Where(group => group.Key != body.Key)
            .Select(group => group.Value)
            .Order()
            .ToArray();

        if (!remainingCounts.SequenceEqual(attachmentCounts.Order()))
        {
            bodyRank = default;
            return false;
        }

        bodyRank = body.Key;
        return true;
    }

    private static bool TryGetExactSequence(
        IReadOnlyDictionary<CardRank, int> groups,
        int cardsPerRank,
        int minimumLength,
        out CardRank highRank,
        out int sequenceLength)
    {
        var ranks = groups.Keys.Order().ToArray();
        if (ranks.Length < minimumLength
            || ranks[^1] > CardRank.Ace
            || groups.Values.Any(count => count != cardsPerRank))
        {
            highRank = default;
            sequenceLength = 0;
            return false;
        }

        for (var index = 1; index < ranks.Length; index++)
        {
            if ((int)ranks[index] != (int)ranks[index - 1] + 1)
            {
                highRank = default;
                sequenceLength = 0;
                return false;
            }
        }

        highRank = ranks[^1];
        sequenceLength = ranks.Length;
        return true;
    }

    private static bool TryGetAirplane(
        IReadOnlyDictionary<CardRank, int> groups,
        int cardCount,
        int cardsPerBodyGroup,
        WingKind wingKind,
        out CardRank highRank,
        out int sequenceLength)
    {
        if (cardCount % cardsPerBodyGroup != 0)
        {
            highRank = default;
            sequenceLength = 0;
            return false;
        }

        sequenceLength = cardCount / cardsPerBodyGroup;
        if (sequenceLength < 2)
        {
            highRank = default;
            sequenceLength = 0;
            return false;
        }

        for (var startValue = (int)CardRank.Three;
             startValue + sequenceLength - 1 <= (int)CardRank.Ace;
             startValue++)
        {
            var bodyRanks = Enumerable.Range(startValue, sequenceLength)
                .Select(value => (CardRank)value)
                .ToHashSet();

            if (bodyRanks.Any(rank => !groups.TryGetValue(rank, out var count) || count < 3))
            {
                continue;
            }

            var remainders = groups
                .Select(group => new
                {
                    group.Key,
                    Count = group.Value - (bodyRanks.Contains(group.Key) ? 3 : 0),
                })
                .Where(group => group.Count > 0)
                .ToArray();

            var validWings = wingKind switch
            {
                WingKind.Singles => remainders.Sum(group => group.Count) == sequenceLength
                    && remainders.All(group => bodyRanks.Contains(group.Key) ? group.Count == 1 : group.Count <= 2),
                WingKind.Pairs => remainders.Length == sequenceLength
                    && remainders.All(group => !bodyRanks.Contains(group.Key) && group.Count == 2),
                _ => false,
            };

            if (validWings)
            {
                highRank = (CardRank)(startValue + sequenceLength - 1);
                return true;
            }
        }

        highRank = default;
        sequenceLength = 0;
        return false;
    }

    private static bool TryGetFourWithAttachments(
        IReadOnlyDictionary<CardRank, int> groups,
        WingKind wingKind,
        out CardRank fourRank)
    {
        var bodies = groups.Where(group => group.Value == 4).ToArray();
        if (bodies.Length != 1)
        {
            fourRank = default;
            return false;
        }

        var body = bodies[0];
        var attachments = groups
            .Where(group => group.Key != body.Key)
            .Select(group => group.Value)
            .ToArray();

        var valid = wingKind switch
        {
            WingKind.Singles => attachments.Sum() == 2 && attachments.All(count => count <= 2),
            WingKind.Pairs => attachments.Length == 2 && attachments.All(count => count == 2),
            _ => false,
        };

        fourRank = valid ? body.Key : default;
        return valid;
    }

    private enum WingKind
    {
        Singles,
        Pairs,
    }
}
