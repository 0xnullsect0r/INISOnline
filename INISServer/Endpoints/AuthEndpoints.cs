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
        var g = app.MapGroup("/auth").WithTags("Auth").RequireRateLimiting("auth");

        g.MapPost("/register", async (RegisterRequest req, AppDbContext db, JwtTokenService jwt,
            HttpContext http, ILogger<AuthAudit> log) =>
        {
            if (req.Username.Length is < 3 or > 32)
                return Results.BadRequest(new { error = "Username must be 3–32 characters." });
            if (req.Password.Length < 10)
                return Results.BadRequest(new { error = "Password must be at least 10 characters." });
            if (await db.Users.AnyAsync(u => u.Username == req.Username))
            {
                log.LogWarning("Auth: register conflict for {Username} from {Ip}", req.Username, Ip(http));
                return Results.Conflict(new { error = "Username already taken." });
            }

            var user = new User { Username = req.Username, PasswordHash = "" };
            user.PasswordHash = Hasher.HashPassword(user, req.Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();

            log.LogInformation("Auth: registered {Username} ({UserId}) from {Ip}", user.Username, user.Id, Ip(http));
            return Results.Ok(await IssueTokens(db, jwt, user));
        });

        g.MapPost("/login", async (LoginRequest req, AppDbContext db, JwtTokenService jwt,
            HttpContext http, ILogger<AuthAudit> log) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Username == req.Username);
            if (user is null)
            {
                log.LogWarning("Auth: login failed (unknown user {Username}) from {Ip}", req.Username, Ip(http));
                return Results.Unauthorized();
            }
            var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
            if (result == PasswordVerificationResult.Failed)
            {
                log.LogWarning("Auth: login failed (bad password) for {Username} from {Ip}", req.Username, Ip(http));
                return Results.Unauthorized();
            }

            user.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            log.LogInformation("Auth: login {Username} ({UserId}) from {Ip}", user.Username, user.Id, Ip(http));
            return Results.Ok(await IssueTokens(db, jwt, user));
        });

        g.MapPost("/refresh", async (RefreshRequest req, AppDbContext db, JwtTokenService jwt,
            HttpContext http, ILogger<AuthAudit> log) =>
        {
            var hash = JwtTokenService.Hash(req.RefreshToken);
            var token = await db.RefreshTokens.Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == hash);
            if (token is null || !token.IsActive || token.User is null)
            {
                log.LogWarning("Auth: refresh rejected from {Ip}", Ip(http));
                return Results.Unauthorized();
            }

            token.RevokedAt = DateTimeOffset.UtcNow; // rotate
            var response = await IssueTokens(db, jwt, token.User);
            await db.SaveChangesAsync();
            log.LogInformation("Auth: refresh for {Username} ({UserId}) from {Ip}",
                token.User.Username, token.User.Id, Ip(http));
            return Results.Ok(response);
        });

        g.MapGet("/me", (ClaimsPrincipal user) =>
            Results.Ok(new { id = user.FindFirstValue("sub"), username = user.Identity?.Name }))
            .RequireAuthorization();
    }

    private static string Ip(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

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

/// <summary>Logger category for auth audit events.</summary>
public sealed class AuthAudit;
