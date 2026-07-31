using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.Actions;
using Game.Mahjong.Riichi.Commands;
using Game.Mahjong.Riichi.Scoring;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.AI;

public sealed class BasicRiichiMahjongAi
{
    public IGameCommand ChooseCommand(
        RiichiMahjongSnapshot snapshot,
        MahjongSeat seat,
        IReadOnlyList<RiichiMahjongAction> legalActions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(legalActions);
        if (legalActions.Count == 0)
        {
            throw new InvalidOperationException("AI requires at least one legal action.");
        }

        var win = legalActions.FirstOrDefault(action =>
            action.Kind is RiichiMahjongActionKind.SelfDrawWin or RiichiMahjongActionKind.DiscardWin);
        if (win is not null)
        {
            return new DeclareMahjongWinCommand((int)seat);
        }

        if (legalActions.Any(action => action.Kind == RiichiMahjongActionKind.NineTerminalsDraw))
        {
            return new DeclareNineTerminalsDrawCommand((int)seat);
        }

        var concealedKong = legalActions.FirstOrDefault(action =>
            action.Kind == RiichiMahjongActionKind.ConcealedKong);
        if (concealedKong is not null)
        {
            return new DeclareConcealedKongCommand((int)seat, concealedKong.ConcealedTiles);
        }

        var addedKong = legalActions.FirstOrDefault(action =>
            action.Kind == RiichiMahjongActionKind.AddedKong);
        if (addedKong is not null)
        {
            return new DeclareAddedKongCommand((int)seat, addedKong.Tile!.Value);
        }

        var riichiActions = legalActions
            .Where(action => action.Kind == RiichiMahjongActionKind.RiichiDiscard)
            .ToArray();
        if (riichiActions.Length > 0)
        {
            var tile = ChooseDiscard(snapshot, seat, riichiActions);
            return new DeclareRiichiCommand((int)seat, tile);
        }

        var valuableClaim = legalActions.FirstOrDefault(action =>
            action.Kind == RiichiMahjongActionKind.OpenKong && IsValueHonor(snapshot, seat, action.Tile!.Value.Kind))
            ?? legalActions.FirstOrDefault(action =>
                action.Kind == RiichiMahjongActionKind.Pong && IsValueHonor(snapshot, seat, action.Tile!.Value.Kind));
        if (valuableClaim is not null)
        {
            return new ClaimMahjongDiscardCommand(
                (int)seat,
                valuableClaim.MeldType!.Value,
                valuableClaim.ConcealedTiles);
        }

        var discardActions = legalActions
            .Where(action => action.Kind == RiichiMahjongActionKind.Discard)
            .ToArray();
        if (discardActions.Length > 0)
        {
            var tile = ChooseDiscard(snapshot, seat, discardActions);
            return new DiscardMahjongTileCommand((int)seat, tile);
        }

        return new PassMahjongCommand((int)seat);
    }

    private static MahjongTile ChooseDiscard(
        RiichiMahjongSnapshot snapshot,
        MahjongSeat seat,
        IReadOnlyList<RiichiMahjongAction> discardActions)
    {
        var hand = snapshot.Table.Hands[(int)seat];
        var meldCount = snapshot.Table.Melds[(int)seat].Count;
        var doraKinds = snapshot.DoraIndicators.Select(RiichiMahjongScorer.GetDoraKind).ToHashSet();
        var riichiOpponents = Enum.GetValues<MahjongSeat>()
            .Where(other => other != seat && snapshot.RiichiDeclared[(int)other])
            .ToArray();
        return discardActions
            .Select(action => new
            {
                Tile = action.Tile!.Value,
                WaitCount = RiichiMahjongScorer.GetWinningKinds(
                    hand.Where(tile => tile != action.Tile).Select(tile => tile.Kind),
                    meldCount).Count,
                SafeAgainstRiichi = riichiOpponents.Length > 0
                    && riichiOpponents.All(opponent => snapshot.Table.Rivers[(int)opponent]
                        .Any(riverTile => riverTile.Tile.Kind == action.Tile.Value.Kind)),
            })
            .OrderByDescending(candidate => candidate.SafeAgainstRiichi)
            .ThenByDescending(candidate => candidate.WaitCount)
            .ThenBy(candidate => doraKinds.Contains(candidate.Tile.Kind))
            .ThenByDescending(candidate => DiscardIsolation(candidate.Tile.Kind, hand))
            .ThenByDescending(candidate => candidate.Tile.Kind)
            .ThenByDescending(candidate => candidate.Tile.CopyIndex)
            .First()
            .Tile;
    }

    private static bool IsValueHonor(
        RiichiMahjongSnapshot snapshot,
        MahjongSeat seat,
        MahjongTileKind kind)
    {
        if (kind is MahjongTileKind.White or MahjongTileKind.Green or MahjongTileKind.Red)
        {
            return true;
        }

        var seatWind = (MahjongTileKind)((int)MahjongTileKind.East + seat.DistanceFrom(snapshot.Dealer));
        var roundWind = snapshot.RoundWind == RiichiRoundWind.East
            ? MahjongTileKind.East
            : MahjongTileKind.South;
        return kind == seatWind || kind == roundWind;
    }

    private static int DiscardIsolation(MahjongTileKind kind, IReadOnlyList<MahjongTile> hand)
    {
        if (kind.IsHonor())
        {
            return 5 - hand.Count(tile => tile.Kind == kind);
        }

        var nearby = hand.Count(tile =>
            tile.Kind.GetSuit() == kind.GetSuit()
            && Math.Abs(tile.Kind.GetNumber() - kind.GetNumber()) <= 2);
        return kind.IsTerminal() ? 3 - nearby : 2 - nearby;
    }
}
