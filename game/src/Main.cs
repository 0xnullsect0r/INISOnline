using Godot;
using INISOnline.App;
using INISOnline.Screens;
using INISOnline.Theme;
using Inis.Core.Data;

namespace INISOnline;

/// <summary>
/// Application root. Applies the shared theme, installs the <see cref="ScreenManager"/>, and
/// shows the main menu. Games are driven offline/hotseat through the embedded
/// <c>Inis.Core</c> engine (online play arrives in Phase 6).
/// </summary>
public partial class Main : Control
{
    public override void _Ready()
    {
        // Confirm the shared engine + content catalogue are linked.
        var data = GameData.Default;
        GD.Print($"INIS engine linked. Cards: {data.Cards.Count}, Territories: {data.Territories.Count}");

        Theme = UiTheme.Build();

        var screens = new ScreenManager();
        AddChild(screens);
        screens.Show(new MainMenu());
    }
}
