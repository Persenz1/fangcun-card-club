using Game.Mahjong.Hands;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Application.Mahjong;

public enum MahjongActionViewKind
{
    Discard,
    Win,
    Chow,
    Pong,
    OpenKong,
    ConcealedKong,
    AddedKong,
    Pass,
    ExchangeThree,
    DeclareVoidSuit,
    RiichiDiscard,
    NineTerminalsDraw,
}

public sealed class MahjongActionOption
{
    public MahjongActionOption(
        int id,
        MahjongActionViewKind kind,
        string label,
        MahjongTile? primaryTile = null,
        IEnumerable<MahjongTile>? tiles = null,
        MahjongTileSuit? suit = null)
    {
        Id = id;
        Kind = kind;
        Label = label;
        PrimaryTile = primaryTile;
        Tiles = Array.AsReadOnly((tiles ?? []).ToArray());
        Suit = suit;
    }

    public int Id { get; }

    public MahjongActionViewKind Kind { get; }

    public string Label { get; }

    public MahjongTile? PrimaryTile { get; }

    public IReadOnlyList<MahjongTile> Tiles { get; }

    public MahjongTileSuit? Suit { get; }
}

public sealed record MahjongPresentedTile(
    MahjongTile? Tile,
    bool FaceUp,
    bool IsDrawn);

public sealed class MahjongMeldView
{
    public MahjongMeldView(MahjongMeldType type, IEnumerable<MahjongTile> tiles, MahjongSeat? sourceSeat)
    {
        Type = type;
        Tiles = Array.AsReadOnly(tiles.ToArray());
        SourceSeat = sourceSeat;
    }

    public MahjongMeldType Type { get; }

    public IReadOnlyList<MahjongTile> Tiles { get; }

    public MahjongSeat? SourceSeat { get; }
}

public sealed record MahjongRiverTileView(
    MahjongTile Tile,
    bool IsTsumogiri,
    bool IsClaimed,
    long Sequence);

public sealed class MahjongSeatView
{
    public MahjongSeatView(
        MahjongSeat seat,
        string name,
        IEnumerable<MahjongPresentedTile> hand,
        IEnumerable<MahjongMeldView> melds,
        IEnumerable<MahjongRiverTileView> river,
        long score,
        IEnumerable<string> status)
    {
        Seat = seat;
        Name = name;
        Hand = Array.AsReadOnly(hand.ToArray());
        Melds = Array.AsReadOnly(melds.ToArray());
        River = Array.AsReadOnly(river.ToArray());
        Score = score;
        Status = Array.AsReadOnly(status.ToArray());
    }

    public MahjongSeat Seat { get; }

    public string Name { get; }

    public IReadOnlyList<MahjongPresentedTile> Hand { get; }

    public IReadOnlyList<MahjongMeldView> Melds { get; }

    public IReadOnlyList<MahjongRiverTileView> River { get; }

    public long Score { get; }

    public IReadOnlyList<string> Status { get; }
}

public sealed class MahjongTableView
{
    public MahjongTableView(
        MahjongSeat dealer,
        MahjongSeat currentSeat,
        MahjongSeat? offeredReactionSeat,
        IEnumerable<MahjongSeatView> seats,
        int liveTilesRemaining,
        int replacementTilesRemaining)
    {
        Dealer = dealer;
        CurrentSeat = currentSeat;
        OfferedReactionSeat = offeredReactionSeat;
        Seats = Array.AsReadOnly(seats.ToArray());
        LiveTilesRemaining = liveTilesRemaining;
        ReplacementTilesRemaining = replacementTilesRemaining;
    }

    public MahjongSeat Dealer { get; }

    public MahjongSeat CurrentSeat { get; }

    public MahjongSeat? OfferedReactionSeat { get; }

    public IReadOnlyList<MahjongSeatView> Seats { get; }

    public int LiveTilesRemaining { get; }

    public int ReplacementTilesRemaining { get; }
}

public sealed record MahjongHudItem(string Label, string Value);

public sealed record MahjongLocalOutcome(long ScoreChange, bool Won);

