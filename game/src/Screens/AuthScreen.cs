using System;
using System.Threading.Tasks;
using Godot;
using INISOnline.App;
using INISOnline.Net;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>Login / register for online play. On success it stores the session and opens the online menu.</summary>
public partial class AuthScreen : Screen
{
    private readonly InisHttp _http = new();
    private LineEdit _server = null!;
    private LineEdit _user = null!;
    private LineEdit _pass = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 12);
        AddChild(column);

        column.AddChild(Ui.Heading("Online"));
        _server = Ui.Field("Server URL", text: Session.ServerUrl);
        _user = Ui.Field("Username");
        _pass = Ui.Field("Password", secret: true);
        column.AddChild(_server);
        column.AddChild(_user);
        column.AddChild(_pass);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        var login = Ui.MenuButton("Log in", 170);
        login.Pressed += () => _ = SubmitAsync((u, p) => _http.LoginAsync(u, p));
        var register = Ui.MenuButton("Register", 170);
        register.Pressed += () => _ = SubmitAsync((u, p) => _http.RegisterAsync(u, p));
        buttons.AddChild(login);
        buttons.AddChild(register);
        column.AddChild(buttons);

        _status = Ui.Body("", Palette.Danger);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        column.AddChild(_status);

        var back = Ui.MenuButton("Back");
        back.Pressed += () => Nav.Show(new ModeSelect());
        column.AddChild(back);
    }

    private async Task SubmitAsync(Func<string, string, Task<InisHttp.Result>> call)
    {
        Session.ServerUrl = _server.Text.Trim();
        Callable.From(() => _status.Text = "Connecting…").CallDeferred();
        var result = await call(_user.Text.Trim(), _pass.Text);
        Callable.From(() => OnResult(result)).CallDeferred();
    }

    private void OnResult(InisHttp.Result result)
    {
        if (result.Ok) Nav.Show(new OnlineMenu());
        else _status.Text = result.Error ?? "Sign-in failed.";
    }
}
