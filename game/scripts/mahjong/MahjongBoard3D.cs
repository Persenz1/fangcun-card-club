using Game.Application.Mahjong;
using Game.Mahjong.Hands;
using Game.Mahjong.Table;
using Game.Mahjong.Tiles;
using Godot;

namespace FangcunCardClub.Game.Mahjong;

public partial class MahjongBoard3D : Node3D
{
    private const float WallScale = 0.48f;
    private const float RiverScale = 0.58f;
    private const float OpponentHandScale = 0.52f;
    private const float PlayerHandScale = 0.84f;
    private const float MeldScale = 0.54f;

    private readonly Dictionary<MahjongSeat, List<MahjongTile3D>> _handNodes = [];
    private readonly Dictionary<MahjongSeat, List<MahjongTile3D>> _meldNodes = [];
    private readonly List<MahjongTile3D> _playerTiles = [];
    private readonly List<MahjongTile> _playerTileValues = [];
    private readonly HashSet<MahjongTile> _selectedTiles = [];
    private readonly Dictionary<MahjongTile, MahjongTile3D> _visibleTileNodes = [];

    private StandardMaterial3D _backMaterial = null!;
    private BoxMesh _bodyMesh = null!;
    private StandardMaterial3D _bodyMaterial = null!;
    private Camera3D _camera = null!;
    private StandardMaterial3D _faceMaterial = null!;
    private QuadMesh _faceMesh = null!;
    private Node3D? _piecesRoot;
    private MahjongSessionView? _view;

    public event Action<IReadOnlyList<MahjongTile>>? PlayerSelectionChanged;

    public override void _Ready()
    {
        CreateSharedResources();
        CreateLightingAndCamera();
    }

    public void Render(MahjongSessionView view, MahjongAnimationEvent? cue = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
        var humanTiles = view.Table.Seats[(int)view.HumanSeat].Hand
            .Where(tile => tile.Tile is not null)
            .Select(tile => tile.Tile!.Value)
            .ToHashSet();
        _selectedTiles.IntersectWith(humanTiles);

        _piecesRoot?.QueueFree();
        _piecesRoot = new Node3D { Name = "RenderedPieces" };
        AddChild(_piecesRoot);
        _handNodes.Clear();
        _meldNodes.Clear();
        _playerTiles.Clear();
        _playerTileValues.Clear();
        _visibleTileNodes.Clear();

        CreateWall(view);
        CreateHands(view);
        CreateMelds(view);
        CreateRivers(view);
        CreateDoraIndicators(view);
        ApplySelection();
        ApplyCue(cue);
    }

