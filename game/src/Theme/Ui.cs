using Godot;

namespace INISOnline.Theme;

/// <summary>Factory helpers for the reusable UI building blocks (headings, buttons, panels).</summary>
public static class Ui
{
    public static Label Title(string text)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 64);
        l.AddThemeColorOverride("font_color", Palette.GoldBright);
        return l;
    }

    public static Label Heading(string text)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 32);
        l.AddThemeColorOverride("font_color", Palette.Cream);
        return l;
    }

    public static Label Body(string text, Color? color = null)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeColorOverride("font_color", color ?? Palette.Muted);
        return l;
    }

    public static Button MenuButton(string text, float minWidth = 320f)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(minWidth, 56) };
        b.Pressed += () => INISOnline.Audio.AudioManager.Instance?.PlayUi();
        return b;
    }

    public static PanelContainer Card()
    {
        var p = new PanelContainer();
        return p;
    }

    public static LineEdit Field(string placeholder, bool secret = false, string text = "")
    {
        var edit = new LineEdit
        {
            PlaceholderText = placeholder,
            Secret = secret,
            Text = text,
            CustomMinimumSize = new Vector2(360, 0),
        };
        return edit;
    }

    /// <summary>A full-rect background: deep slate with a soft vignette toward the edges.</summary>
    public static Control Background()
    {
        var root = new Control();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        var fill = new ColorRect { Color = Palette.Slate };
        fill.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(fill);

        var glow = new ColorRect { Color = Palette.SlateLight, MouseFilter = Control.MouseFilterEnum.Ignore };
        glow.SetAnchorsPreset(Control.LayoutPreset.Center);
        glow.CustomMinimumSize = new Vector2(900, 600);
        glow.PivotOffset = new Vector2(450, 300);
        root.AddChild(glow);
        return root;
    }
}
