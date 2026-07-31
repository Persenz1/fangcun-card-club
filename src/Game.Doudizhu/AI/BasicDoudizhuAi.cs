using Game.Core.Simulation;
using Game.Doudizhu.Cards;
using Game.Doudizhu.Commands;
using Game.Doudizhu.Moves;
using Game.Doudizhu.Patterns;
using Game.Doudizhu.State;

namespace Game.Doudizhu.AI;

public sealed class BasicDoudizhuAi
{
    public IGameCommand ChooseCommand(
        DoudizhuObservation observation,
        IReadOnlyList<DoudizhuMove> legalMoves)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(legalMoves);

        if (observation.PlayerIndex != observation.CurrentPlayerIndex)
        {
            throw new InvalidOperationException("AI can act only for the current player.");
        }

        return observation.Phase switch
        {
            DoudizhuPhase.Bidding => ChooseBid(observation),
            DoudizhuPhase.Playing => ChoosePlay(observation, legalMoves),
            _ => throw new InvalidOperationException("The game is not waiting for a player command."),
        };
    }

    private static IGameCommand ChooseBid(DoudizhuObservation observation)
    {
        var strength = EvaluateHandStrength(observation.Hand);
        var forceCall = observation.RedealCount >= 2 && observation.BidPrompt == DoudizhuBidPrompt.Call;
        var accept = observation.BidPrompt switch
        {
            DoudizhuBidPrompt.Call => forceCall || strength >= 9,
            DoudizhuBidPrompt.Rob => strength >= 14,
            _ => false,
        };
        var action = accept
            ? observation.BidPrompt == DoudizhuBidPrompt.Call
                ? DoudizhuBidAction.Call
                : DoudizhuBidAction.Rob
            : DoudizhuBidAction.Pass;

        return new BidCommand(observation.PlayerIndex, action);
    }

    private static IGameCommand ChoosePlay(
        DoudizhuObservation observation,
        IReadOnlyList<DoudizhuMove> legalMoves)
    {
        if (legalMoves.Count == 0)
        {
            return new PassCommand(observation.PlayerIndex);
        }

        var finishingMove = legalMoves.FirstOrDefault(move => move.Cards.Count == observation.Hand.Count);
        if (finishingMove is not null)
        {
            return new PlayCardsCommand(observation.PlayerIndex, finishingMove.Cards);
        }

        if (ShouldYieldToFarmerPartner(observation))
        {
            return new PassCommand(observation.PlayerIndex);
        }

        var landlordHasOneCard = observation.LandlordIndex is { } landlord
            && observation.RemainingCardCounts[landlord] == 1;
        var move = observation.LastMove is null
            ? legalMoves
                .OrderBy(IsBombOrRocket)
                .ThenBy(candidate => BreaksBomb(candidate, observation.Hand))
                .ThenByDescending(candidate => candidate.Cards.Count)
                .ThenBy(candidate => candidate.Pattern.MainRank)
                .First()
            : legalMoves
                .OrderBy(IsBombOrRocket)
                .ThenBy(candidate => BreaksBomb(candidate, observation.Hand))
                .ThenBy(candidate => landlordHasOneCard && candidate.Pattern.Kind == CardPatternKind.Single ? 0 : 1)
                .ThenByDescending(candidate => landlordHasOneCard && candidate.Pattern.Kind == CardPatternKind.Single
                    ? candidate.Pattern.MainRank
                    : CardRank.Three)
                .ThenBy(candidate => candidate.Pattern.MainRank)
                .First();

        return new PlayCardsCommand(observation.PlayerIndex, move.Cards);
    }

    private static bool ShouldYieldToFarmerPartner(DoudizhuObservation observation)
    {
        if (!observation.CanPass
            || observation.LandlordIndex is not { } landlord
            || observation.PlayerIndex == landlord
            || observation.LastMovePlayerIndex is not { } lastPlayer
            || lastPlayer == landlord)
        {
            return false;
        }

        return observation.RemainingCardCounts[landlord] > 2;
    }

    private static int EvaluateHandStrength(IEnumerable<Card> hand)
    {
        var cards = hand.ToArray();
        var score = cards.Sum(card => card.Rank switch
        {
            CardRank.BigJoker => 4,
            CardRank.SmallJoker => 3,
            CardRank.Two => 2,
            CardRank.Ace => 1,
            _ => 0,
        });
        score += cards
            .GroupBy(card => card.Rank)
            .Count(group => group.Count() == 4) * 5;

        if (cards.Any(card => card.Rank == CardRank.SmallJoker)
            && cards.Any(card => card.Rank == CardRank.BigJoker))
        {
            score += 3;
        }

        return score;
    }

    private static bool IsBombOrRocket(DoudizhuMove move)
    {
        return move.Pattern.Kind is CardPatternKind.Bomb or CardPatternKind.Rocket;
    }

    private static bool BreaksBomb(DoudizhuMove move, IReadOnlyCollection<Card> hand)
    {
        var bombRanks = hand
            .GroupBy(card => card.Rank)
            .Where(group => group.Count() == 4)
            .Select(group => group.Key)
            .ToHashSet();

        return move.Cards
            .GroupBy(card => card.Rank)
            .Any(group => bombRanks.Contains(group.Key) && group.Count() < 4);
    }
}
