using System;
using Godot;
using INISOnline.App;
using INISOnline.Lan;
using INISOnline.Net;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>
/// Host a LAN game: choose the player count, open the lobby (starts the WebSocket host + UDP
/// beacon), watch peers fill the seats, then start — empty seats become AI. The host plays in
/// process via <see cref="LanHostGame"/>.
/// </summary>
public partial class LanHostScreen : Screen
{
    private readonly string _hostName = Session.Username ?? "Host";
    private LanHost? _host;
    private LanDiscovery.Announcer? _announcer;
    private double _announceClock;

    private int _count = 3;
    private bool _seasons;
    private VBoxContainer _column = null!;
    private Label _info = null!;
    private VBoxContainer _seatList = null!;

    private int MaxPlayers => _seasons ? 5 : 4;

    public override void _Ready()
    {
        AddChild(Ui.Background());
        _column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _column.SetAnchorsPreset(LayoutPreset.Center);
        _column.AddThemeConstantOverride("separation", 14);
        AddChild(_column);
        BuildSetup();
    }

    private void BuildSetup()
    {
        Clear();
        _column.AddChild(Ui.Heading("Host a LAN Game"));

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        var minus = Ui.MenuButton("−", 72);
        minus.Pressed += () => SetCount(_count - 1);
        var label = Ui.Heading($"{_count} players");
        label.CustomMinimumSize = new Vector2(220, 0);
        label.Name = "CountLabel";
        var plus = Ui.MenuButton("+", 72);
        plus.Pressed += () => SetCount(_count + 1);
        row.AddChild(minus);
        row.AddChild(label);
        row.AddChild(plus);
        _column.AddChild(row);

        var seasons = new CheckButton { Text = "Seasons of Inis (5 players)", ButtonPressed = _seasons };
        seasons.Toggled += on => { _seasons = on; SetCount(_count); };
        _column.AddChild(seasons);

        var open = Ui.MenuButton("Open Lobby");
        open.Pressed += OpenLobby;
        _column.AddChild(open);

        var back = Ui.MenuButton("Back");
        back.Pressed += () => Nav.Show(new LanMenu());
        _column.AddChild(back);
    }

    private void SetCount(int value)
    {
        _count = Math.Clamp(value, 2, MaxPlayers);
        if (_column.FindChild("CountLabel", true, false) is Label l) l.Text = $"{_count} players";
    }

    private void OpenLobby()
    {
        _host = new LanHost();
        if (!_host.Open(_count, _seasons)) { BuildError("Could not open a network port."); return; }
        Nav.AddChild(_host);              // persist the host across the screen change to the game
        _host.ClaimLocalSeat(_hostName);
        _announcer = new LanDiscovery.Announcer();
        BuildLobby();
    }

    private void BuildLobby()
    {
        Clear();
        _column.AddChild(Ui.Heading("Lobby"));
        _info = Ui.Body($"Hosting on port {_host!.Port}. Players on your network can join from “Find Games”.", Palette.Gold);
        _info.HorizontalAlignment = HorizontalAlignment.Center;
        _column.AddChild(_info);

        _seatList = new VBoxContainer();
        _seatList.AddThemeConstantOverride("separation", 6);
        _column.AddChild(_seatList);

        var start = Ui.MenuButton("Start Game");
        start.Pressed += StartGame;
        _column.AddChild(start);

        var leave = Ui.MenuButton("Cancel");
        leave.Pressed += () => { Teardown(); Nav.Show(new LanMenu()); };
        _column.AddChild(leave);

        RenderSeats();
    }

    private void RenderSeats()
    {
        foreach (var child in _seatList.GetChildren()) child.QueueFree();
        var seats = _host!.SeatNames;
        for (var i = 0; i < seats.Count; i++)
        {
            var occupant = seats[i] ?? "— open (AI on start) —";
            var mine = i == 0 ? "  (you)" : "";
            _seatList.AddChild(Ui.Body($"Seat {i + 1}:  {occupant}{mine}", seats[i] is null ? Palette.Muted : Palette.Cream));
        }
    }

    private void StartGame()
    {
        _host!.Start();
        _announcer?.Close();
        _announcer = null;
        Nav.Show(new GameHud(new LanHostGame(_host)));
    }

    public override void _Process(double delta)
    {
        if (_host is null || _host.Started) return;
        RenderSeats();
        _announceClock += delta;
        if (_announceClock >= 1.0)
        {
            _announceClock = 0;
            var filled = 0;
            foreach (var n in _host.SeatNames) if (n is not null) filled++;
            _announcer?.Announce(new LanBeacon(_hostName, _host.Port, _host.Capacity, filled));
        }
    }

    private void Teardown()
    {
        _announcer?.Close();
        _announcer = null;
        if (_host is not null) { _host.Shutdown(); _host.QueueFree(); _host = null; }
    }

    private void BuildError(string message)
    {
        Clear();
        _column.AddChild(Ui.Heading("LAN"));
        _column.AddChild(Ui.Body(message, Palette.Danger));
        var back = Ui.MenuButton("Back");
        back.Pressed += () => Nav.Show(new LanMenu());
        _column.AddChild(back);
    }

    private void Clear()
    {
        foreach (var child in _column.GetChildren()) child.QueueFree();
    }
}
