using Godot;
using INISOnline.Theme;
using Inis.Core.Data;

namespace INISOnline.Screens;

/// <summary>
/// A clickable card: the generated SVG art (see <c>tools/gen-art.mjs</c>) with a hover lift and
/// an optional verb caption ("Play", "Draft", "React"). Falls back to a themed button showing the
/// card name when art is missing. Used by the HUD's hand dock for every card-shaped legal move.
/// </summary>
public partial class CardWidget : Button
{
    // Art is authored at 300×420; the dock renders at ~0.4 scale.
    public const float CardWidth = 120f;
    public const float CardHeight = 168f;

    private readonly CardDefinition _def;
    private readonly string? _caption;

    public CardWidget(CardDefinition def, string? caption = null)
    {
        _def = def;
        _caption = caption;
        CustomMinimumSize = new Vector2(CardWidth, CardHeight);
        TooltipText = $"{def.Name}\n{def.Text}";
        // The art replaces the themed chrome entirely.
        Flat = true;
        PivotOffset = new Vector2(CardWidth / 2f, CardHeight); // lift from the bottom edge
    }

    public override void _Ready()
    {
        var texture = _def.Art is not null && ResourceLoader.Exists($"res://art/{_def.Art}")
            ? ResourceLoader.Load<Texture2D>($"res://art/{_def.Art}")
            : null;

        if (texture is not null)
        {
            var rect = new TextureRect
            {
                Texture = texture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(rect);
        }
        else
        {
            Text = _def.Name;
            AutowrapMode = TextServer.AutowrapMode.WordSmart;
            Flat = false;
        }

        if (_caption is not null)
        {
            var caption = new Label
            {
                Text = _caption,
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            caption.AddThemeFontSizeOverride("font_size", 14);
            caption.AddThemeColorOverride("font_color", Palette.GoldBright);
            caption.AddThemeColorOverride("font_outline_color", Palette.Ink);
            caption.AddThemeConstantOverride("outline_size", 4);
            caption.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            caption.Position -= new Vector2(0, 4);
            AddChild(caption);
        }

        MouseEntered += () => Lift(true);
        MouseExited += () => Lift(false);
    }

    private void Lift(bool up)
    {
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", up ? new Vector2(1.15f, 1.15f) : Vector2.One, 0.12f);
        ZIndex = up ? 10 : 0;
    }
}
