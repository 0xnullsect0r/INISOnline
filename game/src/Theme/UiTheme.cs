using Godot;

namespace INISOnline.Theme;

/// <summary>
/// Builds the single Godot <see cref="Godot.Theme"/> that styles every control: rounded slate
/// panels, gold-accented tactile buttons, parchment headings. Built in code (rather than a
/// .tres) so the design system is reviewable and edited like any other source. Applied once at
/// the root so all descendants inherit it.
/// </summary>
public static class UiTheme
{
    public const int CornerRadius = 10;

    public static Godot.Theme Build()
    {
        var theme = new Godot.Theme();
        StyleButtons(theme);
        StylePanels(theme);
        StyleLabels(theme);
        StyleInputs(theme);
        return theme;
    }

    private static StyleBoxFlat Filled(Color bg, int radius = CornerRadius, int border = 0, Color? borderColor = null)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            ContentMarginLeft = 18, ContentMarginRight = 18,
            ContentMarginTop = 12, ContentMarginBottom = 12,
        };
        box.SetCornerRadiusAll(radius);
        if (border > 0)
        {
            box.SetBorderWidthAll(border);
            box.BorderColor = borderColor ?? Palette.Bronze;
        }
        return box;
    }

    private static void StyleButtons(Godot.Theme theme)
    {
        var normal = Filled(Palette.Bronze, border: 2, borderColor: Palette.Gold);
        var hover = Filled(Palette.Gold, border: 2, borderColor: Palette.GoldBright);
        var pressed = Filled(Palette.Bronze.Darkened(0.15f), border: 2, borderColor: Palette.Gold);
        var disabled = Filled(Palette.SlateLight, border: 2, borderColor: Palette.Muted);

        theme.SetStylebox("normal", "Button", normal);
        theme.SetStylebox("hover", "Button", hover);
        theme.SetStylebox("pressed", "Button", pressed);
        theme.SetStylebox("disabled", "Button", disabled);
        theme.SetStylebox("focus", "Button", Filled(new Color(0, 0, 0, 0), border: 2, borderColor: Palette.GoldBright));

        theme.SetColor("font_color", "Button", Palette.Cream);
        theme.SetColor("font_hover_color", "Button", Palette.Ink);
        theme.SetColor("font_pressed_color", "Button", Palette.Cream);
        theme.SetColor("font_disabled_color", "Button", Palette.Muted);
        theme.SetFontSize("font_size", "Button", 20);
    }

    private static void StylePanels(Godot.Theme theme)
    {
        var panel = Filled(Palette.SlatePanel, radius: 14, border: 2, borderColor: Palette.Bronze);
        panel.ShadowColor = new Color(0, 0, 0, 0.35f);
        panel.ShadowSize = 8;
        theme.SetStylebox("panel", "PanelContainer", panel);
        theme.SetStylebox("panel", "Panel", panel);
    }

    private static void StyleLabels(Godot.Theme theme)
    {
        theme.SetColor("font_color", "Label", Palette.Cream);
        theme.SetFontSize("font_size", "Label", 18);
    }

    private static void StyleInputs(Godot.Theme theme)
    {
        var box = Filled(Palette.Slate, radius: 8, border: 2, borderColor: Palette.Bronze);
        theme.SetStylebox("normal", "LineEdit", box);
        theme.SetStylebox("focus", "LineEdit", Filled(Palette.Slate, radius: 8, border: 2, borderColor: Palette.GoldBright));
        theme.SetColor("font_color", "LineEdit", Palette.Cream);
        theme.SetColor("font_placeholder_color", "LineEdit", Palette.Muted);
        theme.SetFontSize("font_size", "LineEdit", 18);
    }
}
