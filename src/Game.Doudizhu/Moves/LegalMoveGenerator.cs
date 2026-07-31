using Game.Doudizhu.Cards;
using Game.Doudizhu.Patterns;

namespace Game.Doudizhu.Moves;

public static class LegalMoveGenerator
{
    private static readonly CardRank[] Ranks = Enum.GetValues<CardRank>();
    private static readonly int AceIndex = Array.IndexOf(Ranks, CardRank.Ace);

    public static IReadOnlyList<DoudizhuMove> Generate(
        IReadOnlyCollection<Card> hand,
        CardPattern? previousPattern = null)
    {
        ArgumentNullException.ThrowIfNull(hand);

        var cardsByRank = Ranks
            .Select(rank => hand
                .Where(card => card.Rank == rank)
                .OrderBy(card => card.Suit)
                .ToArray())
            .ToArray();
        var available = cardsByRank.Select(cards => cards.Length).ToArray();
        var selections = new Dictionary<ulong, RankSelection>();

        void TryAdd(int[] counts)
        {
            var representative = counts
                .SelectMany((count, rankIndex) => cardsByRank[rankIndex].Take(count))
                .ToArray();
            var pattern = PatternDetector.Detect(representative);
            if (pattern is null || previousPattern is { } previous && !PatternComparer.CanBeat(pattern.Value, previous))
            {
                return;
            }

            var key = EncodeCounts(counts);
            selections.TryAdd(key, new RankSelection((int[])counts.Clone(), pattern.Value));
        }

        AddBasicGroups(available, TryAdd);
        AddTripleAttachments(available, TryAdd);
        AddSequences(available, 1, 5, TryAdd);
        AddSequences(available, 2, 3, TryAdd);
        AddAirplanes(available, TryAdd);
        AddFourWithAttachments(available, TryAdd);
        AddRocket(available, TryAdd);

        var moves = new List<DoudizhuMove>();
        foreach (var selection in selections.Values)
        {
            ExpandPhysicalCards(cardsByRank, selection, 0, [], moves);
        }

        return moves
            .OrderBy(move => move.Pattern.Kind)
            .ThenBy(move => move.Pattern.CardCount)
            .ThenBy(move => move.Pattern.MainRank)
            .ThenBy(GetCardKey)
            .ToArray();
    }

    private static void AddBasicGroups(int[] available, Action<int[]> tryAdd)
    {
        for (var rankIndex = 0; rankIndex < Ranks.Length; rankIndex++)
        {
            for (var count = 1; count <= Math.Min(available[rankIndex], 4); count++)
            {
                var selection = new int[Ranks.Length];
                selection[rankIndex] = count;
                tryAdd(selection);
            }
        }
    }

    private static void AddTripleAttachments(int[] available, Action<int[]> tryAdd)
    {
        for (var bodyIndex = 0; bodyIndex < Ranks.Length; bodyIndex++)
        {
            if (available[bodyIndex] < 3)
            {
                continue;
            }

            for (var wingIndex = 0; wingIndex < Ranks.Length; wingIndex++)
            {
                if (wingIndex == bodyIndex || available[wingIndex] == 0)
                {
                    continue;
                }

                var singleSelection = new int[Ranks.Length];
                singleSelection[bodyIndex] = 3;
                singleSelection[wingIndex] = 1;
                tryAdd(singleSelection);

                if (available[wingIndex] >= 2)
                {
                    var pairSelection = (int[])singleSelection.Clone();
                    pairSelection[wingIndex] = 2;
                    tryAdd(pairSelection);
                }
            }
        }
    }

    private static void AddSequences(
        int[] available,
        int cardsPerRank,
        int minimumLength,
        Action<int[]> tryAdd)
    {
        for (var startIndex = 0; startIndex <= AceIndex; startIndex++)
        {
            var selection = new int[Ranks.Length];
            for (var endIndex = startIndex; endIndex <= AceIndex; endIndex++)
            {
                if (available[endIndex] < cardsPerRank)
                {
                    break;
                }

                selection[endIndex] = cardsPerRank;
                if (endIndex - startIndex + 1 >= minimumLength)
                {
                    tryAdd(selection);
                }
            }
        }
    }

    private static void AddAirplanes(int[] available, Action<int[]> tryAdd)
    {
        for (var startIndex = 0; startIndex <= AceIndex; startIndex++)
        {
            var body = new int[Ranks.Length];
            for (var endIndex = startIndex; endIndex <= AceIndex; endIndex++)
            {
                if (available[endIndex] < 3)
                {
                    break;
                }

                body[endIndex] = 3;
                var bodyLength = endIndex - startIndex + 1;
                if (bodyLength < 2)
                {
                    continue;
                }

                tryAdd(body);
                AddSingleAttachments(body, available, bodyLength, tryAdd);
                AddPairAttachments(body, available, bodyLength, tryAdd);
            }
        }
    }

    private static void AddFourWithAttachments(int[] available, Action<int[]> tryAdd)
    {
        for (var bodyIndex = 0; bodyIndex < Ranks.Length; bodyIndex++)
        {
            if (available[bodyIndex] < 4)
            {
                continue;
            }

            var body = new int[Ranks.Length];
            body[bodyIndex] = 4;
            AddSingleAttachments(body, available, 2, tryAdd);
            AddPairAttachments(body, available, 2, tryAdd);
        }
    }

