using Game.Core.Random;
using Game.Core.Simulation;
using Game.Doudizhu.Cards;
using Game.Doudizhu.Commands;
using Game.Doudizhu.Events;
using Game.Doudizhu.Moves;
using Game.Doudizhu.Patterns;
using Game.Doudizhu.Settlement;

namespace Game.Doudizhu.State;

public sealed class DoudizhuRuleEngine
{
    public const int PlayerCount = 3;

    private readonly DoudizhuRuleConfig _config;
    private readonly List<Card>[] _hands = Enumerable.Range(0, PlayerCount)
        .Select(_ => new List<Card>(20))
        .ToArray();
    private readonly IDeterministicRandom _random;
    private readonly HashSet<int> _callPassers = [];
    private readonly Queue<int> _robCandidates = [];
    private readonly int[] _successfulPlayCounts = new int[PlayerCount];
    private Card[] _bottomCards = [];
    private bool _counterOffer;
    private int _currentPlayerIndex;
    private int _firstBidderIndex;
    private DoudizhuMove? _lastMove;
    private int? _lastMovePlayerIndex;
    private int? _landlordIndex;
    private int _lastBidderIndex;
    private int _multiplier;
    private int _originalCallerIndex;
    private DoudizhuPhase _phase;
    private int _redealCount;
    private bool _robOccurred;
    private long _sequence;
    private DoudizhuSettlement? _settlement;
    private int _successivePassCount;

    public DoudizhuRuleEngine(IDeterministicRandom random, DoudizhuRuleConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        _random = random;
        _config = config ?? new DoudizhuRuleConfig();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_config.BaseScore);

