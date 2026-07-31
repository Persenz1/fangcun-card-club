using Game.Core.Simulation;
using Game.Mahjong.Analysis;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Standard.Actions;
using Game.Mahjong.Standard.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Standard.AI;

public sealed class BasicStandardMahjongAi
{
    private static readonly MahjongWinningOptions WinningOptions = new(
        AllowSevenPairs: true,
        AllowThirteenOrphans: true);

    public IGameCommand ChooseCommand(
        StandardMahjongSnapshot snapshot,
        MahjongSeat seat,
        IReadOnlyList<StandardMahjongAction> legalActions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(legalActions);
        if (legalActions.Count == 0)
        {
            throw new InvalidOperationException("AI requires at least one legal action.");
        }

        var win = legalActions.FirstOrDefault(action =>
            action.Kind is StandardMahjongActionKind.SelfDrawWin or StandardMahjongActionKind.DiscardWin);
        if (win is not null)
        {
            return new DeclareMahjongWinCommand((int)seat);
        }

        var kong = legalActions.FirstOrDefault(action => action.Kind is
            StandardMahjongActionKind.ConcealedKong or
            StandardMahjongActionKind.AddedKong or
            StandardMahjongActionKind.OpenKong);
        if (kong is not null)
        {
            return kong.Kind switch
            {
                StandardMahjongActionKind.ConcealedKong =>
                    new DeclareConcealedKongCommand((int)seat, kong.ConcealedTiles),
                StandardMahjongActionKind.AddedKong =>
                    new DeclareAddedKongCommand((int)seat, kong.Tile!.Value),
                _ => new ClaimMahjongDiscardCommand((int)seat, MahjongMeldType.OpenKong, kong.ConcealedTiles),
            };
        }

        var claim = legalActions.FirstOrDefault(action => action.Kind == StandardMahjongActionKind.Pong)
            ?? legalActions.FirstOrDefault(action => action.Kind == StandardMahjongActionKind.Chow);
        if (claim is not null)
        {
            return new ClaimMahjongDiscardCommand(
                (int)seat,
                claim.MeldType!.Value,
                claim.ConcealedTiles);
        }

        var discardActions = legalActions
            .Where(action => action.Kind == StandardMahjongActionKind.Discard)
            .ToArray();
        if (discardActions.Length > 0)
        {
            var hand = snapshot.Table.Hands[(int)seat];
            var meldCount = snapshot.Table.Melds[(int)seat].Count;
            var discard = discardActions
                .Select(action => new
                {
                    Action = action,
                    WaitCount = MahjongHandAnalyzer.GetWinningKinds(
                        hand.Where(tile => tile != action.Tile).Select(tile => tile.Kind),
                        meldCount,
                        WinningOptions).Count,
                })
                .OrderByDescending(candidate => candidate.WaitCount)
                .ThenByDescending(candidate => DiscardIsolation(candidate.Action.Tile!.Value.Kind, hand))
                .ThenByDescending(candidate => candidate.Action.Tile!.Value.Kind)
                .First()
                .Action;
            return new DiscardMahjongTileCommand((int)seat, discard.Tile!.Value);
        }

        return new PassMahjongCommand((int)seat);
    }

    private static int DiscardIsolation(MahjongTileKind kind, IReadOnlyList<MahjongTile> hand)
    {
        if (kind.IsHonor())
        {
            return 3;
        }

        var nearby = hand.Count(tile =>
            tile.Kind.GetSuit() == kind.GetSuit()
            && Math.Abs(tile.Kind.GetNumber() - kind.GetNumber()) <= 2);
        return kind.IsTerminal() ? 2 - nearby : 1 - nearby;
    }
}
