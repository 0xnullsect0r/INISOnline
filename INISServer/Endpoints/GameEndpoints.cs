using System.Security.Claims;
using InisServer.Game;

namespace InisServer.Endpoints;

/// <summary>
/// The authoritative game WebSocket. A connected, authenticated client streams intents and
/// receives per-player redacted <c>StateSync</c>/<c>Event</c>/<c>TurnPrompt</c> messages from
/// its <see cref="GameSession"/>. Reconnection replays a full StateSync; users without a seat
/// connect as spectators. See docs/protocol.md.
/// </summary>
public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        // Authoritative game WebSocket. Authenticated via ?access_token=... (see Program.cs).
        app.Map("/ws/game/{id:guid}", async (HttpContext ctx, Guid id, GameSessionManager mgr) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            if (ctx.User.Identity?.IsAuthenticated != true ||
                ctx.User.FindFirstValue("sub") is not { } sub || !Guid.TryParse(sub, out var userId))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var session = await mgr.GetSessionAsync(id, ctx.RequestAborted);
            if (session is null)
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            await session.RunConnectionAsync(socket, userId, ctx.RequestAborted);
        });
    }
}
