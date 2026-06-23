using System.Security.Claims;
using InisServer.Data;
using Microsoft.EntityFrameworkCore;

namespace InisServer.Endpoints;

public sealed record FriendRequestBody(string Username);

public static class FriendsEndpoints
{
    public static void MapFriendsEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/friends").WithTags("Friends").RequireAuthorization();

        // List accepted friends + incoming/outgoing pending requests.
        g.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = UserId(principal);
            var rows = await db.Friendships
                .Include(f => f.Requester).Include(f => f.Addressee)
                .Where(f => f.RequesterId == me || f.AddresseeId == me)
                .ToListAsync();

            return Results.Ok(new
            {
                friends = rows.Where(f => f.Status == FriendshipStatus.Accepted)
                    .Select(f => Other(f, me)),
                incoming = rows.Where(f => f.Status == FriendshipStatus.Pending && f.AddresseeId == me)
                    .Select(f => new { id = f.Id, from = f.Requester!.Username }),
                outgoing = rows.Where(f => f.Status == FriendshipStatus.Pending && f.RequesterId == me)
                    .Select(f => new { id = f.Id, to = f.Addressee!.Username }),
            });
        });

        // Send a friend request by username.
        g.MapPost("/requests", async (FriendRequestBody body, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = UserId(principal);
            var target = await db.Users.FirstOrDefaultAsync(u => u.Username == body.Username);
            if (target is null) return Results.NotFound(new { error = "No such user." });
            if (target.Id == me) return Results.BadRequest(new { error = "Cannot friend yourself." });

            var exists = await db.Friendships.AnyAsync(f =>
                (f.RequesterId == me && f.AddresseeId == target.Id) ||
                (f.RequesterId == target.Id && f.AddresseeId == me));
            if (exists) return Results.Conflict(new { error = "Relationship already exists." });

            db.Friendships.Add(new Friendship { RequesterId = me, AddresseeId = target.Id });
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Accept / decline an incoming request.
        g.MapPut("/requests/{id:guid}", async (Guid id, string action, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = UserId(principal);
            var req = await db.Friendships.FirstOrDefaultAsync(f => f.Id == id && f.AddresseeId == me);
            if (req is null || req.Status != FriendshipStatus.Pending) return Results.NotFound();

            req.Status = action.Equals("accept", StringComparison.OrdinalIgnoreCase)
                ? FriendshipStatus.Accepted : FriendshipStatus.Declined;
            req.RespondedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok();
        });

        // Remove a friend / cancel a request.
        g.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDbContext db) =>
        {
            var me = UserId(principal);
            var f = await db.Friendships.FirstOrDefaultAsync(x =>
                x.Id == id && (x.RequesterId == me || x.AddresseeId == me));
            if (f is null) return Results.NotFound();
            db.Friendships.Remove(f);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
    }

    private static Guid UserId(ClaimsPrincipal p) => Guid.Parse(p.FindFirstValue("sub")!);

    private static object Other(Friendship f, Guid me) => f.RequesterId == me
        ? new { id = f.AddresseeId, username = f.Addressee!.Username }
        : new { id = f.RequesterId, username = f.Requester!.Username };
}