public sealed class MahjongSessionView
{
    public MahjongSessionView(
        MahjongMode mode,
        MahjongSeat humanSeat,
        string phase,
        string prompt,
        MahjongTableView table,
        IEnumerable<MahjongHudItem> hudItems,
        IEnumerable<MahjongTileKind> doraIndicators,
        IEnumerable<MahjongActionOption> legalActions,
        int? suggestedActionId,
        bool canAdvanceAi,
        bool isFinished,
        IEnumerable<string>? settlementLines = null,
        MahjongLocalOutcome? localOutcome = null)
    {
        Mode = mode;
        HumanSeat = humanSeat;
        Phase = phase;
        Prompt = prompt;
        Table = table;
        HudItems = Array.AsReadOnly(hudItems.ToArray());
        DoraIndicators = Array.AsReadOnly(doraIndicators.ToArray());
        LegalActions = Array.AsReadOnly(legalActions.ToArray());
        SuggestedActionId = suggestedActionId;
        CanAdvanceAi = canAdvanceAi;
        IsFinished = isFinished;
        SettlementLines = Array.AsReadOnly((settlementLines ?? []).ToArray());
        LocalOutcome = localOutcome;
    }

    public MahjongMode Mode { get; }

    public MahjongSeat HumanSeat { get; }

    public string Phase { get; }

    public string Prompt { get; }

    public MahjongTableView Table { get; }

    public IReadOnlyList<MahjongHudItem> HudItems { get; }

    public IReadOnlyList<MahjongTileKind> DoraIndicators { get; }

    public IReadOnlyList<MahjongActionOption> LegalActions { get; }

    public int? SuggestedActionId { get; }

    public bool IsHumanActionRequired => LegalActions.Count > 0;

    public bool CanAdvanceAi { get; }

    public bool IsFinished { get; }

    public IReadOnlyList<string> SettlementLines { get; }

    public MahjongLocalOutcome? LocalOutcome { get; }
}

public enum MahjongAnimationEventKind
{
    Draw,
    Discard,
    Meld,
    Pass,
    Exchange,
    Declaration,
    Dora,
    Win,
    HandFinished,
    MatchFinished,
}

public sealed class MahjongAnimationEvent
{
    public MahjongAnimationEvent(
        MahjongAnimationEventKind kind,
        string message,
        MahjongSeat? seat = null,
        MahjongTile? tile = null,
        MahjongMeldView? meld = null)
    {
        Kind = kind;
        Message = message;
        Seat = seat;
        Tile = tile;
        Meld = meld;
        DurationMilliseconds = MahjongAnimationTiming.For(kind);
    }

    public MahjongAnimationEventKind Kind { get; }

    public string Message { get; }

    public MahjongSeat? Seat { get; }

    public MahjongTile? Tile { get; }

    public MahjongMeldView? Meld { get; }

    public int DurationMilliseconds { get; }
}

public static class MahjongAnimationTiming
{
    public const int AiThinkMilliseconds = 260;

    public static int For(MahjongAnimationEventKind kind)
    {
        return kind switch
        {
            MahjongAnimationEventKind.Draw => 110,
            MahjongAnimationEventKind.Discard => 170,
            MahjongAnimationEventKind.Meld => 240,
            MahjongAnimationEventKind.Pass => 80,
            MahjongAnimationEventKind.Exchange or MahjongAnimationEventKind.Declaration => 220,
            MahjongAnimationEventKind.Dora => 220,
            MahjongAnimationEventKind.Win => 420,
            MahjongAnimationEventKind.HandFinished => 520,
            MahjongAnimationEventKind.MatchFinished => 700,
            _ => 120,
        };
    }
}

