using Game.Core.Random;
using Game.Mahjong.Hands;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Table;

public sealed class MahjongTableState
{
    private readonly MahjongHand[] _hands = Enumerable.Range(0, 4)
        .Select(_ => new MahjongHand())
        .ToArray();
    private readonly List<MahjongRiverTile>[] _rivers = Enumerable.Range(0, 4)
        .Select(_ => new List<MahjongRiverTile>())
        .ToArray();
    private MahjongTile? _lastDrawnTile;
    private MahjongSeat? _lastDiscardSeat;
    private bool _tilesWereExchanged;
    private long _riverSequence;

    public MahjongTableState(
        IDeterministicRandom random,
        MahjongSeat dealer = MahjongSeat.East,
        int deadWallSize = 0,
        int replacementLimit = 4,
        bool drawDealerOpeningTile = true)
        : this(new MahjongWall(random, deadWallSize, replacementLimit), dealer, drawDealerOpeningTile)
    {
    }

    public MahjongTableState(
        MahjongWall wall,
        MahjongSeat dealer = MahjongSeat.East,
        bool drawDealerOpeningTile = true)
    {
        ArgumentNullException.ThrowIfNull(wall);
        if (!Enum.IsDefined(dealer))
        {
            throw new ArgumentOutOfRangeException(nameof(dealer));
        }

        Wall = wall;
        Dealer = dealer;
        CurrentSeat = dealer;
        DealStartingHands();
        if (drawDealerOpeningTile)
        {
            DrawCurrent();
        }
    }

    public MahjongSeat Dealer { get; }

    public MahjongSeat CurrentSeat { get; private set; }

    public MahjongWall Wall { get; }

    public MahjongRiverTile? LastDiscard => _lastDiscardSeat is { } seat
        ? _rivers[(int)seat].LastOrDefault(tile => tile.Sequence == _riverSequence)
        : null;

    public MahjongSeat? LastDiscardSeat => _lastDiscardSeat;

    public MahjongTableSnapshot Snapshot => new(
        Dealer,
        CurrentSeat,
        _hands.Select(hand => hand.ConcealedTiles),
        _hands.Select(hand => hand.Melds),
        _rivers,
        _lastDrawnTile,
        _lastDiscardSeat,
        LastDiscard,
        Wall.LiveTilesRemaining,
        Wall.ReplacementTilesRemaining);

    public IReadOnlyList<MahjongTile> GetConcealedTiles(MahjongSeat seat)
    {
        ValidateSeat(seat);
        return _hands[(int)seat].ConcealedTiles;
    }

    public IReadOnlyList<MahjongMeld> GetMelds(MahjongSeat seat)
    {
        ValidateSeat(seat);
        return _hands[(int)seat].Melds;
    }

    public IReadOnlyList<MahjongRiverTile> GetRiver(MahjongSeat seat)
    {
        ValidateSeat(seat);
        return Array.AsReadOnly(_rivers[(int)seat].ToArray());
    }

    public MahjongTile DrawCurrent(bool replacement = false)
    {
        if (_lastDrawnTile is not null)
        {
            throw new InvalidOperationException("The current player must discard before drawing again.");
        }

        var tile = replacement ? Wall.DrawReplacement() : Wall.DrawLive();
        _hands[(int)CurrentSeat].AddTile(tile);
        _lastDrawnTile = tile;
        _lastDiscardSeat = null;
        return tile;
    }

    public MahjongRiverTile Discard(MahjongSeat seat, MahjongTile tile)
    {
        EnsureCurrentSeat(seat);
        _hands[(int)seat].RemoveTiles([tile]);
        var riverTile = new MahjongRiverTile(
            tile,
            _lastDrawnTile == tile,
            false,
            ++_riverSequence);
        _rivers[(int)seat].Add(riverTile);
        _lastDrawnTile = null;
        _lastDiscardSeat = seat;
        CurrentSeat = seat.Next();
        return riverTile;
    }

