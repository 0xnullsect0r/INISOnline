using System.Security.Claims;
using InisServer.Data;
using InisServer.Game;
using Microsoft.EntityFrameworkCore;

namespace InisServer.Endpoints;

public sealed record CreateLobbyRequest(int Capacity = 4, bool Seasons = false);
public sealed record JoinByCodeRequest(string Code);
public sealed record ReadyRequest(bool Ready);
public sealed record SeatAiRequest(bool Ai);
public sealed record InviteRequest(string Username);

/// <summary>
/// Pre-game lobby REST: create/join (open seat, invite code, or friend invite), AI fill,
/// ready-up, and start → spins up an authoritative <see cref="GameSession"/>. The game itself
/// is then played over the <c>/ws/game/{id}</c> socket (see <see cref="GameEndpoints"/>).
/// </summary>
public static class LobbyEndpoints
{
    public static void MapLobbyEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/lobbies").WithTags("Lobbies").RequireAuthorization();

        g.MapPost("/", (CreateLobbyRequest req, ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var max = req.Seasons ? 5 : 4;
            if (req.Capacity < 2 || req.Capacity > max)
                return Results.BadRequest(new { error = $"Capacity must be 2–{max}." });
            var lobby = mgr.CreateLobby(UserId(user), Username(user), req.Capacity, req.Seasons);
            return Results.Ok(View(lobby, UserId(user)));
        });

        g.MapGet("/", (ClaimsPrincipal user, GameSessionManager mgr) =>
            Results.Ok(mgr.OpenLobbies().Select(l => Summary(l)).ToList()));

        g.MapGet("/{id:guid}", (Guid id, ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var lobby = mgr.Get(id);
            return lobby is null ? Results.NotFound() : Results.Ok(View(lobby, UserId(user)));
        });

        g.MapPost("/{id:guid}/join", (Guid id, ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var lobby = mgr.Get(id);
            if (lobby is null) return Results.NotFound();
            return mgr.Join(lobby, UserId(user), Username(user), out var err)
                ? Results.Ok(View(lobby, UserId(user)))
                : Results.BadRequest(new { error = err });
        });

        g.MapPost("/join", (JoinByCodeRequest req, ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var lobby = mgr.ByCode(req.Code.Trim());
            if (lobby is null) return Results.NotFound(new { error = "No lobby with that code." });
            return mgr.Join(lobby, UserId(user), Username(user), out var err)
                ? Results.Ok(View(lobby, UserId(user)))
                : Results.BadRequest(new { error = err });
        });

        g.MapPost("/{id:guid}/leave", (Guid id, ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var lobby = mgr.Get(id);
            if (lobby is null) return Results.NotFound();
            mgr.Leave(lobby, UserId(user));
            return Results.Ok();
        });

        g.MapPost("/{id:guid}/ready", (Guid id, ReadyRequest req, ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var lobby = mgr.Get(id);
            if (lobby is null) return Results.NotFound();
            return mgr.SetReady(lobby, UserId(user), req.Ready, out var err)
                ? Results.Ok(View(lobby, UserId(user)))
                : Results.BadRequest(new { error = err });
        });

        g.MapPost("/{id:guid}/seats/{index:int}/ai", (Guid id, int index, SeatAiRequest req,
            ClaimsPrincipal user, GameSessionManager mgr) =>
        {
            var lobby = mgr.Get(id);
            if (lobby is null) return Results.NotFound();
            return mgr.SetSeatAi(lobby, UserId(user), index, req.Ai, out var err)
                ? Results.Ok(View(lobby, UserId(user)))
                : Results.BadRequest(new { error = err });
        });

        // Invite a friend (must be an accepted friend). Codes still let anyone join; this
        // records the invite and validates the relationship.
        g.MapPost("/{id:guid}/invite", async (Guid id, InviteRequest req,
            ClaimsPrincipal user, GameSessionManager mgr, AppDbContext db) =>
        {
            var lobby = mgr.Get(id);
            if (lobby is null) return Results.NotFound();
            var me = UserId(user);

            var friend = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (friend is null) return Results.NotFound(new { error = "No such user." });

            var areFriends = await db.Friendships.AnyAsync(f =>
                f.Status == FriendshipStatus.Accepted &&
                ((f.RequesterId == me && f.AddresseeId == friend.Id) ||
                 (f.RequesterId == friend.Id && f.AddresseeId == me)));
            if (!areFriends) return Results.BadRequest(new { error = "You can only invite friends." });

            return mgr.Invite(lobby, me, friend.Id, out var err)
                ? Results.Ok(new { lobby.InviteCode })
                : Results.BadRequest(new { error = err });
        });

        g.MapPost("/{id:guid}/start", async (Guid id, ClaimsPrincipal user,
            GameSessionManager mgr, CancellationToken ct) =>
        {
            var lobby = mgr.Get(id);
            if (lobby is null) return Results.NotFound();
            try
            {
                var gameId = await mgr.StartAsync(lobby, UserId(user), ct);
                return Results.Ok(new { gameId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // Lightweight game existence/status (the client connects to /ws/game/{id} to play).
        app.MapGet("/games/{id:guid}", async (Guid id, AppDbContext db) =>
        {
            var game = await db.Games.FirstOrDefaultAsync(x => x.Id == id);
            return game is null
                ? Results.NotFound()
                : Results.Ok(new { id = game.Id, status = game.Status.ToString(), seed = game.Seed });
        }).WithTags("Games").RequireAuthorization();
    }

    private static Guid UserId(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue("sub")!);
    private static string Username(ClaimsPrincipal p) =>
        p.Identity?.Name ?? p.FindFirstValue("unique_name") ?? "Player";

    private static object Summary(Lobby l) => new
    {
        id = l.Id,
        host = l.Seats.FirstOrDefault(s => s.UserId == l.HostUserId)?.Username,
        capacity = l.Seats.Count,
        filled = l.FilledSeats,
    };

    private static object View(Lobby l, Guid me) => new
    {
        id = l.Id,
        inviteCode = l.InviteCode,
        hostUserId = l.HostUserId,
        isHost = l.HostUserId == me,
        started = l.Started,
        gameId = l.GameId,
        seats = l.Seats.Select(s => new
        {
            index = s.Index,
            occupant = s.Username,
            isAi = s.IsAi,
            ready = s.Ready,
            open = s.IsOpen,
            you = s.UserId == me,
        }),
    };
}
