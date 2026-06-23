using Godot;
using INISOnline.Theme;

namespace INISOnline.App;

/// <summary>
/// Base for modal overlays (settings, debug, in-game menu): a full-rect dimmer that swallows input
/// with a centered panel on top. Subclasses fill <see cref="Body"/>; <see cref="Close"/> frees it.
/// </summary>
public abstract partial class Overlay : Control
{
    protected VBoxContainer Body = null!;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.MouseFilter = MouseFilterEnum.Stop;
        AddChild(dim);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(LayoutPreset.Center);
        AddChild(panel);

        Body = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        Body.AddThemeConstantOverride("separation", 12);
        panel.AddChild(Body);

        Build();
    }

    protected abstract void Build();

    protected void AddCloseButton(string text = "Close")
    {
        var close = Ui.MenuButton(text);
        close.Pressed += Close;
        Body.AddChild(close);
    }

    public void Close() => QueueFree();
}