    private static void AddSingleAttachments(
        int[] body,
        int[] available,
        int attachmentCount,
        Action<int[]> tryAdd)
    {
        var selection = (int[])body.Clone();
        AddSingleAttachments(selection, available, attachmentCount, 0, tryAdd);
    }

    private static void AddSingleAttachments(
        int[] selection,
        int[] available,
        int remaining,
        int rankIndex,
        Action<int[]> tryAdd)
    {
        if (remaining == 0)
        {
            tryAdd(selection);
            return;
        }

        if (rankIndex == Ranks.Length)
        {
            return;
        }

        var reserved = selection[rankIndex];
        var maximumAtRank = reserved >= 3 ? 1 : 2;
        var maximum = Math.Min(remaining, Math.Min(maximumAtRank, available[rankIndex] - reserved));

        for (var count = 0; count <= maximum; count++)
        {
            selection[rankIndex] += count;
            AddSingleAttachments(selection, available, remaining - count, rankIndex + 1, tryAdd);
            selection[rankIndex] -= count;
        }
    }

    private static void AddPairAttachments(
        int[] body,
        int[] available,
        int pairCount,
        Action<int[]> tryAdd)
    {
        var selection = (int[])body.Clone();
        AddPairAttachments(selection, available, pairCount, 0, tryAdd);
    }

    private static void AddPairAttachments(
        int[] selection,
        int[] available,
        int remainingPairs,
        int startIndex,
        Action<int[]> tryAdd)
    {
        if (remainingPairs == 0)
        {
            tryAdd(selection);
            return;
        }

        for (var rankIndex = startIndex; rankIndex < Ranks.Length; rankIndex++)
        {
            if (available[rankIndex] - selection[rankIndex] < 2 || selection[rankIndex] != 0)
            {
                continue;
            }

            selection[rankIndex] = 2;
            AddPairAttachments(selection, available, remainingPairs - 1, rankIndex + 1, tryAdd);
            selection[rankIndex] = 0;
        }
    }

    private static void AddRocket(int[] available, Action<int[]> tryAdd)
    {
        var smallJokerIndex = Array.IndexOf(Ranks, CardRank.SmallJoker);
        var bigJokerIndex = Array.IndexOf(Ranks, CardRank.BigJoker);
        if (available[smallJokerIndex] == 0 || available[bigJokerIndex] == 0)
        {
            return;
        }

        var selection = new int[Ranks.Length];
        selection[smallJokerIndex] = 1;
        selection[bigJokerIndex] = 1;
        tryAdd(selection);
    }

    private static void ExpandPhysicalCards(
        IReadOnlyList<Card[]> cardsByRank,
        RankSelection selection,
        int rankIndex,
        List<Card> current,
        ICollection<DoudizhuMove> moves)
    {
        if (rankIndex == Ranks.Length)
        {
            moves.Add(new DoudizhuMove(current, selection.Pattern));
            return;
        }

        var count = selection.Counts[rankIndex];
        if (count == 0)
        {
            ExpandPhysicalCards(cardsByRank, selection, rankIndex + 1, current, moves);
            return;
        }

        foreach (var choice in Choose(cardsByRank[rankIndex], count))
        {
            current.AddRange(choice);
            ExpandPhysicalCards(cardsByRank, selection, rankIndex + 1, current, moves);
            current.RemoveRange(current.Count - choice.Count, choice.Count);
        }
    }

    private static IEnumerable<IReadOnlyList<Card>> Choose(IReadOnlyList<Card> cards, int count)
    {
        var choice = new Card[count];
        return Choose(cards, count, 0, 0, choice);
    }

    private static IEnumerable<IReadOnlyList<Card>> Choose(
        IReadOnlyList<Card> cards,
        int count,
        int sourceIndex,
        int choiceIndex,
        Card[] choice)
    {
        if (choiceIndex == count)
        {
            yield return (Card[])choice.Clone();
            yield break;
        }

        for (var index = sourceIndex; index <= cards.Count - (count - choiceIndex); index++)
        {
            choice[choiceIndex] = cards[index];
            foreach (var result in Choose(cards, count, index + 1, choiceIndex + 1, choice))
            {
                yield return result;
            }
        }
    }

    private static ulong EncodeCounts(IReadOnlyList<int> counts)
    {
        ulong key = 0;
        for (var index = 0; index < counts.Count; index++)
        {
            key |= (ulong)counts[index] << (index * 3);
        }

        return key;
    }

    private static ulong GetCardKey(DoudizhuMove move)
    {
        ulong key = 0;
        foreach (var card in move.Cards)
        {
            var cardIndex = card.IsJoker
                ? 52 + (card.Rank == CardRank.BigJoker ? 1 : 0)
                : (((int)card.Rank - (int)CardRank.Three) * 4) + (int)card.Suit;
            key |= 1UL << cardIndex;
        }

        return key;
    }

    private sealed record RankSelection(int[] Counts, CardPattern Pattern);
}