internal static class MahjongPresentationBuilder
{
    public static MahjongTableView CreateTable(
        MahjongTableSnapshot table,
        MahjongSeat humanSeat,
        MahjongSeat? offeredReactionSeat,
        IReadOnlyList<long>? scores = null,
        IReadOnlyList<IReadOnlyList<string>>? statuses = null)
    {
        var seats = Enum.GetValues<MahjongSeat>().Select(seat =>
        {
            var index = (int)seat;
            var hand = CreateHand(table, seat, humanSeat);
            var melds = table.Melds[index].Select(FromMeld);
            var river = table.Rivers[index].Select(tile => new MahjongRiverTileView(
                tile.Tile,
                tile.IsTsumogiri,
                tile.IsClaimed,
                tile.Sequence));
            return new MahjongSeatView(
                seat,
                MahjongText.Seat(seat),
                hand,
                melds,
                river,
                scores is null ? 0 : scores[index],
                statuses is null ? [] : statuses[index]);
        });

        return new MahjongTableView(
            table.Dealer,
            table.CurrentSeat,
            offeredReactionSeat,
            seats,
            table.LiveTilesRemaining,
            table.ReplacementTilesRemaining);
    }

    public static MahjongMeldView FromMeld(MahjongMeld meld)
    {
        return new MahjongMeldView(meld.Type, meld.Tiles, meld.SourceSeat);
    }

    private static IReadOnlyList<MahjongPresentedTile> CreateHand(
        MahjongTableSnapshot table,
        MahjongSeat seat,
        MahjongSeat humanSeat)
    {
        var source = table.Hands[(int)seat];
        var drawnTile = seat == table.CurrentSeat ? table.LastDrawnTile : null;
        if (seat != humanSeat)
        {
            return source
                .Select((_, index) => new MahjongPresentedTile(
                    null,
                    false,
                    drawnTile is not null && index == source.Count - 1))
                .ToArray();
        }

        return source
            .OrderBy(tile => drawnTile == tile ? 1 : 0)
            .ThenBy(tile => tile.Kind)
            .ThenBy(tile => tile.CopyIndex)
            .Select(tile => new MahjongPresentedTile(tile, true, drawnTile == tile))
            .ToArray();
    }
}

public static class MahjongText
{
    private static readonly string[] Numerals = ["一", "二", "三", "四", "五", "六", "七", "八", "九"];

    public static string Tile(MahjongTile tile) => Tile(tile.Kind);

    public static string Tile(MahjongTileKind kind)
    {
        if (kind.IsSuited())
        {
            var suffix = kind.GetSuit() switch
            {
                MahjongTileSuit.Characters => "万",
                MahjongTileSuit.Dots => "筒",
                MahjongTileSuit.Bamboo => "条",
                _ => string.Empty,
            };
            return Numerals[kind.GetNumber() - 1] + suffix;
        }

        return kind switch
        {
            MahjongTileKind.East => "东",
            MahjongTileKind.South => "南",
            MahjongTileKind.West => "西",
            MahjongTileKind.North => "北",
            MahjongTileKind.White => "白",
            MahjongTileKind.Green => "发",
            MahjongTileKind.Red => "中",
            _ => kind.ToString(),
        };
    }

    public static string Tiles(IEnumerable<MahjongTile> tiles)
    {
        return string.Join(" ", tiles.Select(Tile));
    }

    public static string Suit(MahjongTileSuit suit)
    {
        return suit switch
        {
            MahjongTileSuit.Characters => "万",
            MahjongTileSuit.Dots => "筒",
            MahjongTileSuit.Bamboo => "条",
            MahjongTileSuit.Honors => "字",
            _ => suit.ToString(),
        };
    }

    public static string Seat(MahjongSeat seat)
    {
        return seat switch
        {
            MahjongSeat.East => "东家",
            MahjongSeat.South => "南家",
            MahjongSeat.West => "西家",
            MahjongSeat.North => "北家",
            _ => seat.ToString(),
        };
    }

    public static string Meld(MahjongMeldType type)
    {
        return type switch
        {
            MahjongMeldType.Chow => "吃",
            MahjongMeldType.Pong => "碰",
            MahjongMeldType.OpenKong => "明杠",
            MahjongMeldType.ConcealedKong => "暗杠",
            MahjongMeldType.AddedKong => "加杠",
            _ => type.ToString(),
        };
    }
}
