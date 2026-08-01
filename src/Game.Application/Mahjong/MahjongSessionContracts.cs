using Game.Application.Mahjong.Riichi;
using Game.Application.Mahjong.Sichuan;
using Game.Application.Mahjong.Standard;
using Game.Mahjong.Table;

namespace Game.Application.Mahjong;

public enum MahjongMode
{
    Standard,
    Sichuan,
    Riichi,
}

public interface IMahjongGameSession
{
    event Action? StateChanged;

    MahjongMode Mode { get; }

    ulong Seed { get; }

    MahjongSeat HumanSeat { get; }

    MahjongSessionView Snapshot { get; }

    MahjongSessionResult Dispatch(int actionId);

    MahjongSessionResult DispatchSuggestedAction();

    MahjongSessionResult AdvanceAiTurn();
}

public sealed record MahjongSessionResult(
    bool Accepted,
    MahjongSessionView Snapshot,
    IReadOnlyList<MahjongAnimationEvent> Events,
    string? Error = null);

public static class MahjongSessionFactory
{
    public static IMahjongGameSession Start(
        MahjongMode mode,
        ulong seed,
        MahjongSeat humanSeat = MahjongSeat.East)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (!Enum.IsDefined(humanSeat))
        {
            throw new ArgumentOutOfRangeException(nameof(humanSeat));
        }

        return mode switch
        {
            MahjongMode.Standard => StandardMahjongGameSession.Start(seed, humanSeat),
            MahjongMode.Sichuan => SichuanMahjongGameSession.Start(seed, humanSeat),
            MahjongMode.Riichi => RiichiMahjongGameSession.Start(seed, humanSeat),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
