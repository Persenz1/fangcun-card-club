using System.Text.Json.Serialization;
using Game.Core.Simulation;
using Game.Mahjong.Commands;
using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.Commands;
using Game.Mahjong.Sichuan.Commands;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Application.Mahjong;

public sealed class MahjongRecoveryState
{
    [JsonPropertyName("mode")]
    public MahjongMode Mode { get; set; }

    [JsonPropertyName("seed")]
    public ulong Seed { get; set; }

    [JsonPropertyName("base_score")]
    public int BaseScore { get; set; } = 10;

    [JsonPropertyName("human_seat")]
    public MahjongSeat HumanSeat { get; set; }

    [JsonPropertyName("accepted_commands")]
    public List<MahjongCommandRecord> AcceptedCommands { get; set; } = [];

    public void Validate()
    {
        if (!Enum.IsDefined(Mode)
            || !Enum.IsDefined(HumanSeat)
            || BaseScore <= 0
            || AcceptedCommands is null)
        {
            throw new InvalidDataException("麻将恢复记录配置无效。");
        }

        foreach (var command in AcceptedCommands)
        {
            _ = command?.ToCommand() ?? throw new InvalidDataException("麻将恢复记录包含空命令。");
        }
    }
}

public enum MahjongStoredCommandKind
{
    Discard,
    ClaimDiscard,
    ConcealedKong,
    AddedKong,
    Win,
    Pass,
    ExchangeThree,
    DeclareVoidSuit,
    Riichi,
    NineTerminalsDraw,
}

public sealed class MahjongCommandRecord
{
    [JsonPropertyName("kind")]
    public MahjongStoredCommandKind Kind { get; set; }

    [JsonPropertyName("player_index")]
    public int PlayerIndex { get; set; }

    [JsonPropertyName("tile")]
    public MahjongStoredTile? Tile { get; set; }

    [JsonPropertyName("tiles")]
    public List<MahjongStoredTile> Tiles { get; set; } = [];

    [JsonPropertyName("meld_type")]
    public MahjongMeldType? MeldType { get; set; }

    [JsonPropertyName("suit")]
    public MahjongTileSuit? Suit { get; set; }

    public static MahjongCommandRecord FromCommand(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            DiscardMahjongTileCommand discard => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.Discard,
                PlayerIndex = discard.PlayerIndex,
                Tile = MahjongStoredTile.FromTile(discard.Tile),
            },
            ClaimMahjongDiscardCommand claim => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.ClaimDiscard,
                PlayerIndex = claim.PlayerIndex,
                MeldType = claim.MeldType,
                Tiles = claim.ConcealedTiles.Select(MahjongStoredTile.FromTile).ToList(),
            },
            DeclareConcealedKongCommand kong => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.ConcealedKong,
                PlayerIndex = kong.PlayerIndex,
                Tiles = kong.Tiles.Select(MahjongStoredTile.FromTile).ToList(),
            },
            DeclareAddedKongCommand kong => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.AddedKong,
                PlayerIndex = kong.PlayerIndex,
                Tile = MahjongStoredTile.FromTile(kong.FourthTile),
            },
            DeclareMahjongWinCommand win => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.Win,
                PlayerIndex = win.PlayerIndex,
            },
            PassMahjongCommand pass => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.Pass,
                PlayerIndex = pass.PlayerIndex,
            },
            ExchangeThreeTilesCommand exchange => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.ExchangeThree,
                PlayerIndex = exchange.PlayerIndex,
                Tiles = exchange.Tiles.Select(MahjongStoredTile.FromTile).ToList(),
            },
            DeclareVoidSuitCommand declaration => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.DeclareVoidSuit,
                PlayerIndex = declaration.PlayerIndex,
                Suit = declaration.Suit,
            },
            DeclareRiichiCommand riichi => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.Riichi,
                PlayerIndex = riichi.PlayerIndex,
                Tile = MahjongStoredTile.FromTile(riichi.DiscardTile),
            },
            DeclareNineTerminalsDrawCommand draw => new MahjongCommandRecord
            {
                Kind = MahjongStoredCommandKind.NineTerminalsDraw,
                PlayerIndex = draw.PlayerIndex,
            },
            _ => throw new ArgumentException("不是可保存的麻将命令。", nameof(command)),
        };
    }

    public IGameCommand ToCommand()
    {
        if (PlayerIndex is < 0 or >= 4
            || Tiles is null
            || Tiles.Any(tile => tile is null)
            || !Enum.IsDefined(Kind)
            || MeldType is { } meldType && !Enum.IsDefined(meldType)
            || Suit is { } suit && !Enum.IsDefined(suit))
        {
            throw new InvalidDataException("麻将命令记录无效。");
        }

        var tile = Tile?.ToTile();
        var tiles = Tiles.Select(item => item.ToTile()).ToArray();
        return Kind switch
        {
            MahjongStoredCommandKind.Discard when tile is not null && HasNoOptions() =>
                new DiscardMahjongTileCommand(PlayerIndex, tile.Value),
            MahjongStoredCommandKind.ClaimDiscard
                when tile is null && Suit is null && MeldType is MahjongMeldType.Chow
                    or MahjongMeldType.Pong or MahjongMeldType.OpenKong =>
                new ClaimMahjongDiscardCommand(PlayerIndex, MeldType.Value, tiles),
            MahjongStoredCommandKind.ConcealedKong
                when tile is null && MeldType is null && Suit is null && tiles.Length == 4 =>
                new DeclareConcealedKongCommand(PlayerIndex, tiles),
            MahjongStoredCommandKind.AddedKong when tile is not null && HasNoOptions() =>
                new DeclareAddedKongCommand(PlayerIndex, tile.Value),
            MahjongStoredCommandKind.Win when tile is null && HasNoOptions() =>
                new DeclareMahjongWinCommand(PlayerIndex),
            MahjongStoredCommandKind.Pass when tile is null && HasNoOptions() =>
                new PassMahjongCommand(PlayerIndex),
            MahjongStoredCommandKind.ExchangeThree
                when tile is null && MeldType is null && Suit is null && tiles.Length == 3 =>
                new ExchangeThreeTilesCommand(PlayerIndex, tiles),
            MahjongStoredCommandKind.DeclareVoidSuit
                when tile is null && MeldType is null && Suit is not null && tiles.Length == 0 =>
                new DeclareVoidSuitCommand(PlayerIndex, Suit.Value),
            MahjongStoredCommandKind.Riichi when tile is not null && HasNoOptions() =>
                new DeclareRiichiCommand(PlayerIndex, tile.Value),
            MahjongStoredCommandKind.NineTerminalsDraw when tile is null && HasNoOptions() =>
                new DeclareNineTerminalsDrawCommand(PlayerIndex),
            _ => throw new InvalidDataException("麻将命令记录字段组合无效。"),
        };
    }

    private bool HasNoOptions()
    {
        return Tiles.Count == 0 && MeldType is null && Suit is null;
    }
}

public sealed class MahjongStoredTile
{
    [JsonPropertyName("kind")]
    public MahjongTileKind Kind { get; set; }

    [JsonPropertyName("copy_index")]
    public byte CopyIndex { get; set; }

    public static MahjongStoredTile FromTile(MahjongTile tile)
    {
        return new MahjongStoredTile { Kind = tile.Kind, CopyIndex = tile.CopyIndex };
    }

    public MahjongTile ToTile()
    {
        if (!Enum.IsDefined(Kind) || CopyIndex > 3)
        {
            throw new InvalidDataException("麻将命令记录包含无效牌张。");
        }

        return new MahjongTile(Kind, CopyIndex);
    }
}
