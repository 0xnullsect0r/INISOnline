using System.Net.WebSockets;
using System.Text;

namespace InisServer.Endpoints;

/// <summary>
/// Lobby + authoritative game-session endpoints.
/// SCAFFOLD: the WebSocket endpoint currently accepts an authenticated socket and
/// echoes a hello. Phase 5 wires this to a per-game <c>Inis.Core</c> session that
/// validates intents and broadcasts redacted state diffs/events. See docs/protocol.md.
/// </summary>
public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        // Lobby REST (stubs — fleshed out in Phase 5).
        var g = app.MapGroup("/lobbies").WithTags("Lobbies").RequireAuthorization();
        g.MapGet("/", () => Results.Ok(Array.Empty<object>()));
        g.MapPost("/", () => Results.Ok(new { id = Guid.NewGuid() }));

        // Authoritative game WebSocket. Authenticated via ?access_token=... (see Program.cs).
        app.Map("/ws/game/{id}", async (HttpContext ctx, string id) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            if (ctx.User.Identity?.IsAuthenticated != true)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            var hello = Encoding.UTF8.GetBytes($"{{\"type\":\"Hello\",\"game\":\"{id}\"}}");
            await socket.SendAsync(hello, WebSocketMessageType.Text, true, ctx.RequestAborted);

            var buffer = new byte[8192];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ctx.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", ctx.RequestAborted);
                    break;
                }
                // TODO Phase 5: parse intent -> validate with Inis.Core -> mutate -> broadcast diff.
            }
        });
    }
}
