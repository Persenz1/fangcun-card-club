using Godot;

namespace FangcunCardClub.Game.Mahjong;

public partial class Mahjong3DLayer : Control
{
    private static readonly Vector2 LogicalSize = new(960f, 540f);

    private TextureRect _boardTexture = null!;
    private SubViewport _boardViewport = null!;

    public override void _Ready()
    {
        _boardViewport = GetNode<SubViewport>("BoardViewport");
        _boardTexture = GetNode<TextureRect>("BoardTexture");
        _boardTexture.Texture = _boardViewport.GetTexture();

        GetViewport().SizeChanged += UpdateRenderSize;
        UpdateRenderSize();
    }

    public override void _ExitTree()
    {
        GetViewport().SizeChanged -= UpdateRenderSize;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        var inputScale = new Vector2(_boardViewport.Size.X / Size.X, _boardViewport.Size.Y / Size.Y);
        InputEvent? forwardedEvent = @event switch
        {
            InputEventMouseButton mouseButton => ScaleMouseButton(mouseButton, inputScale),
            InputEventMouseMotion mouseMotion => ScaleMouseMotion(mouseMotion, inputScale),
            InputEventScreenTouch screenTouch => ScaleScreenTouch(screenTouch, inputScale),
            InputEventScreenDrag screenDrag => ScaleScreenDrag(screenDrag, inputScale),
            _ => null,
        };

        if (forwardedEvent is not null)
        {
            _boardViewport.PushInput(forwardedEvent, true);
        }
    }

    private void UpdateRenderSize()
    {
        var windowSize = DisplayServer.WindowGetSize();
        var scale = Mathf.Max(1f, Mathf.Min(windowSize.X / LogicalSize.X, windowSize.Y / LogicalSize.Y));
        _boardViewport.Size = new Vector2I(
            Mathf.RoundToInt(LogicalSize.X * scale),
            Mathf.RoundToInt(LogicalSize.Y * scale));
    }

    private static InputEventMouseButton ScaleMouseButton(InputEventMouseButton source, Vector2 scale)
    {
        var result = (InputEventMouseButton)source.Duplicate();
        result.Position *= scale;
        result.GlobalPosition = result.Position;
        return result;
    }

    private static InputEventMouseMotion ScaleMouseMotion(InputEventMouseMotion source, Vector2 scale)
    {
        var result = (InputEventMouseMotion)source.Duplicate();
        result.Position *= scale;
        result.GlobalPosition = result.Position;
        result.Relative *= scale;
        return result;
    }

    private static InputEventScreenTouch ScaleScreenTouch(InputEventScreenTouch source, Vector2 scale)
    {
        var result = (InputEventScreenTouch)source.Duplicate();
        result.Position *= scale;
        return result;
    }

    private static InputEventScreenDrag ScaleScreenDrag(InputEventScreenDrag source, Vector2 scale)
    {
        var result = (InputEventScreenDrag)source.Duplicate();
        result.Position *= scale;
        result.Relative *= scale;
        return result;
    }
}