        _firstBidderIndex = _random.NextInt(PlayerCount);
        Deal();
    }

    public DoudizhuSnapshot Snapshot => CreateSnapshot();

    public DoudizhuCommandResult Dispatch(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PlayerIndex is < 0 or >= PlayerCount)
        {
            return Reject("玩家座位无效。");
        }

        return command switch
        {
            BidCommand bid => ApplyBid(bid),
            PlayCardsCommand play => ApplyPlay(play),
            PassCommand pass => ApplyPass(pass),
            _ => Reject("当前对局不支持该命令。"),
        };
    }

    public IReadOnlyList<DoudizhuMove> GetLegalMoves(int playerIndex)
    {
        if (_phase != DoudizhuPhase.Playing || playerIndex != _currentPlayerIndex)
        {
            return [];
        }

        return LegalMoveGenerator.Generate(_hands[playerIndex], _lastMove?.Pattern);
    }

    public DoudizhuObservation GetObservation(int playerIndex)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(playerIndex, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(playerIndex, PlayerCount);

        var bottomCardsAreVisible = _phase is DoudizhuPhase.Playing or DoudizhuPhase.Finished;
        return new DoudizhuObservation(
            playerIndex,
            _phase,
            _currentPlayerIndex,
            CurrentBidPrompt,
            _landlordIndex,
            _hands[playerIndex],
            _hands.Select(hand => hand.Count),
            bottomCardsAreVisible ? _bottomCards : [],
            _lastMove,
            _lastMovePlayerIndex,
            _multiplier,
            _redealCount,
            _phase == DoudizhuPhase.Playing && _lastMove is not null);
    }

    private DoudizhuBidPrompt? CurrentBidPrompt { get; set; }

    private DoudizhuCommandResult ApplyBid(BidCommand command)
    {
        if (_phase != DoudizhuPhase.Bidding)
        {
            return Reject("当前不在叫地主阶段。");
        }

        if (command.PlayerIndex != _currentPlayerIndex)
        {
            return Reject("尚未轮到该玩家叫抢地主。");
        }

        var actionIsLegal = CurrentBidPrompt switch
        {
            DoudizhuBidPrompt.Call => command.Action is DoudizhuBidAction.Call or DoudizhuBidAction.Pass,
            DoudizhuBidPrompt.Rob => command.Action is DoudizhuBidAction.Rob or DoudizhuBidAction.Pass,
            _ => false,
        };
        if (!actionIsLegal)
        {
            return Reject("叫抢动作与当前提示不符。");
        }

        var events = new List<IGameEvent>();
        if (CurrentBidPrompt == DoudizhuBidPrompt.Call)
        {
            ApplyCallDecision(command, events);
        }
        else
        {
            ApplyRobDecision(command, events);
        }

        return Accept(events);
    }

    private void ApplyCallDecision(BidCommand command, ICollection<IGameEvent> events)
    {
        events.Add(new BidMadeEvent(NextSequence(), command.PlayerIndex, command.Action, _multiplier));

        if (command.Action == DoudizhuBidAction.Pass)
        {
            _callPassers.Add(command.PlayerIndex);
            if (_callPassers.Count == PlayerCount)
            {
                _redealCount++;
                _firstBidderIndex = NextPlayer(_firstBidderIndex);
                Deal();
                events.Add(new CardsRedealtEvent(NextSequence(), _redealCount, _firstBidderIndex));
                return;
            }

            _currentPlayerIndex = NextPlayer(_currentPlayerIndex);
            return;
        }

        _originalCallerIndex = command.PlayerIndex;
        _lastBidderIndex = command.PlayerIndex;
        _robOccurred = false;
        _counterOffer = false;
        _robCandidates.Clear();

        for (var offset = 1; offset < PlayerCount; offset++)
        {
            var candidate = (command.PlayerIndex + offset) % PlayerCount;
            if (!_callPassers.Contains(candidate))
            {
                _robCandidates.Enqueue(candidate);
            }
        }

        OfferNextRobOrDetermineLandlord(events);
    }

    private void ApplyRobDecision(BidCommand command, ICollection<IGameEvent> events)
    {
        if (command.Action == DoudizhuBidAction.Rob)
        {
            _lastBidderIndex = command.PlayerIndex;
            _multiplier = checked(_multiplier * 2);
            _robOccurred = true;
        }

        events.Add(new BidMadeEvent(NextSequence(), command.PlayerIndex, command.Action, _multiplier));

        if (_counterOffer)
        {
            DetermineLandlord(events);
            return;
        }

        OfferNextRobOrDetermineLandlord(events);
    }

    private void OfferNextRobOrDetermineLandlord(ICollection<IGameEvent> events)
    {
        if (_robCandidates.TryDequeue(out var candidate))
        {
            _currentPlayerIndex = candidate;
            CurrentBidPrompt = DoudizhuBidPrompt.Rob;
            return;
        }

        if (_robOccurred)
        {
            _counterOffer = true;
            _currentPlayerIndex = _originalCallerIndex;
            CurrentBidPrompt = DoudizhuBidPrompt.Rob;
            return;
        }

        DetermineLandlord(events);
    }

    private void DetermineLandlord(ICollection<IGameEvent> events)
    {
        _landlordIndex = _lastBidderIndex;
        _hands[_landlordIndex.Value].AddRange(_bottomCards);
        SortHand(_hands[_landlordIndex.Value]);
        _phase = DoudizhuPhase.Playing;
        _currentPlayerIndex = _landlordIndex.Value;
        CurrentBidPrompt = null;
        events.Add(new LandlordDeterminedEvent(
            NextSequence(),
            _landlordIndex.Value,
            Array.AsReadOnly((Card[])_bottomCards.Clone()),
            _multiplier));
    }

    private DoudizhuCommandResult ApplyPlay(PlayCardsCommand command)
    {
        if (_phase != DoudizhuPhase.Playing)
        {
            return Reject("当前不在出牌阶段。");
        }

        if (command.PlayerIndex != _currentPlayerIndex)
        {
            return Reject("尚未轮到该玩家出牌。");
        }

        var legalMove = GetLegalMoves(command.PlayerIndex)
            .FirstOrDefault(move => SameCards(move.Cards, command.Cards));
        if (legalMove is null)
        {
            return Reject("所选牌不是当前合法出牌。");
        }

        foreach (var card in legalMove.Cards)
        {
            _hands[command.PlayerIndex].Remove(card);
        }

        _successfulPlayCounts[command.PlayerIndex]++;
        _lastMove = legalMove;
        _lastMovePlayerIndex = command.PlayerIndex;
        _successivePassCount = 0;
        if (legalMove.Pattern.Kind is CardPatternKind.Bomb or CardPatternKind.Rocket)
        {
            _multiplier = checked(_multiplier * 2);
        }

        var events = new List<IGameEvent>
        {
            new CardsPlayedEvent(NextSequence(), command.PlayerIndex, legalMove, _multiplier),
        };

        if (_hands[command.PlayerIndex].Count == 0)
        {
            Finish(command.PlayerIndex, events);
        }
        else
        {
            _currentPlayerIndex = NextPlayer(_currentPlayerIndex);
        }

        return Accept(events);
    }

    private DoudizhuCommandResult ApplyPass(PassCommand command)
    {
        if (_phase != DoudizhuPhase.Playing)
        {
            return Reject("当前不在出牌阶段。");
        }

        if (command.PlayerIndex != _currentPlayerIndex)
        {
            return Reject("尚未轮到该玩家出牌。");
        }

        if (_lastMove is null)
        {
            return Reject("领出玩家不能不出。");
        }

        var events = new List<IGameEvent>
        {
            new PlayerPassedEvent(NextSequence(), command.PlayerIndex),
        };
        _successivePassCount++;
        _currentPlayerIndex = NextPlayer(_currentPlayerIndex);

        if (_successivePassCount == PlayerCount - 1)
        {
            _lastMove = null;
            _lastMovePlayerIndex = null;
            _successivePassCount = 0;
            events.Add(new TrickResetEvent(NextSequence(), _currentPlayerIndex));
        }

        return Accept(events);
    }

    private void Finish(int winnerIndex, ICollection<IGameEvent> events)
    {
        _phase = DoudizhuPhase.Settling;
        _settlement = SettlementCalculator.Calculate(
            _config.BaseScore,
            _multiplier,
            _landlordIndex!.Value,
            winnerIndex,
            _successfulPlayCounts);
        _multiplier = _settlement.FinalMultiplier;
        _phase = DoudizhuPhase.Finished;
        events.Add(new DoudizhuFinishedEvent(NextSequence(), _settlement));
    }

    private void Deal()
    {
        _phase = DoudizhuPhase.Dealing;
        foreach (var hand in _hands)
        {
            hand.Clear();
        }

        var deck = CardDeck.CreateShuffled(_random);
        for (var cardIndex = 0; cardIndex < 51; cardIndex++)
        {
            _hands[cardIndex % PlayerCount].Add(deck[cardIndex]);
        }

        foreach (var hand in _hands)
        {
            SortHand(hand);
        }

        _bottomCards = deck.Skip(51).ToArray();
        _callPassers.Clear();
        _robCandidates.Clear();
        Array.Clear(_successfulPlayCounts);
        _counterOffer = false;
        _currentPlayerIndex = _firstBidderIndex;
        _lastMove = null;
        _lastMovePlayerIndex = null;
        _landlordIndex = null;
        _multiplier = 1;
        _robOccurred = false;
        _settlement = null;
        _successivePassCount = 0;
        CurrentBidPrompt = DoudizhuBidPrompt.Call;
        _phase = DoudizhuPhase.Bidding;
    }

    private DoudizhuSnapshot CreateSnapshot()
    {
        return new DoudizhuSnapshot(
            _phase,
            _currentPlayerIndex,
            _firstBidderIndex,
            CurrentBidPrompt,
            _landlordIndex,
            _hands,
            _bottomCards,
            _lastMove,
            _lastMovePlayerIndex,
            _multiplier,
            _redealCount,
            _successfulPlayCounts,
            _settlement);
    }

    private DoudizhuCommandResult Accept(IReadOnlyList<IGameEvent> events)
    {
        return new DoudizhuCommandResult(true, CreateSnapshot(), events);
    }

    private DoudizhuCommandResult Reject(string error)
    {
        return new DoudizhuCommandResult(false, CreateSnapshot(), [], error);
    }

    private long NextSequence()
    {
        return ++_sequence;
    }

    private static int NextPlayer(int playerIndex)
    {
        return (playerIndex + 1) % PlayerCount;
    }

    private static bool SameCards(IReadOnlyCollection<Card> first, IReadOnlyCollection<Card> second)
    {
        return first.Count == second.Count && first.All(second.Contains);
    }

    private static void SortHand(List<Card> hand)
    {
        hand.Sort(static (left, right) =>
        {
            var rankComparison = right.Rank.CompareTo(left.Rank);
            return rankComparison != 0 ? rankComparison : left.Suit.CompareTo(right.Suit);
        });
    }
}
