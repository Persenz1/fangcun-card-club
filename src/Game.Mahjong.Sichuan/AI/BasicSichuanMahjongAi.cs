using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Sichuan.Actions;
using Game.Mahjong.Sichuan.Commands;
using Game.Mahjong.Sichuan.Scoring;
using Game.Mahjong.Sichuan.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.AI;

public sealed class BasicSichuanMahjongAi
{
    public IGameCommand ChooseCommand(
        SichuanMahjongSnapshot snapshot,
        MahjongSeat seat,
        IReadOnlyList<SichuanMahjongAction> legalActions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(legalActions);
        if (legalActions.Count == 0)
        {
            throw new InvalidOperationException("AI requires at least one legal action.");
        }

        var win = legalActions.FirstOrDefault(action =>
            action.Kind is SichuanMahjongActionKind.SelfDrawWin or SichuanMahjongActionKind.DiscardWin);
        if (win is not null)
        {
            return new DeclareMahjongWinCommand((int)seat);
        }

        if (snapshot.Phase == SichuanMahjongPhase.ExchangeThree)
        {
            var hand = snapshot.Table.Hands[(int)seat];
            var exchange = legalActions
                .Where(action => action.Kind == SichuanMahjongActionKind.ExchangeThree)
                .OrderBy(action => hand.Count(tile =>
                    tile.Kind.GetSuit() == action.ConcealedTiles[0].Kind.GetSuit()))
                .ThenByDescending(action => action.ConcealedTiles.Sum(tile =>
                    DiscardIsolation(tile.Kind, hand)))
                .ThenBy(action => action.ConcealedTiles[0].Kind.GetSuit())
                .First();
            return new ExchangeThreeTilesCommand((int)seat, exchange.ConcealedTiles);
        }

        if (snapshot.Phase == SichuanMahjongPhase.DeclareVoidSuit)
        {
            var hand = snapshot.Table.Hands[(int)seat];
            var declaration = legalActions
                .Where(action => action.Kind == SichuanMahjongActionKind.DeclareVoidSuit)
                .OrderBy(action => hand.Count(tile => tile.Kind.GetSuit() == action.Suit))
                .ThenBy(action => action.Suit)
                .First();
            return new DeclareVoidSuitCommand((int)seat, declaration.Suit!.Value);
        }

        var kong = legalActions.FirstOrDefault(action => action.Kind is
            SichuanMahjongActionKind.ConcealedKong or
            SichuanMahjongActionKind.AddedKong or
            SichuanMahjongActionKind.OpenKong);
        if (kong is not null)
        {
            return kong.Kind switch
            {
                SichuanMahjongActionKind.ConcealedKong =>
                    new DeclareConcealedKongCommand((int)seat, kong.ConcealedTiles),
                SichuanMahjongActionKind.AddedKong =>
                    new DeclareAddedKongCommand((int)seat, kong.Tile!.Value),
                _ => new ClaimMahjongDiscardCommand(
                    (int)seat,
                    MahjongMeldType.OpenKong,
                    kong.ConcealedTiles),
            };
        }

        var pong = legalActions.FirstOrDefault(action => action.Kind == SichuanMahjongActionKind.Pong);
        if (pong is not null)
        {
            return new ClaimMahjongDiscardCommand(
                (int)seat,
                MahjongMeldType.Pong,
                pong.ConcealedTiles);
        }

        var discardActions = legalActions
            .Where(action => action.Kind == SichuanMahjongActionKind.Discard)
            .ToArray();
        if (discardActions.Length > 0)
        {
            var hand = snapshot.Table.Hands[(int)seat];
            var meldCount = snapshot.Table.Melds[(int)seat].Count;
            var voidSuit = snapshot.VoidSuits[(int)seat]!.Value;
            var discard = discardActions
                .Select(action => new
                {
                    Action = action,
                    WaitCount = SichuanMahjongScorer.GetWinningKinds(
                        hand.Where(tile => tile != action.Tile).Select(tile => tile.Kind),
                        meldCount,
                        voidSuit).Count,
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
        var nearby = hand.Count(tile =>
            tile.Kind.GetSuit() == kind.GetSuit()
            && Math.Abs(tile.Kind.GetNumber() - kind.GetNumber()) <= 2);
        return kind.IsTerminal() ? 2 - nearby : 1 - nearby;
    }
}
