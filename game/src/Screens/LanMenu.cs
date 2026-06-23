using Godot;
using INISOnline.App;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>LAN entry: host a game on this machine, or browse for games on the network.</summary>
public partial class LanMenu : Screen
{
    public override void _Ready()
    {
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);

        column.AddChild(Ui.Heading("LAN"));

        var host = Ui.MenuButton("Host Game");
        host.Pressed += () => Nav.Show(new LanHostScreen());
        column.AddChild(host);

        var join = Ui.MenuButton("Find Games");
        join.Pressed += () => Nav.Show(new LanBrowser());
        column.AddChild(join);

        var back = Ui.MenuButton("Back");
        back.Pressed += () => Nav.Show(new ModeSelect());
        column.AddChild(back);
    }
}
