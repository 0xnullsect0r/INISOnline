using Godot;

namespace INISOnline.App;

/// <summary>
/// Owns the single active full-screen <see cref="Screen"/> and cross-fades between them — the
/// app's navigation backbone. Screens call <c>Nav.Show(new SomeScreen())</c> to transition.
/// </summary>
public partial class ScreenManager : Control
{
    private Screen? _current;

    public override void _Ready() => SetAnchorsPreset(LayoutPreset.FullRect);

    public void Show(Screen next)
    {
        next.Nav = this;
        next.SetAnchorsPreset(LayoutPreset.FullRect);
        next.Modulate = new Color(1, 1, 1, 0);
        AddChild(next);

        var fadeIn = CreateTween();
        fadeIn.TweenProperty(next, "modulate:a", 1.0, 0.25);

        if (_current is { } old)
        {
            var fadeOut = CreateTween();
            fadeOut.TweenProperty(old, "modulate:a", 0.0, 0.2);
            fadeOut.TweenCallback(Callable.From(old.QueueFree));
        }
        _current = next;
    }

    /// <summary>Shows a transient toast message at the bottom of the screen.</summary>
    public void Toast(string message)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddChild(new Label { Text = message });
        panel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        panel.OffsetTop = -100;
        panel.OffsetBottom = -60;
        AddChild(panel);

        var tween = CreateTween();
        tween.TweenInterval(1.6);
        tween.TweenProperty(panel, "modulate:a", 0.0, 0.4);
        tween.TweenCallback(Callable.From(panel.QueueFree));
    }
}

/// <summary>Base class for full-screen screens; <see cref="Nav"/> is injected by the manager.</summary>
public abstract partial class Screen : Control
{
    public ScreenManager Nav { get; set; } = null!;
}
