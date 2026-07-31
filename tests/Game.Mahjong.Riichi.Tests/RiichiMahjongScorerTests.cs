using Game.Mahjong.Hands;
using Game.Mahjong.Riichi.Scoring;
using Game.Mahjong.Riichi.State;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Riichi.Tests;

public sealed class RiichiMahjongScorerTests
{
    [Fact]
    public void Dora_indicators_wrap_each_suit_wind_and_dragon_cycle()
    {
        Assert.Equal(
            MahjongTileKind.Characters1,
            RiichiMahjongScorer.GetDoraKind(MahjongTileKind.Characters9));
        Assert.Equal(
            MahjongTileKind.East,
            RiichiMahjongScorer.GetDoraKind(MahjongTileKind.North));
        Assert.Equal(
            MahjongTileKind.White,
            RiichiMahjongScorer.GetDoraKind(MahjongTileKind.Red));
    }

    [Fact]
    public void Closed_riichi_pinfu_tsumo_is_twenty_fu_and_uses_split_payment()
    {
        var kinds = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots4,
            MahjongTileKind.Dots6,
            MahjongTileKind.Dots7,
            MahjongTileKind.Dots8,
            MahjongTileKind.Bamboo5,
            MahjongTileKind.Bamboo5);
        var context = new RiichiWinContext(
            MahjongSeat.South,
            MahjongSeat.East,
            RiichiRoundWind.East,
            MahjongTileKind.Dots8,
            selfDraw: true,
            isRiichi: true,
            riichiSticksAwarded: 1);

        var result = RiichiMahjongScorer.CalculateWin(kinds, [], context, null);