    public void SelectTiles(IEnumerable<MahjongTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);
        _selectedTiles.Clear();
        _selectedTiles.UnionWith(tiles.Where(_playerTileValues.Contains));
        ApplySelection();
        PublishSelection();
    }

    public void ClearSelection()
    {
        if (_selectedTiles.Count == 0)
        {
            return;
        }

        _selectedTiles.Clear();
        ApplySelection();
        PublishSelection();
    }

    private void CreateSharedResources()
    {
        _bodyMesh = new BoxMesh
        {
            Size = new Vector3(0.56f, 0.8f, 0.32f),
        };
        _faceMesh = new QuadMesh
        {
            Size = new Vector2(0.48f, 0.7f),
        };

        _bodyMaterial = CreateMaterial(new Color("e8dec5"), 0.82f);
        _faceMaterial = CreateMaterial(new Color("f7efd9"), 0.88f);
        _backMaterial = CreateMaterial(new Color("246858"), 0.75f);
    }

    private void CreateLightingAndCamera()
    {
        _camera = new Camera3D
        {
            Current = true,
            Fov = 36f,
            Near = 0.1f,
            Far = 50f,
        };
        AddChild(_camera);
        _camera.LookAtFromPosition(
            new Vector3(0, 9.4f, 10.6f),
            new Vector3(0, 0, -0.55f),
            Vector3.Up);

        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-58f, -28f, 0),
            LightColor = new Color("ffd7a0"),
            LightEnergy = 1.15f,
            ShadowEnabled = false,
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-38f, 146f, 0),
            LightColor = new Color("8fcbd0"),
            LightEnergy = 0.55f,
            ShadowEnabled = false,
        });
    }

    private void CreateWall(MahjongSessionView view)
    {
        var deadWallCount = view.Mode == MahjongMode.Riichi ? 14 : 0;
        var tileCount = Math.Min(136, view.Table.LiveTilesRemaining + deadWallCount);
        for (var ordinal = 0; ordinal < tileCount; ordinal++)
        {
            var stack = ordinal / 2;
            var side = stack / MahjongTableCalibration.WallStacksPerSide;
            if (side >= 4)
            {
                break;
            }

            var stackIndex = stack % MahjongTableCalibration.WallStacksPerSide;
            var logicalPosition = MahjongTableCalibration.PointOnLine(
                MahjongTableCalibration.WallLines[side],
                stackIndex,
                MahjongTableCalibration.WallStacksPerSide);
            var tile = CreateTile(-1, "牌背", string.Empty, Colors.White, false, false, true);
            tile.Scale = Vector3.One * WallScale;
            var position = ProjectToTable(logicalPosition);
            var level = ordinal % 2;
            position += Vector3.Up * (_bodyMesh.Size.Z * WallScale * (level + 0.5f));
            tile.SetRestPose(position, TableRotation(side));
        }
    }

    private void CreateHands(MahjongSessionView view)
    {
        var selectableTiles = view.LegalActions
            .Where(action => action.Kind is
                MahjongActionViewKind.Discard or
                MahjongActionViewKind.ExchangeThree or
                MahjongActionViewKind.RiichiDiscard)
            .SelectMany(action => action.Tiles)
            .ToHashSet();
        foreach (var seatView in view.Table.Seats)
        {
            var screenSide = ScreenSide(seatView.Seat, view.HumanSeat);
            var nodes = new List<MahjongTile3D>();
            _handNodes[seatView.Seat] = nodes;
            for (var index = 0; index < seatView.Hand.Count; index++)
            {
                var presented = seatView.Hand[index];
                var physicalTile = presented.Tile;
                var isHuman = seatView.Seat == view.HumanSeat;
                var selectable = isHuman
                    && physicalTile is { } candidate
                    && selectableTiles.Contains(candidate);
                var tile = CreateTile(
                    isHuman ? index : -1,
                    physicalTile is { } value ? MahjongText.Tile(value) : "手牌",
                    physicalTile is { } face ? FaceText(face.Kind) : string.Empty,
                    physicalTile is { } colored ? FaceColor(colored.Kind) : Colors.White,
                    presented.FaceUp,
                    selectable,
                    !presented.FaceUp);
                var scale = isHuman ? PlayerHandScale : OpponentHandScale;
                tile.Scale = Vector3.One * scale;
                var logicalPosition = MahjongTableCalibration.PointOnLine(
                    MahjongTableCalibration.HandLines[screenSide],
                    index,
                    seatView.Hand.Count);
                var position = ProjectToTable(logicalPosition);
                position += Vector3.Up * (_bodyMesh.Size.Z * scale * 0.5f);
                var rotation = isHuman ? _camera.RotationDegrees : TableRotation(screenSide);
                tile.SetRestPose(position, rotation);
                nodes.Add(tile);
                if (physicalTile is { } knownTile)
                {
                    _visibleTileNodes[knownTile] = tile;
                    if (isHuman)
                    {
                        tile.Pressed += OnPlayerTilePressed;
                        _playerTiles.Add(tile);
                        _playerTileValues.Add(knownTile);
                    }
                }
            }
        }
    }

    private void CreateMelds(MahjongSessionView view)
    {
        foreach (var seatView in view.Table.Seats)
        {
            var screenSide = ScreenSide(seatView.Seat, view.HumanSeat);
            var flattened = seatView.Melds
                .SelectMany(meld => meld.Tiles.Select(tile => (Meld: meld, Tile: tile)))
                .ToArray();
            var nodes = new List<MahjongTile3D>();
            _meldNodes[seatView.Seat] = nodes;
            for (var index = 0; index < flattened.Length; index++)
            {
                var item = flattened[index];
                var tile = CreateTile(
                    -1,
                    MahjongText.Tile(item.Tile),
                    FaceText(item.Tile.Kind),
                    FaceColor(item.Tile.Kind),
                    true,
                    false,
                    false);
                tile.Scale = Vector3.One * MeldScale;
                var logicalPosition = MahjongTableCalibration.PointOnLine(
                    MahjongTableCalibration.MeldLines[screenSide],
                    index,
                    flattened.Length);
                var position = ProjectToTable(logicalPosition);
                position += Vector3.Up * ((_bodyMesh.Size.Z * MeldScale * 0.5f) + 0.01f);
                tile.SetRestPose(position, TableRotation(screenSide));
                nodes.Add(tile);
                _visibleTileNodes[item.Tile] = tile;
            }
        }
    }

    private void CreateRivers(MahjongSessionView view)
    {
        foreach (var seatView in view.Table.Seats)
        {
            var screenSide = ScreenSide(seatView.Seat, view.HumanSeat);
            var river = seatView.River.Where(tile => !tile.IsClaimed).ToArray();
            for (var index = 0; index < river.Length; index++)
            {
                var riverTile = river[index];
                var tile = CreateTile(
                    -1,
                    MahjongText.Tile(riverTile.Tile),
                    FaceText(riverTile.Tile.Kind),
                    FaceColor(riverTile.Tile.Kind),
                    true,
                    false,
                    false);
                tile.Scale = Vector3.One * RiverScale;
                var position = ProjectToTable(
                    MahjongTableCalibration.RiverTilePoint(screenSide, index));
                position += Vector3.Up * ((_bodyMesh.Size.Z * RiverScale * 0.5f) + 0.01f);
                tile.SetRestPose(position, TableRotation(screenSide));
                _visibleTileNodes[riverTile.Tile] = tile;
            }
        }
    }

    private void CreateDoraIndicators(MahjongSessionView view)
    {
        for (var index = 0; index < view.DoraIndicators.Count; index++)
        {
            var kind = view.DoraIndicators[index];
            var tile = CreateTile(
                -1,
                MahjongText.Tile(kind),
                FaceText(kind),
                FaceColor(kind),
                true,
                false,
                false);
            tile.Scale = Vector3.One * WallScale;
            var logicalPosition = MahjongTableCalibration.PointOnLine(
                MahjongTableCalibration.DoraIndicatorLine,
                index,
                view.DoraIndicators.Count);
            var position = ProjectToTable(logicalPosition);
            position += Vector3.Up * ((_bodyMesh.Size.Z * WallScale * 0.5f) + 0.02f);
            tile.SetRestPose(position, TableRotation(0));
        }
    }

    private void ApplyCue(MahjongAnimationEvent? cue)
    {
        if (cue is null)
        {
            return;
        }

        if (_view?.Mode == MahjongMode.Riichi
            && !_view.IsFinished
            && cue.Kind is MahjongAnimationEventKind.Win or MahjongAnimationEventKind.HandFinished)
        {
            return;
        }

        if (cue.Tile is { } physicalTile && _visibleTileNodes.TryGetValue(physicalTile, out var tileNode))
        {
            tileNode.SetSelected(true);
            return;
        }

        if (cue.Seat is not { } seat)
        {
            return;
        }

        var nodes = cue.Kind switch
        {
            MahjongAnimationEventKind.Meld when _meldNodes.TryGetValue(seat, out var melds) => melds,
            MahjongAnimationEventKind.Win when _handNodes.TryGetValue(seat, out var hand) => hand,
            MahjongAnimationEventKind.Draw when _handNodes.TryGetValue(seat, out var drawnHand) =>
                drawnHand.Count == 0 ? [] : [drawnHand[^1]],
            _ => [],
        };
        foreach (var node in nodes)
        {
            node.SetSelected(true);
        }
    }

    private void OnPlayerTilePressed(int tileIndex, string _)
    {
        if (tileIndex < 0 || tileIndex >= _playerTileValues.Count)
        {
            return;
        }

        var tile = _playerTileValues[tileIndex];
        if (!_selectedTiles.Add(tile))
        {
            _selectedTiles.Remove(tile);
        }

        ApplySelection();
        PublishSelection();
    }

    private void ApplySelection()
    {
        for (var index = 0; index < _playerTiles.Count; index++)
        {
            _playerTiles[index].SetSelected(_selectedTiles.Contains(_playerTileValues[index]));
        }
    }

    private void PublishSelection()
    {
        PlayerSelectionChanged?.Invoke(Array.AsReadOnly(_selectedTiles
            .OrderBy(tile => tile.Kind)
            .ThenBy(tile => tile.CopyIndex)
            .ToArray()));
    }

    private Vector3 ProjectToTable(Vector2 logicalPosition)
    {
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var viewportPosition = new Vector2(
            logicalPosition.X * viewportSize.X / MahjongTableCalibration.LogicalSize.X,
            logicalPosition.Y * viewportSize.Y / MahjongTableCalibration.LogicalSize.Y);
        var rayOrigin = _camera.ProjectRayOrigin(viewportPosition);
        var rayDirection = _camera.ProjectRayNormal(viewportPosition);
        return rayOrigin + (rayDirection * (-rayOrigin.Y / rayDirection.Y));
    }

    private MahjongTile3D CreateTile(
        int tileIndex,
        string displayName,
        string faceText,
        Color textColor,
        bool faceUp,
        bool selectable,
        bool showBack)
    {
        var tile = new MahjongTile3D();
        _piecesRoot!.AddChild(tile);
        tile.Configure(
            tileIndex,
            displayName,
            faceText,
            textColor,
            faceUp,
            selectable,
            _bodyMesh,
            _bodyMaterial,
            _faceMesh,
            showBack ? _backMaterial : _faceMaterial,
            _bodyMesh.Size);
        return tile;
    }

    private static int ScreenSide(MahjongSeat seat, MahjongSeat humanSeat)
    {
        return seat.DistanceFrom(humanSeat) switch
        {
            0 => 2,
            1 => 1,
            2 => 0,
            _ => 3,
        };
    }

    private static Vector3 TableRotation(int side)
    {
        return side switch
        {
            0 => new Vector3(-90f, 180f, 0),
            1 => new Vector3(-90f, -90f, 0),
            2 => new Vector3(-90f, 0, 0),
            _ => new Vector3(-90f, 90f, 0),
        };
    }

    private static string FaceText(MahjongTileKind kind)
    {
        var text = MahjongText.Tile(kind);
        return kind.IsSuited() ? text.Insert(1, "\n") : text;
    }

    private static Color FaceColor(MahjongTileKind kind)
    {
        return kind switch
        {
            >= MahjongTileKind.Characters1 and <= MahjongTileKind.Characters9 => new Color("8e2f2b"),
            >= MahjongTileKind.Dots1 and <= MahjongTileKind.Dots9 => new Color("225f82"),
            >= MahjongTileKind.Bamboo1 and <= MahjongTileKind.Bamboo9 => new Color("24724d"),
            MahjongTileKind.Red => new Color("a52f32"),
            MahjongTileKind.Green => new Color("24724d"),
            MahjongTileKind.White => new Color("34546b"),
            _ => new Color("20252a"),
        };
    }

    private static StandardMaterial3D CreateMaterial(Color color, float roughness)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = 0f,
        };
    }
}
