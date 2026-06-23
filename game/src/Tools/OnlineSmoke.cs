using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using INISOnline.Net;
using Inis.Core.Model;
using Inis.Core.Moves;

namespace INISOnline.Tools;

/// <summary>
/// End-to-end online validation against a running server (default http://localhost:8080):
/// <c>godot --headless res://scenes/OnlineSmoke.tscn</c>. It registers a user, creates an
/// AI-filled lobby, starts the game, then plays it to completion over the real WebSocket via
/// <see cref="RemoteGame"/> — printing <c>ONLINE …</c> lines. A dev aid; not part of the app.
/// </summary>
public partial class OnlineSmoke : Node
{
    public override async void _Ready()
    {
        Session.ServerUrl = OS.GetEnvironment("INIS_SERVER") is { Length: > 0 } url ? url : "http://localhost:8080";
        var http = new InisHttp();

        var user = "bot_" + Guid.NewGuid().ToString("N")[..8];
        var reg = await http.RegisterAsync(user, "password123");
        GD.Print($"ONLINE register: ok={reg.Ok} {reg.Error}");
        if (!reg.Ok) { Quit(); return; }

        var lobby = await http.CreateLobbyAsync(3);
        var id = Guid.Parse(lobby.Body.GetProperty("id").GetString()!);
        await http.SetSeatAiAsync(id, 1, true);
        await http.SetSeatAiAsync(id, 2, true);
        await http.ReadyAsync(id, true);

        var start = await http.StartAsync(id);
        GD.Print($"ONLINE start: ok={start.Ok} {start.Error}");
        if (!start.Ok) { Quit(); return; }
        var gameId = start.Body.GetProperty("gameId").GetString()!;

        var game = new RemoteGame(gameId);
        int guard = 0, moves = 0;
        while (!game.IsGameOver && guard++ < 6000)
        {
            game.Poll(0.1);
            if (game.CanLocalAct)
            {
                var legal = game.LegalMoves();
                var pick = legal.FirstOrDefault(m => m.Type == MoveType.TakePretender) ?? legal[0];
                game.Submit(pick);
                moves++;
            }
            await Task.Delay(15);
        }

        GD.Print($"ONLINE play: ready={game.Ready} gameOver={game.IsGameOver} movesSubmitted={moves} winner={(game.Ready ? game.State.WinnerId : null)}");
        game.Dispose();
        Quit();
    }

    private void Quit() => Callable.From(() => GetTree().Quit()).CallDeferred();
}
