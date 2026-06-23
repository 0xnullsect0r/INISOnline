using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace INISServer.Tests;

public sealed class AuthAndFriendsTests : IClassFixture<InisAppFactory>
{
    private readonly InisAppFactory _factory;
    public AuthAndFriendsTests(InisAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Then_Me_Returns_Username()
    {
        var client = _factory.CreateClient();
        var (username, access, _) = await ApiHelpers.RegisterAsync(client);

        var me = await client.WithBearer(access).GetFromJsonAsync<JsonElement>("/auth/me");
        Assert.Equal(username, me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Login_Rejects_Wrong_Password()
    {
        var client = _factory.CreateClient();
        var (username, _, _) = await ApiHelpers.RegisterAsync(client);

        var ok = await client.PostAsJsonAsync("/auth/login", new { username, password = "password123" });
        Assert.True(ok.IsSuccessStatusCode);

        var bad = await client.PostAsJsonAsync("/auth/login", new { username, password = "wrong-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Refresh_Rotates_The_Token()
    {
        var client = _factory.CreateClient();
        var (_, _, refresh) = await ApiHelpers.RegisterAsync(client);

        var first = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = refresh });
        first.EnsureSuccessStatusCode();

        // The original refresh token has been rotated (revoked) and must no longer work.
        var reuse = await client.PostAsJsonAsync("/auth/refresh", new { refreshToken = refresh });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Friend_Request_Can_Be_Sent_And_Accepted()
    {
        var client = _factory.CreateClient();
        var a = await ApiHelpers.RegisterAsync(client, "alice");
        var b = await ApiHelpers.RegisterAsync(client, "bob");

        // Alice requests Bob.
        await ApiHelpers.PostJsonAsync(client, a.Access, "/friends/requests", new { username = b.Username });

        // Bob sees the incoming request and accepts it.
        var bobView = await Authed(client, b.Access).GetFromJsonAsync<JsonElement>("/friends");
        var incoming = bobView.GetProperty("incoming").EnumerateArray().Single();
        Assert.Equal(a.Username, incoming.GetProperty("from").GetString());
        var requestId = incoming.GetProperty("id").GetString();

        var accept = new HttpRequestMessage(HttpMethod.Put, $"/friends/requests/{requestId}?action=accept");
        accept.Headers.Authorization = new AuthenticationHeaderValue("Bearer", b.Access);
        (await client.SendAsync(accept)).EnsureSuccessStatusCode();

        // Alice now lists Bob as a friend.
        var aliceView = await Authed(client, a.Access).GetFromJsonAsync<JsonElement>("/friends");
        var friend = aliceView.GetProperty("friends").EnumerateArray().Single();
        Assert.Equal(b.Username, friend.GetProperty("username").GetString());
    }

    private static HttpClient Authed(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
