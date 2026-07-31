using Game.Mahjong.Tiles;

namespace Game.Mahjong.Hands;

public sealed class MahjongHand
{
    private readonly List<MahjongTile> _concealedTiles = [];
    private readonly List<MahjongMeld> _melds = [];

    public IReadOnlyList<MahjongTile> ConcealedTiles => Array.AsReadOnly(_concealedTiles.ToArray());

    public IReadOnlyList<MahjongMeld> Melds => Array.AsReadOnly(_melds.ToArray());

    public void AddTile(MahjongTile tile)
    {
        _concealedTiles.Add(tile);
        SortTiles();
    }

    public void AddTiles(IEnumerable<MahjongTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        _concealedTiles.AddRange(tiles);
        SortTiles();
    }

    public void RemoveTiles(IEnumerable<MahjongTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        var requested = tiles.ToArray();
        if (requested.Distinct().Count() != requested.Length
            || requested.Any(tile => !_concealedTiles.Contains(tile)))
        {
            throw new InvalidOperationException("The hand does not contain the requested physical tiles.");
        }

        foreach (var tile in requested)
        {
            _concealedTiles.Remove(tile);
        }
    }

    public void AddMeld(MahjongMeld meld, IEnumerable<MahjongTile> concealedTilesUsed)
    {
        ArgumentNullException.ThrowIfNull(meld);
        RemoveTiles(concealedTilesUsed);
        _melds.Add(meld);
    }

    public MahjongMeld UpgradePong(MahjongTile fourthTile)
    {
        if (!_concealedTiles.Contains(fourthTile))
        {
            throw new InvalidOperationException("The fourth tile is not in the concealed hand.");
        }

        var meldIndex = _melds.FindIndex(meld =>
            meld.Type == MahjongMeldType.Pong && meld.Tiles[0].Kind == fourthTile.Kind);
        if (meldIndex < 0)
        {
            throw new InvalidOperationException("No matching open pong can be upgraded.");
        }

        var pong = _melds[meldIndex];
        var kong = new MahjongMeld(
            MahjongMeldType.AddedKong,
            pong.Tiles.Append(fourthTile),
            pong.SourceSeat);
        _concealedTiles.Remove(fourthTile);
        _melds[meldIndex] = kong;
        return kong;
    }

    private void SortTiles()
    {
        _concealedTiles.Sort(static (left, right) =>
        {
            var kindComparison = left.Kind.CompareTo(right.Kind);
            return kindComparison != 0 ? kindComparison : left.CopyIndex.CompareTo(right.CopyIndex);
        });
    }
}
