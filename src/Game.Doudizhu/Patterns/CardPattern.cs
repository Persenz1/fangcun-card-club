using Game.Doudizhu.Cards;

namespace Game.Doudizhu.Patterns;

public readonly record struct CardPattern(
    CardPatternKind Kind,
    CardRank MainRank,
    int SequenceLength,
    int CardCount);
