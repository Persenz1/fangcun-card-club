using Game.Mahjong.Hands;
using Game.Mahjong.Sichuan.Scoring;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;

namespace Game.Mahjong.Sichuan.Tests;

public sealed class SichuanMahjongScorerTests
{
    [Fact]
    public void Sequence_hand_without_patterns_is_zero_fan_pinghu()
    {
        var score = SichuanMahjongScorer.Evaluate(Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Dots1,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Characters7,
            MahjongTileKind.Characters8,
            MahjongTileKind.Characters9,
            MahjongTileKind.Dots5,
            MahjongTileKind.Dots5), []);

        Assert.Equal(0, score.Fan);
        Assert.Equal(10, score.Unit);
        Assert.Equal(["平胡"], score.Patterns);
    }

    [Fact]
    public void Dragon_seven_pairs_stacks_full_flush_and_roots()
    {
        var score = SichuanMahjongScorer.Evaluate(Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Characters6), []);

        Assert.Equal(6, score.Fan);
        Assert.Equal(640, score.Unit);
        Assert.Contains("龙七对", score.Patterns);
        Assert.Contains("清一色", score.Patterns);
        Assert.Contains("根×1", score.Patterns);
    }

    [Fact]
    public void Four_kong_golden_hook_keeps_fan_but_caps_payment_exponent()
    {
        var melds = new[]
        {
            OpenKong(MahjongTileKind.Characters1),
            OpenKong(MahjongTileKind.Characters2),
            OpenKong(MahjongTileKind.Characters3),
            OpenKong(MahjongTileKind.Characters4),
        };

        var score = SichuanMahjongScorer.Evaluate(
            Kinds(MahjongTileKind.Characters5, MahjongTileKind.Characters5),
            melds);

        Assert.Equal(9, score.Fan);
        Assert.Equal(2560, score.Unit);
        Assert.Contains("对对胡", score.Patterns);
        Assert.Contains("金钩钓", score.Patterns);
        Assert.Contains("根×4", score.Patterns);
    }

    [Fact]
    public void Self_draw_charges_only_other_active_players()
    {
        var kinds = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Dots1,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Bamboo1,
            MahjongTileKind.Bamboo2,
            MahjongTileKind.Bamboo3,
            MahjongTileKind.Dots5,
            MahjongTileKind.Dots5);

        var result = SichuanMahjongScorer.CalculateWin(
            kinds,
            [],
            MahjongSeat.West,
            null,
            [MahjongSeat.East, MahjongSeat.South, MahjongSeat.West]);

        Assert.Equal([-10, -10, 20, 0], result.ScoreChanges);
        Assert.Equal(0, result.ScoreChanges.Sum());
    }

    [Fact]
    public void Kong_payments_distinguish_discard_added_and_concealed_kongs()
    {
        var active = new[] { MahjongSeat.East, MahjongSeat.South, MahjongSeat.West };

        var open = SichuanMahjongScorer.CalculateKongPayment(
            MahjongMeldType.OpenKong,
            MahjongSeat.South,
            MahjongSeat.West,
            active);
        var added = SichuanMahjongScorer.CalculateKongPayment(
            MahjongMeldType.AddedKong,
            MahjongSeat.South,
            null,
            active);
        var concealed = SichuanMahjongScorer.CalculateKongPayment(
            MahjongMeldType.ConcealedKong,
            MahjongSeat.South,
            null,
            active);

        Assert.Equal([0, 20, -20, 0], open);
        Assert.Equal([-10, 20, -10, 0], added);
        Assert.Equal([-20, 40, -20, 0], concealed);
    }

    [Fact]
    public void Winning_shape_is_rejected_while_void_suit_remains()
    {
        var kinds = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Dots1,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Characters7,
            MahjongTileKind.Characters8,
            MahjongTileKind.Characters9,
            MahjongTileKind.Dots5,
            MahjongTileKind.Dots5);

        Assert.False(SichuanMahjongScorer.CanWin(kinds, 0, MahjongTileSuit.Characters));
        Assert.True(SichuanMahjongScorer.CanWin(kinds, 0, MahjongTileSuit.Bamboo));
    }

    [Fact]
    public void Exhaustive_draw_flower_pig_pays_each_remaining_non_flower_player()
    {
        var tenpai = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Characters7,
            MahjongTileKind.Characters8,
            MahjongTileKind.Characters9,
            MahjongTileKind.Dots1,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots5);
        var hands = new Dictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>>
        {
            [MahjongSeat.East] = tenpai,
            [MahjongSeat.South] = tenpai,
            [MahjongSeat.West] = tenpai,
        };
        var melds = EmptyMelds(hands.Keys);
        var voidSuits = hands.Keys.ToDictionary(
            seat => seat,
            seat => seat == MahjongSeat.East ? MahjongTileSuit.Characters : MahjongTileSuit.Bamboo);

        var result = SichuanMahjongScorer.CalculateExhaustive(
            hands,
            melds,
            voidSuits,
            hands.Keys);

        Assert.Equal([MahjongSeat.East], result.FlowerPigSeats);
        Assert.Equal([MahjongSeat.South, MahjongSeat.West], result.TenpaiSeats);
        Assert.Equal([-320, 160, 160, 0], result.ScoreChanges);
    }

    [Fact]
    public void Exhaustive_draw_noten_pays_each_tenpai_players_best_wait_value()
    {
        var pinghuTenpai = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Characters7,
            MahjongTileKind.Characters8,
            MahjongTileKind.Characters9,
            MahjongTileKind.Dots1,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots5);
        var dragonSevenPairsTenpai = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters3,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters6,
            MahjongTileKind.Characters6);
        var noten = Kinds(
            MahjongTileKind.Characters1,
            MahjongTileKind.Characters2,
            MahjongTileKind.Characters4,
            MahjongTileKind.Characters5,
            MahjongTileKind.Characters7,
            MahjongTileKind.Characters8,
            MahjongTileKind.Dots1,
            MahjongTileKind.Dots3,
            MahjongTileKind.Dots5,
            MahjongTileKind.Dots7,
            MahjongTileKind.Dots9,
            MahjongTileKind.Dots2,
            MahjongTileKind.Dots4);
        var hands = new Dictionary<MahjongSeat, IReadOnlyList<MahjongTileKind>>
        {
            [MahjongSeat.East] = pinghuTenpai,
            [MahjongSeat.South] = dragonSevenPairsTenpai,
            [MahjongSeat.West] = noten,
        };

        var result = SichuanMahjongScorer.CalculateExhaustive(
            hands,
            EmptyMelds(hands.Keys),
            hands.Keys.ToDictionary(seat => seat, _ => MahjongTileSuit.Bamboo),
            hands.Keys);

        Assert.Equal([MahjongSeat.East, MahjongSeat.South], result.TenpaiSeats);
        Assert.Equal([10, 640, -650, 0], result.ScoreChanges);
    }

    private static IReadOnlyList<MahjongTileKind> Kinds(params MahjongTileKind[] kinds)
    {
        return kinds;
    }

    private static IReadOnlyDictionary<MahjongSeat, IReadOnlyList<MahjongMeld>> EmptyMelds(
        IEnumerable<MahjongSeat> seats)
    {
        return seats.ToDictionary(seat => seat, _ => (IReadOnlyList<MahjongMeld>)[]);
    }

    private static MahjongMeld OpenKong(MahjongTileKind kind)
    {
        return new MahjongMeld(
            MahjongMeldType.OpenKong,
            Enumerable.Range(0, 4).Select(copy => new MahjongTile(kind, (byte)copy)),
            MahjongSeat.East);
    }
}
