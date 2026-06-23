using Godot;
using INISOnline.App;
using INISOnline.Lan;
using INISOnline.Net;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>Lists LAN games discovered over UDP and connects to the chosen host as a peer.</summary>
public partial class LanBrowser : Screen
{
    private readonly string _name = Session.Username ?? "Player";
    private LanDiscovery.Browser? _browser;
    private VBoxContainer _list = null!;
    private double _refreshClock;

    public override void _Ready()
    {
        AddChild(Ui.Background());
        _browser = new LanDiscovery.Browser();

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 14);
        AddChild(column);

        column.AddChild(Ui.Heading("LAN Games"));
        column.AddChild(Ui.Body("Searching the local network…", Palette.Muted));

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 8);
        column.AddChild(_list);

        var back = Ui.MenuButton("Back");
        back.Pressed += () => { _browser?.Close(); Nav.Show(new LanMenu()); };
        column.AddChild(back);
    }

    public override void _Process(double delta)
    {
        _browser?.Poll();
        _refreshClock += delta;
        if (_refreshClock < 0.5) return;
        _refreshClock = 0;
        Render();
    }

    private void Render()
    {
        foreach (var child in _list.GetChildren()) child.QueueFree();
        foreach (var (beacon, url) in _browser!.Seen)
        {
            var button = Ui.MenuButton($"{beacon.Name}   {beacon.Filled}/{beacon.Capacity}", 420);
            var target = url;
            button.Pressed += () => Connect(target);
            _list.AddChild(button);
        }
    }

    private void Connect(string url)
    {
        _browser?.Close();
        _browser = null;
        Nav.Show(new GameHud(new LanClientGame(url, _name)));
    }
}
