using System.Text;
using System.Threading.RateLimiting;
using InisServer.Auth;
using InisServer.Data;
using InisServer.Endpoints;
using InisServer.Game;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
const string devSigningKey = "dev-only-insecure-signing-key-change-me-please-32+chars";
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    jwt.SigningKey = builder.Configuration["JWT_SIGNING_KEY"] ?? devSigningKey;
// Never boot a non-development deployment on the well-known dev key.
if (!builder.Environment.IsDevelopment() && jwt.SigningKey == devSigningKey)
    throw new InvalidOperationException(
        "JWT signing key is the insecure dev default. Set Jwt:SigningKey or JWT_SIGNING_KEY.");
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<JwtTokenService>();

// ---- Persistence (PostgreSQL) ----
var connString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["POSTGRES_CONNECTION"]
    ?? "Host=postgres;Port=5432;Database=inis;Username=inis;Password=inis";
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connString));

// ---- Authoritative game sessions ----
builder.Services.AddSingleton<GameSessionManager>();
builder.Services.AddHostedService<MaintenanceService>();

// ---- Rate limiting (per-IP fixed windows; strict on /auth) ----
var authPerMinute = builder.Configuration.GetValue("RateLimits:AuthPerMinute", 20);
var globalPerMinute = builder.Configuration.GetValue("RateLimits:GlobalPerMinute", 600);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    o.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// ---- Auth ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        // Keep JWT claim names verbatim ("sub", "unique_name") rather than remapping them to
        // the long XML claim-type URIs, so endpoints can read FindFirstValue("sub") directly.
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = "unique_name",
        };
        // Allow the access token on the game WebSocket via ?access_token=...
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/ws"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddOpenApi();

// ---- CORS ----
// Browsers only (the Godot client is not CORS-constrained). Development stays permissive;
// otherwise only the configured origins may call the API.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? new[] { "https://inis.aricummings.com" };
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (builder.Environment.IsDevelopment())
        p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials();
    else
        p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

// Initialize the schema at startup (single-instance deployment). Production applies versioned
// EF migrations; the integration-test host uses a provider (Sqlite) for which it just creates
// the schema from the model.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational() && db.Database.ProviderName!.Contains("Npgsql"))
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

app.UseCors();
app.UseRateLimiter();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

// ---- API docs (Scalar over OpenAPI) ----
app.MapOpenApi();
app.MapScalarApiReference(o => o.WithTitle("INIS Server API"));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();
app.MapFriendsEndpoints();
app.MapLobbyEndpoints();
app.MapGameEndpoints();

app.Run();

/// <summary>Exposed so the integration-test <c>WebApplicationFactory</c> can host the app.</summary>
public partial class Program;
