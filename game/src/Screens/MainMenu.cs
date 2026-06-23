using Godot;
using INISOnline.App;
using INISOnline.Audio;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>The landing screen: Play, Multiplayer, Settings, Quit over an animated backdrop.</summary>
public partial class MainMenu : Screen
{
    public override void _Ready()
    {
        AudioManager.Instance?.PlayMusic("menu_ambient");
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 16);
        AddChild(column);

        column.AddChild(Ui.Title("INIS"));
        var subtitle = Ui.Body("Celtic conquest of the island", Palette.Gold);
        subtitle.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(subtitle);

        column.AddChild(Spacer(24));
        column.AddChild(Play());
        column.AddChild(MultiplayerButton());
        column.AddChild(SettingsButton());
        column.AddChild(QuitButton());
    }

    private Button Play()
    {
        var b = Ui.MenuButton("Play");
        b.Pressed += () => Nav.Show(new ModeSelect());
        return b;
    }

    private Button MultiplayerButton()
    {
        var b = Ui.MenuButton("Multiplayer");
        b.Pressed += () => Nav.Show(new ModeSelect(online: true));
        return b;
    }

    private Button SettingsButton()
    {
        var b = Ui.MenuButton("Settings");
        b.Pressed += () => AddChild(new SettingsPanel());
        return b;
    }

    private Button QuitButton()
    {
        var b = Ui.MenuButton("Quit");
        b.Pressed += () => GetTree().Quit();
        return b;
    }

    private static Control Spacer(float height) =>
        new Control { CustomMinimumSize = new Vector2(0, height) };
}