    public MahjongMeld ClaimDiscard(
        MahjongSeat caller,
        MahjongMeldType type,
        IEnumerable<MahjongTile> concealedTiles)
    {
        ValidateSeat(caller);
        var lastDiscard = LastDiscard ?? throw new InvalidOperationException("There is no discard to claim.");
        var sourceSeat = _lastDiscardSeat!.Value;
        if (caller == sourceSeat || type is not (MahjongMeldType.Chow or MahjongMeldType.Pong or MahjongMeldType.OpenKong))
        {
            throw new InvalidOperationException("The requested discard claim is not a common open meld.");
        }

        var usedTiles = concealedTiles.ToArray();
        var meld = new MahjongMeld(type, usedTiles.Append(lastDiscard.Tile), sourceSeat);
        _hands[(int)caller].AddMeld(meld, usedTiles);
        MarkLastDiscardClaimed(sourceSeat);
        CurrentSeat = caller;
        _lastDrawnTile = null;
        _lastDiscardSeat = null;
        return meld;
    }

    public MahjongMeld DeclareConcealedKong(MahjongSeat seat, IEnumerable<MahjongTile> tiles)
    {
        EnsureCurrentSeat(seat);
        var materializedTiles = tiles.ToArray();
        var meld = new MahjongMeld(MahjongMeldType.ConcealedKong, materializedTiles);
        _hands[(int)seat].AddMeld(meld, materializedTiles);
        _lastDrawnTile = null;
        return meld;
    }

    public MahjongMeld DeclareAddedKong(MahjongSeat seat, MahjongTile fourthTile)
    {
        EnsureCurrentSeat(seat);
        var meld = _hands[(int)seat].UpgradePong(fourthTile);
        _lastDrawnTile = null;
        return meld;
    }

    public void MoveTurnTo(MahjongSeat seat)
    {
        ValidateSeat(seat);
        if (_lastDrawnTile is not null)
        {
            throw new InvalidOperationException("Cannot move the turn while a drawn tile is waiting for discard.");
        }

        CurrentSeat = seat;
    }

    public void EndCurrentTurnWithoutDiscard(MahjongSeat seat)
    {
        EnsureCurrentSeat(seat);
        if (_lastDrawnTile is null)
        {
            throw new InvalidOperationException("The current player has no completed draw to end.");
        }

        _lastDrawnTile = null;
    }

    public void ExchangeTiles(
        IReadOnlyDictionary<MahjongSeat, IReadOnlyList<MahjongTile>> outgoingTiles,
        int recipientOffset)
    {
        ArgumentNullException.ThrowIfNull(outgoingTiles);
        if (recipientOffset is < 1 or > 3
            || outgoingTiles.Count != 4
            || _tilesWereExchanged
            || _lastDrawnTile is not null
            || _rivers.Any(river => river.Count > 0)
            || _hands.Any(hand => hand.Melds.Count > 0))
        {
            throw new InvalidOperationException("Tiles can be exchanged only once before normal play starts.");
        }

        foreach (var seat in Enum.GetValues<MahjongSeat>())
        {
            if (!outgoingTiles.TryGetValue(seat, out var tiles)
                || tiles.Count != 3
                || tiles.Distinct().Count() != 3
                || tiles.Any(tile => !_hands[(int)seat].ConcealedTiles.Contains(tile)))
            {
                throw new InvalidOperationException("Every seat must exchange three physical tiles from its hand.");
            }
        }

        foreach (var (seat, tiles) in outgoingTiles)
        {
            _hands[(int)seat].RemoveTiles(tiles);
        }

        foreach (var (seat, tiles) in outgoingTiles)
        {
            var recipient = (MahjongSeat)(((int)seat + recipientOffset) % 4);
            _hands[(int)recipient].AddTiles(tiles);
        }

        _tilesWereExchanged = true;
    }

    private void DealStartingHands()
    {
        for (var tileNumber = 0; tileNumber < 13; tileNumber++)
        {
            for (var offset = 0; offset < 4; offset++)
            {
                var seat = (MahjongSeat)(((int)Dealer + offset) % 4);
                _hands[(int)seat].AddTile(Wall.DrawLive());
            }
        }
    }

    private void MarkLastDiscardClaimed(MahjongSeat sourceSeat)
    {
        var river = _rivers[(int)sourceSeat];
        var index = river.FindLastIndex(tile => tile.Sequence == _riverSequence);
        river[index] = river[index] with { IsClaimed = true };
    }

    private void EnsureCurrentSeat(MahjongSeat seat)
    {
        ValidateSeat(seat);
        if (seat != CurrentSeat)
        {
            throw new InvalidOperationException("It is not this seat's turn.");
        }
    }

    private static void ValidateSeat(MahjongSeat seat)
    {
        if (!Enum.IsDefined(seat))
        {
            throw new ArgumentOutOfRangeException(nameof(seat));
        }
    }
}
