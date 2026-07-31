using Godot;

namespace FangcunCardClub.Game.Mahjong;

public partial class MahjongBoard3D : Node3D
{
    private const float WallScale = 0.48f;
    private const float RiverScale = 0.58f;
    private const float PlayerHandScale = 0.84f;

    private static readonly (string Name, string Face, string Color)[] PlayerHand =
    [
        ("二万", "二\n万", "8e2f2b"),
        ("三万", "三\n万", "8e2f2b"),
        ("三万", "三\n万", "8e2f2b"),
        ("四万", "四\n万", "8e2f2b"),
        ("五筒", "五\n筒", "225f82"),
        ("六筒", "六\n筒", "225f82"),
        ("七筒", "七\n筒", "225f82"),
        ("八筒", "八\n筒", "225f82"),
        ("三条", "三\n条", "24724d"),
        ("四条", "四\n条", "24724d"),
        ("五条", "五\n条", "24724d"),
        ("东", "东", "20252a"),
        ("发", "发", "24724d"),
        ("中", "中", "a52f32"),
    ];

    private static readonly (string Name, string Face, string Color)[] RiverTiles =
    [
        ("一万", "一\n万", "8e2f2b"),
        ("九万", "九\n万", "8e2f2b"),
        ("三筒", "三\n筒", "225f82"),
        ("七筒", "七\n筒", "225f82"),
        ("二条", "二\n条", "24724d"),
        ("八条", "八\n条", "24724d"),
        ("东", "东", "20252a"),
        ("南", "南", "20252a"),
        ("西", "西", "20252a"),
        ("北", "北", "20252a"),
        ("白", "白", "34546b"),
        ("中", "中", "a52f32"),
    ];

    private readonly List<MahjongTile3D> _playerTiles = [];

    private BoxMesh _bodyMesh = null!;
    private QuadMesh _faceMesh = null!;
    private StandardMaterial3D _bodyMaterial = null!;
    private StandardMaterial3D _faceMaterial = null!;
    private StandardMaterial3D _backMaterial = null!;
    private Camera3D _camera = null!;
    private int _selectedTileIndex = -1;

    public event Action<int, string>? PlayerTileSelected;

    public override void _Ready()
    {
        CreateSharedResources();
        CreateLightingAndCamera();
        CreateFourWalls();
        CreateRivers();
        CreatePlayerHand();
    }

    public void SelectPlayerTile(int tileIndex)
    {
        if (tileIndex < 0 || tileIndex >= _playerTiles.Count)
        {
            return;
        }

        _selectedTileIndex = _selectedTileIndex == tileIndex ? -1 : tileIndex;
        for (var index = 0; index < _playerTiles.Count; index++)
        {
            _playerTiles[index].SetSelected(index == _selectedTileIndex);
        }

        var selected = _selectedTileIndex >= 0;
        PlayerTileSelected?.Invoke(
            _selectedTileIndex,
            selected ? PlayerHand[_selectedTileIndex].Name : string.Empty);
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
        _camera.LookAtFromPosition(new Vector3(0, 9.4f, 10.6f), new Vector3(0, 0, -0.55f), Vector3.Up);

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

    private void CreateFourWalls()
    {
        const int stacksPerWall = 15;
        float[] wallYaws = [0f, -90f, 180f, 90f];

        for (var side = 0; side < MahjongTableCalibration.WallLines.Length; side++)
        {
            var wall = MahjongTableCalibration.WallLines[side];
            for (var index = 0; index < stacksPerWall; index++)
            {
                var progress = index / (stacksPerWall - 1f);
                CreateWallStack(wall.Start.Lerp(wall.End, progress), wallYaws[side]);
            }
        }
    }

    private void CreateWallStack(Vector2 logicalPosition, float yaw)
    {
        var tablePosition = ProjectToTable(logicalPosition);
        var layerHeight = _bodyMesh.Size.Z * WallScale;
        for (var level = 0; level < 2; level++)
        {
            var tile = CreateTile(-1, "牌背", string.Empty, Colors.White, false, false, true);
            tile.Scale = Vector3.One * WallScale;
            var tilePosition = tablePosition + (Vector3.Up * (layerHeight * (level + 0.5f)));
            tile.SetRestPose(tilePosition, new Vector3(-90f, yaw, 0));
        }
    }

    private void CreateRivers()
    {
        for (var side = 0; side < 4; side++)
        {
            for (var index = 0; index < 3; index++)
            {
                var data = RiverTiles[(side * 3) + index];
                var tile = CreateTile(-1, data.Name, data.Face, new Color(data.Color), true, false, false);
                tile.Scale = Vector3.One * RiverScale;
                var river = MahjongTableCalibration.RiverLines[side];
                var progress = index / 2f;
                var position = ProjectToTable(river.Start.Lerp(river.End, progress));
                position += Vector3.Up * ((_bodyMesh.Size.Z * RiverScale * 0.5f) + 0.01f);
                var rotation = side switch
                {
                    0 => new Vector3(-90f, 180f, 0),
                    1 => new Vector3(-90f, -90f, 0),
                    2 => new Vector3(-90f, 0, 0),
                    _ => new Vector3(-90f, 90f, 0),
                };
                tile.SetRestPose(position, rotation);
            }
        }
    }

    private void CreatePlayerHand()
    {
        var hand = MahjongTableCalibration.PlayerHandLine;
        for (var index = 0; index < PlayerHand.Length; index++)
        {
            var data = PlayerHand[index];
            var tile = CreateTile(index, data.Name, data.Face, new Color(data.Color), true, true, false);
            tile.Scale = Vector3.One * PlayerHandScale;
            var progress = index / (PlayerHand.Length - 1f);
            var position = ProjectToTable(hand.Start.Lerp(hand.End, progress));
            tile.SetRestPose(position, _camera.RotationDegrees);
            tile.Pressed += OnPlayerTilePressed;
            _playerTiles.Add(tile);
        }
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
        AddChild(tile);
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

    private void OnPlayerTilePressed(int tileIndex, string displayName)
    {
        SelectPlayerTile(tileIndex);
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
