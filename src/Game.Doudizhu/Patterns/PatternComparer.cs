namespace Game.Doudizhu.Patterns;

public static class PatternComparer
{
    public static bool CanBeat(CardPattern candidate, CardPattern previous)
    {
        if (candidate.Kind == CardPatternKind.Rocket)
        {
            return previous.Kind != CardPatternKind.Rocket;
        }

        if (previous.Kind == CardPatternKind.Rocket)
        {
            return false;
        }

        if (candidate.Kind == CardPatternKind.Bomb)
        {
            return previous.Kind != CardPatternKind.Bomb || candidate.MainRank > previous.MainRank;
        }

        if (previous.Kind == CardPatternKind.Bomb)
        {
            return false;
        }

        return candidate.Kind == previous.Kind
            && candidate.CardCount == previous.CardCount
            && candidate.SequenceLength == previous.SequenceLength
            && candidate.MainRank > previous.MainRank;
    }
}
