using Godot;
using INISOnline.App;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>
/// Choose how to play: single-player vs AI, local hotseat, LAN, or online. Single-player and
/// hotseat run on the embedded engine (available now); LAN/online land in Phases 6–7.
/// </summary>
public partial class ModeSelect : Screen
{
    private readonly bool _online;

    public ModeSelect(bool online = false) => _online = online;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);

        column.AddChild(Ui.Heading(_online ? "Multiplayer" : "Choose a Mode"));
        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });

        column.AddChild(Mode("Single-player (vs AI)", () =>
            Nav.Show(new GameSetup(GameSetup.Mode.SinglePlayer))));
        column.AddChild(Mode("Local Hotseat", () =>
            Nav.Show(new GameSetup(GameSetup.Mode.Hotseat))));
        column.AddChild(Mode("LAN", () => Nav.Show(new LanMenu())));
        column.AddChild(Mode("Online", () =>
            Nav.Show(INISOnline.Net.Session.LoggedIn ? new OnlineMenu() : new AuthScreen())));

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        var back = Ui.MenuButton("Back");
        back.Pressed += () => Nav.Show(new MainMenu());
        column.AddChild(back);
    }

    private static Button Mode(string label, System.Action onPressed)
    {
        var b = Ui.MenuButton(label);
        b.Pressed += onPressed;
        return b;
    }
}
