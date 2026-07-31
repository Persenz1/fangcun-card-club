using Godot;

namespace FangcunCardClub.Game.Mahjong;

public partial class MahjongTile3D : StaticBody3D
{
    [Signal]
    public delegate void PressedEventHandler(int tileIndex, string displayName);

    private Vector3 _restPosition;

    public int TileIndex { get; private set; } = -1;

    public string DisplayName { get; private set; } = string.Empty;

    public void Configure(
        int tileIndex,
        string displayName,
        string faceText,
        Color textColor,
        bool faceUp,
        bool selectable,
        Mesh bodyMesh,
        Material bodyMaterial,
        Mesh faceMesh,
        Material faceMaterial,
        Vector3 collisionSize)
    {
        TileIndex = tileIndex;
        DisplayName = displayName;
        InputRayPickable = selectable;
        CollisionLayer = selectable ? 1u : 0u;
        CollisionMask = 0;

        AddChild(new MeshInstance3D
        {
            Mesh = bodyMesh,
            MaterialOverride = bodyMaterial,
        });

        var face = new MeshInstance3D
        {
            Mesh = faceMesh,
            MaterialOverride = faceMaterial,
            Position = new Vector3(0, 0, (collisionSize.Z / 2f) + 0.003f),
        };
        AddChild(face);

        if (faceUp)
        {
            face.AddChild(new Label3D
            {
                Text = faceText,
                FontSize = 48,
                OutlineSize = 5,
                PixelSize = 0.0052f,
                Modulate = textColor,
                Position = new Vector3(0, 0, 0.004f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                DoubleSided = false,
            });
        }

        if (selectable)
        {
            AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = collisionSize },
            });
        }
    }

    public void SetRestPose(Vector3 position, Vector3 rotationDegrees)
    {
        _restPosition = position;
        Position = position;
        RotationDegrees = rotationDegrees;
    }

    public void SetSelected(bool selected)
    {
        Position = _restPosition + (selected ? Vector3.Up * 0.24f : Vector3.Zero);
    }

    public override void _InputEvent(Camera3D camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, int shapeIdx)
    {
        var pressed = @event is InputEventMouseButton
        {
            ButtonIndex: MouseButton.Left,
            Pressed: true,
        } || @event is InputEventScreenTouch { Pressed: true };

        if (!pressed)
        {
            return;
        }

        EmitSignal(SignalName.Pressed, TileIndex, DisplayName);
        GetViewport().SetInputAsHandled();
    }
}
