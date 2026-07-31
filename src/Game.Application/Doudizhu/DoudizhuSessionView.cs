using Game.Doudizhu.Moves;
using Game.Doudizhu.State;

namespace Game.Application.Doudizhu;

public sealed class DoudizhuSessionView
{
    public DoudizhuSessionView(
        DoudizhuObservation playerObservation,
        IReadOnlyList<DoudizhuMove> legalMoves,
        bool isHumanTurn)
    {
        PlayerObservation = playerObservation;
        LegalMoves = legalMoves;
        IsHumanTurn = isHumanTurn;
    }

    public DoudizhuObservation PlayerObservation { get; }

    public IReadOnlyList<DoudizhuMove> LegalMoves { get; }

    public bool IsHumanTurn { get; }
}
