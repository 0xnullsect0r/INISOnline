using Godot;
using Inis.Core.Data;

namespace INISOnline;

/// <summary>
/// Bootstrap node. SCAFFOLD: confirms the shared engine is linked and loads the
/// card/territory catalogue. Phase 3 replaces this with the main menu + screen
/// manager (Catan-Universe-style UI).
/// </summary>
public partial class Main : Control
{
    public override void _Ready()
    {
        var data = GameData.Default;
        GD.Print($"INIS engine linked. Cards: {data.Cards.Count}, Territories: {data.Territories.Count}");

        var label = GetNodeOrNull<Label>("%StatusLabel");
        if (label is not null)
            label.Text = $"INIS Online — engine OK\nCards: {data.Cards.Count}  Territories: {data.Territories.Count}";
    }
}
