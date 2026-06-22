using System.Text;
using InisServer.Auth;
using InisServer.Data;
using InisServer.Endpoints;
using InisServer.Game;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Configuration ----
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
    jwt.SigningKey = builder.Configuration["JWT_SIGNING_KEY"]
        ?? "dev-only-insecure-signing-key-change-me-please-32+chars";
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<JwtTokenService>();

// ---- Persistence (PostgreSQL) ----
var connString = builder.Configuration.GetConnectionString("Postgres")
    ?? builder.Configuration["POSTGRES_CONNECTION"]
    ?? "Host=postgres;Port=5432;Database=inis;Username=inis;Password=inis";
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connString));

// ---- Authoritative game sessions ----
builder.Services.AddSingleton<GameSessionManager>();

// ---- Auth ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
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
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyHeader().AllowAnyMethod().SetIsOriginAllowed(_ => true).AllowCredentials()));

var app = builder.Build();

// Apply EF migrations at startup (single-instance deployment) for versioned schema evolution.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseCors();
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
