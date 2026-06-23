using System;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using INISOnline.App;
using INISOnline.Net;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>
/// A server lobby: polls its state, shows the seats, and lets the player ready-up while the host
/// fills empty seats with AI and starts. When the lobby reports a started game, everyone connects
/// to the authoritative game socket via <see cref="RemoteGame"/>.
/// </summary>
public partial class OnlineLobby : Screen
{
    private readonly InisHttp _http = new();
    private readonly Guid _lobbyId;

    private Label _title = null!;
    private Label _code = null!;
    private VBoxContainer _seats = null!;
    private HBoxContainer _controls = null!;
    private Label _status = null!;
    private Timer _poll = null!;
    private JsonElement _lobby;
    private bool _hasLobby;
    private bool _connecting;

    public OnlineLobby(Guid lobbyId) => _lobbyId = lobbyId;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 12);
        AddChild(column);

        _title = Ui.Heading("Lobby");
        column.AddChild(_title);
        _code = Ui.Body("", Palette.Gold);
        _code.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_code);

        _seats = new VBoxContainer();
        _seats.AddThemeConstantOverride("separation", 6);
        column.AddChild(_seats);

        _controls = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _controls.AddThemeConstantOverride("separation", 10);
        column.AddChild(_controls);

        _status = Ui.Body("", Palette.Danger);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_status);

        var leave = Ui.MenuButton("Leave");
        leave.Pressed += () => _ = LeaveAsync();
        column.AddChild(leave);

        _poll = new Timer { WaitTime = 1.5, Autostart = true };
        _poll.Timeout += () => _ = RefreshAsync();
        AddChild(_poll);
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var result = await _http.GetLobbyAsync(_lobbyId);
        Callable.From(() => OnLobby(result)).CallDeferred();
    }

    private void OnLobby(InisHttp.Result result)
    {
        if (!result.Ok) { _status.Text = result.Error ?? "Lobby unavailable."; return; }
        _lobby = result.Body;
        _hasLobby = true;
        _status.Text = "";

        // If the host has started the game, connect everyone to the authoritative socket.
        if (_lobby.TryGetProperty("gameId", out var gid) && gid.ValueKind == JsonValueKind.String && !_connecting)
        {
            _connecting = true;
            _poll.Stop();
            Nav.Show(new GameHud(new RemoteGame(gid.GetString()!)));
            return;
        }
        Render();
    }

    private void Render()
    {
        if (!_hasLobby) return;
        var isHost = _lobby.GetProperty("isHost").GetBoolean();
        _code.Text = $"Invite code:  {_lobby.GetProperty("inviteCode").GetString()}";

        foreach (var child in _seats.GetChildren()) child.QueueFree();
        var anyOpen = false;
        var filled = 0;
        foreach (var seat in _lobby.GetProperty("seats").EnumerateArray())
        {
            var index = seat.GetProperty("index").GetInt32();
            var open = seat.GetProperty("open").GetBoolean();
            var isAi = seat.GetProperty("isAi").GetBoolean();
            var ready = seat.GetProperty("ready").GetBoolean();
            var you = seat.GetProperty("you").GetBoolean();
            var who = seat.TryGetProperty("occupant", out var occ) && occ.ValueKind == JsonValueKind.String
                ? occ.GetString() : null;
            if (open) anyOpen = true; else filled++;

            var label = open ? "— open —" : isAi ? "AI" : who ?? "player";
            var tag = (you ? " (you)" : "") + (!open && !isAi && ready ? "  ✓ ready" : "");
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(Ui.Body($"Seat {index + 1}:  {label}{tag}", you ? Palette.GoldBright : Palette.Cream));

            if (isHost && open)
            {
                var fill = new Button { Text = "Add AI" };
                fill.Pressed += () => _ = SeatAiAsync(index, true);
                row.AddChild(fill);
            }
            else if (isHost && isAi)
            {
                var clear = new Button { Text = "Remove AI" };
                clear.Pressed += () => _ = SeatAiAsync(index, false);
                row.AddChild(clear);
            }
            _seats.AddChild(row);
        }

        foreach (var child in _controls.GetChildren()) child.QueueFree();

        var meReady = SelfReady();
        var readyBtn = new Button { Text = meReady ? "Unready" : "Ready" };
        readyBtn.Pressed += () => _ = ReadyAsync(!meReady);
        _controls.AddChild(readyBtn);

        if (isHost)
        {
            var start = new Button { Text = "Start Game" };
            var canStart = !anyOpen && filled >= 2;
            start.Disabled = !canStart;
            start.Pressed += () => _ = StartAsync();
            _controls.AddChild(start);
        }
    }

    private bool SelfReady()
    {
        foreach (var seat in _lobby.GetProperty("seats").EnumerateArray())
            if (seat.GetProperty("you").GetBoolean())
                return seat.GetProperty("ready").GetBoolean();
        return false;
    }

    private async Task SeatAiAsync(int index, bool ai)
    {
        await _http.SetSeatAiAsync(_lobbyId, index, ai);
        await RefreshAsync();
    }

    private async Task ReadyAsync(bool ready)
    {
        await _http.ReadyAsync(_lobbyId, ready);
        await RefreshAsync();
    }

    private async Task StartAsync()
    {
        var result = await _http.StartAsync(_lobbyId);
        if (!result.Ok) Callable.From(() => _status.Text = result.Error ?? "Could not start.").CallDeferred();
        else await RefreshAsync();
    }

    private async Task LeaveAsync()
    {
        _poll.Stop();
        await _http.LeaveAsync(_lobbyId);
        Callable.From(() => Nav.Show(new OnlineMenu())).CallDeferred();
    }
}