        Assert.Equal(3, result.HandScore.Han);
        Assert.Equal(20, result.HandScore.Fu);
        Assert.Contains("立直", result.HandScore.Yaku);
        Assert.Contains("门前清自摸和", result.HandScore.Yaku);
        Assert.Contains("平和", result.HandScore.Yaku);
        Assert.Equal([-1300, 3700, -700, -700], result.ScoreChanges);
    }

    [Fact]
    public void Seven_pairs_is_fixed_twenty_five_fu()
    {
        var kinds = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots4,
            MahjongTileKind.Dots4,
            MahjongTileKind.Bamboo5,
            MahjongTileKind.Bamboo5,
            MahjongTileKind.Bamboo6,
            MahjongTileKind.Bamboo6,
            MahjongTileKind.East,
            MahjongTileKind.East);
        var context = RonContext(MahjongTileKind.East);

        var score = RiichiMahjongScorer.Evaluate(kinds, [], context);

        Assert.Equal(2, score.Han);
        Assert.Equal(25, score.Fu);
        Assert.Equal(400, score.BasicPoints);
        Assert.Contains("七对子", score.Yaku);
    }

    [Fact]
    public void Dora_cannot_turn_an_open_no_yaku_hand_into_a_win()
    {
        var openChow = new MahjongMeld(
            MahjongMeldType.Chow,
            Tiles(
                MahjongTileKind.Characters1,
                MahjongTileKind.Characters2,
                MahjongTileKind.Characters3),
            MahjongSeat.North);
        var kinds = Kinds(
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots4,
            MahjongTileKind.Bamboo4,
            MahjongTileKind.Bamboo5,
            MahjongTileKind.Bamboo6,
            MahjongTileKind.Characters6,
            MahjongTileKind.Characters7,
            MahjongTileKind.Characters8,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters5);
        var context = new RiichiWinContext(
            MahjongSeat.South,
            MahjongSeat.East,
            RiichiRoundWind.East,
            MahjongTileKind.Characters8,
            selfDraw: false,
            doraIndicators: [MahjongTileKind.Characters4]);

        var canWin = RiichiMahjongScorer.TryEvaluate(kinds, [openChow], context, out var score);

        Assert.False(canWin);
        Assert.Null(score);
    }

    [Fact]
    public void Multiple_yakuman_stack_and_ignore_ordinary_han()
    {
        var melds = new[]
        {
            OpenPong(MahjongTileKind.White),
            OpenPong(MahjongTileKind.Green),
            OpenPong(MahjongTileKind.Red),
        };
        var kinds = Kinds(
            MahjongTileKind.East,
            MahjongTileKind.East,
            MahjongTileKind.East,
            MahjongTileKind.South,
            MahjongTileKind.South);
        var context = RonContext(MahjongTileKind.South);

        var score = RiichiMahjongScorer.Evaluate(kinds, melds, context);

        Assert.Equal(2, score.YakumanCount);
        Assert.Equal(16000, score.BasicPoints);
        Assert.Contains("大三元", score.Yaku);
        Assert.Contains("字一色", score.Yaku);
    }

    [Fact]
    public void Visible_and_ura_dora_are_counted_only_after_a_real_yaku()
    {
        var kinds = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots4,
            MahjongTileKind.Dots6,
            MahjongTileKind.Dots7,
            MahjongTileKind.Dots8,
            MahjongTileKind.Bamboo5,
            MahjongTileKind.Bamboo5);
        var context = new RiichiWinContext(
            MahjongSeat.South,
            MahjongSeat.East,
            RiichiRoundWind.East,
            MahjongTileKind.Dots8,
            selfDraw: false,
            isRiichi: true,
            doraIndicators: [MahjongTileKind.Bamboo4],
            uraDoraIndicators: [MahjongTileKind.Characters3]);

        var score = RiichiMahjongScorer.Evaluate(kinds, [], context);

        Assert.Equal(3, score.DoraCount);
        Assert.Equal(5, score.Han);
        Assert.Equal("满贯", score.LimitName);
    }

    [Fact]
    public void Closed_ron_fu_counts_terminal_triplet_value_pair_and_closed_wait()
    {
        var kinds = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Dots4,
            MahjongTileKind.Dots5,
            MahjongTileKind.Dots6,
            MahjongTileKind.Bamboo7,
            MahjongTileKind.Bamboo8,
            MahjongTileKind.Bamboo9,
            MahjongTileKind.White,
            MahjongTileKind.White);
        var context = new RiichiWinContext(
            MahjongSeat.South,
            MahjongSeat.East,
            RiichiRoundWind.East,
            MahjongTileKind.Characters3,
            selfDraw: false,
            isRiichi: true);

        var score = RiichiMahjongScorer.Evaluate(kinds, [], context);

        Assert.Equal(1, score.Han);
        Assert.Equal(50, score.Fu);
    }

    [Fact]
    public void Noten_pool_is_split_for_one_two_and_three_tenpai_players()
    {
        Assert.Equal(
            [3000, -1000, -1000, -1000],
            RiichiMahjongScorer.CalculateNotenPayments([MahjongSeat.East]));
        Assert.Equal(
            [1500, 1500, -1500, -1500],
            RiichiMahjongScorer.CalculateNotenPayments([MahjongSeat.East, MahjongSeat.South]));
        Assert.Equal(
            [1000, 1000, 1000, -3000],
            RiichiMahjongScorer.CalculateNotenPayments(
                [MahjongSeat.East, MahjongSeat.South, MahjongSeat.West]));
    }

    private static RiichiWinContext RonContext(MahjongTileKind winningKind)
    {
        return new RiichiWinContext(
            MahjongSeat.South,
            MahjongSeat.East,
            RiichiRoundWind.East,
            winningKind,
            selfDraw: false);
    }

    private static IReadOnlyList<MahjongTileKind> Kinds(params MahjongTileKind[] kinds)
    {
        return kinds;
    }

    private static IReadOnlyList<MahjongTile> Tiles(params MahjongTileKind[] kinds)
    {
        return kinds.Select((kind, index) => new MahjongTile(kind, (byte)(index % 4))).ToArray();
    }

    private static MahjongMeld OpenPong(MahjongTileKind kind)
    {
        return new MahjongMeld(
            MahjongMeldType.Pong,
            Enumerable.Range(0, 3).Select(copy => new MahjongTile(kind, (byte)copy)),
            MahjongSeat.East);
    }
}
