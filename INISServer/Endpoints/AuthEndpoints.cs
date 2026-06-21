using System.Security.Claims;
using InisServer.Auth;
using InisServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InisServer.Endpoints;

public sealed record RegisterRequest(string Username, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record AuthResponse(string AccessToken, string RefreshToken, string Username);

public static class AuthEndpoints
{
    private static readonly PasswordHasher<User> Hasher = new();

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth").WithTags("Auth");

        g.MapPost("/register", async (RegisterRequest req, AppDbContext db, JwtTokenService jwt) =>
        {
            if (req.Username.Length is < 3 or > 32)
                return Results.BadRequest(new { error = "Username must be 3–32 characters." });
            if (req.Password.Length < 8)
                return Results.BadRequest(new { error = "Password must be at least 8 characters." });
            if (await db.Users.AnyAsync(u => u.Username == req.Username))
                return Results.Conflict(new { error = "Username already taken." });

            var user = new User { Username = req.Username, PasswordHash = "" };
            user.PasswordHash = Hasher.HashPassword(user, req.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Ok(await IssueTokens(db, jwt, user));
        });

        g.MapPost("/login", async (LoginRequest req, AppDbContext db, JwtTokenService jwt) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null) return Results.Unauthorized();
            var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (result == PasswordVerificationResult.Failed) return Results.Unauthorized();

            user.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return Results.Ok(await IssueTokens(db, jwt, user));
        });

        g.MapPost("/refresh", async (RefreshRequest req, AppDbContext db, JwtTokenService jwt) =>
        {
            var hash = JwtTokenService.Hash(req.RefreshToken);
            var token = await db.RefreshTokens.Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token is null || !token.IsActive || token.User is null) return Results.Unauthorized();

            token.RevokedAt = DateTimeOffset.UtcNow; // rotate
            var response = await IssueTokens(db, jwt, token.User);
            await db.SaveChangesAsync();
            return Results.Ok(response);
        });

        g.MapGet("/me", (ClaimsPrincipal user) =>
            Results.Ok(new { id = user.FindFirstValue("sub"), username = user.Identity?.Name }))
            .RequireAuthorization();
    }

    private static async Task<AuthResponse> IssueTokens(AppDbContext db, JwtTokenService jwt, User user)
    {
        var access = jwt.CreateAccessToken(user.Id, user.Username);
        var (raw, hash) = jwt.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        });
        await db.SaveChangesAsync();
        return new AuthResponse(access, raw, user.Username);
    }
}
