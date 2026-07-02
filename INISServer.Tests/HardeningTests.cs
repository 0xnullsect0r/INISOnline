using System.Net;
using System.Net.Http.Json;
using InisServer.Game;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace INISServer.Tests;

public sealed class HardeningTests : IClassFixture<InisAppFactory>
{
    private readonly InisAppFactory _factory;
    public HardeningTests(InisAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Auth_Endpoints_Rate_Limit_To_429()
    {
        // A private factory with a tiny window so the shared fixture's budget is untouched.
        using var factory = new LowLimitFactory();
        var client = factory.CreateClient();

        var responses = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            var resp = await client.PostAsJsonAsync("/auth/login",
                new { username = "nobody", password = "irrelevant-pw" });
            responses.Add(resp.StatusCode);
        }

        // First 3 hit the endpoint (401 for the unknown user), the rest are throttled.
        Assert.Equal(3, responses.Count(s => s == HttpStatusCode.Unauthorized));
        Assert.Equal(2, responses.Count(s => s == (HttpStatusCode)429));
    }

    [Fact]
    public async Task Register_Rejects_Short_Passwords()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/register",
            new { username = ApiHelpers.UniqueName("shortpw"), password = "nine-char" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Cors_Allows_Configured_Origin_Only()
    {
        var client = _factory.CreateClient();

        var allowed = new HttpRequestMessage(HttpMethod.Get, "/health");
        allowed.Headers.Add("Origin", "https://inis.aricummings.com");
        var allowedResp = await client.SendAsync(allowed);
        Assert.Contains("Access-Control-Allow-Origin", allowedResp.Headers.Select(h => h.Key));

        var denied = new HttpRequestMessage(HttpMethod.Get, "/health");
        denied.Headers.Add("Origin", "https://evil.example");
        var deniedResp = await client.SendAsync(denied);
        Assert.DoesNotContain("Access-Control-Allow-Origin", deniedResp.Headers.Select(h => h.Key));
    }

    [Fact]
    public async Task Sweep_Evicts_Stale_Lobby_And_Idle_Session_But_Game_Survives()
    {
        var manager = _factory.Services.GetRequiredService<GameSessionManager>();
        var hostId = Guid.NewGuid();

        // A stale unstarted lobby is dropped.
        var stale = manager.CreateLobby(hostId, "host", 2);
        stale.LastActivityUtc = DateTimeOffset.UtcNow - TimeSpan.FromHours(5);
        Assert.NotNull(manager.Get(stale.Id));

        // A started game whose session has no connections and is idle is evicted from memory…
        var lobby = manager.CreateLobby(hostId, "host", 2);
        Assert.True(manager.SetSeatAi(lobby, hostId, 1, true, out _));
        Assert.True(manager.SetReady(lobby, hostId, true, out _));
        var gameId = await manager.StartAsync(lobby, hostId, CancellationToken.None);
        Assert.NotNull(await manager.GetSessionAsync(gameId, CancellationToken.None));

        var removed = manager.Sweep(lobbyTtl: TimeSpan.FromHours(2), sessionIdle: TimeSpan.Zero);
        Assert.True(removed >= 2); // the stale lobby + the idle session (started lobby stays fresh)
        Assert.Null(manager.Get(stale.Id));

        // …but the persisted game rebuilds a session on demand.
        var rebuilt = await manager.GetSessionAsync(gameId, CancellationToken.None);
        Assert.NotNull(rebuilt);
    }

    private sealed class LowLimitFactory : InisAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimits:AuthPerMinute", "3");
        }
    }
}
