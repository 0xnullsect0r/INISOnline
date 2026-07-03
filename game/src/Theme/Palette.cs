using Godot;
using Inis.Core.Model;

namespace INISOnline.Theme;

/// <summary>
/// The warm parchment + slate Celtic palette used across every screen (the friendly,
/// premium Catan-Universe feel described in docs/design.md). Centralized so the look is
/// changed in one place.
/// </summary>
public static class Palette
{
    // Surfaces
    public static readonly Color Parchment = Color.FromHtml("E8DCC0");
    public static readonly Color ParchmentDark = Color.FromHtml("D6C49A");
    public static readonly Color Slate = Color.FromHtml("23303A");
    public static readonly Color SlateLight = Color.FromHtml("32434F");
    public static readonly Color SlatePanel = Color.FromHtml("2B3A45");

    // Accents
    public static readonly Color Gold = Color.FromHtml("C9A24B");
    public static readonly Color GoldBright = Color.FromHtml("E3C36A");
    public static readonly Color Bronze = Color.FromHtml("9C6B3C");

    // Text
    public static readonly Color Ink = Color.FromHtml("2A241B");      // on parchment
    public static readonly Color Cream = Color.FromHtml("ECE4D2");    // on slate
    public static readonly Color Muted = Color.FromHtml("A9B3B8");    // secondary on slate

    // Semantic
    public static readonly Color Success = Color.FromHtml("4A7A5A");
    public static readonly Color Danger = Color.FromHtml("A6432F");

    /// <summary>The per-player clan colours (base game uses Red/Blue/Green/Yellow).</summary>
    public static Color Clan(ClanColor color) => INISOnline.Audio.Settings.ColorblindClans
        ? color switch
        {
            // Okabe–Ito inspired palette: distinguishable under the common dichromacies.
            ClanColor.Red => Color.FromHtml("D55E00"),    // vermillion
            ClanColor.Blue => Color.FromHtml("0072B2"),   // strong blue
            ClanColor.Green => Color.FromHtml("009E73"),  // bluish green
            ClanColor.Yellow => Color.FromHtml("F0E442"), // bright yellow
            ClanColor.White => Color.FromHtml("F5F5F0"),
            ClanColor.Purple => Color.FromHtml("CC79A7"), // reddish purple
            ClanColor.Orange => Color.FromHtml("E69F00"),
            ClanColor.Teal => Color.FromHtml("56B4E9"),   // sky blue
            _ => Cream,
        }
        : color switch
        {
            ClanColor.Red => Color.FromHtml("B23A33"),
            ClanColor.Blue => Color.FromHtml("2E5E8C"),
            ClanColor.Green => Color.FromHtml("3E7D4F"),
            ClanColor.Yellow => Color.FromHtml("C9A227"),
            ClanColor.White => Color.FromHtml("E8E4D8"),
            ClanColor.Purple => Color.FromHtml("7E4B8E"),
            ClanColor.Orange => Color.FromHtml("CC6B2C"),
            ClanColor.Teal => Color.FromHtml("2E8B8B"),
            _ => Cream,
        };
}
