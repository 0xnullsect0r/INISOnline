using System;
using Godot;
using INISOnline.App;
using INISOnline.Net;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>Hub for a signed-in player: create a lobby, join by invite code, or sign out.</summary>
public partial class OnlineMenu : Screen
{
    private readonly InisHttp _http = new();
    private LineEdit _code = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 12);
        AddChild(column);

        column.AddChild(Ui.Heading($"Welcome, {Session.Username}"));

        var create = Ui.MenuButton("Create Game");
        create.Pressed += () => _ = CreateAsync();
        column.AddChild(create);

        _code = Ui.Field("Invite code");
        column.AddChild(_code);
        var join = Ui.MenuButton("Join by Code");
        join.Pressed += () => _ = JoinAsync();
        column.AddChild(join);

        _status = Ui.Body("", Palette.Danger);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_status);

        var logout = Ui.MenuButton("Sign out");
        logout.Pressed += () => { Session.Clear(); Nav.Show(new MainMenu()); };
        column.AddChild(logout);
    }

    private async System.Threading.Tasks.Task CreateAsync()
    {
        var result = await _http.CreateLobbyAsync(4);
        Callable.From(() => OnLobby(result)).CallDeferred();
    }

    private async System.Threading.Tasks.Task JoinAsync()
    {
        var code = _code.Text.Trim();
        if (string.IsNullOrEmpty(code)) { _status.Text = "Enter an invite code."; return; }
        var result = await _http.JoinByCodeAsync(code);
        Callable.From(() => OnLobby(result)).CallDeferred();
    }

    private void OnLobby(InisHttp.Result result)
    {
        if (!result.Ok)
        {
            _status.Text = result.Error ?? "Could not open lobby.";
            return;
        }
        var lobbyId = Guid.Parse(result.Body.GetProperty("id").GetString()!);
        Nav.Show(new OnlineLobby(lobbyId));
    }
}
