using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace INISServer.Tests;

/// <summary>Small helpers for driving the REST API from tests.</summary>
public static class ApiHelpers
{
    private static int _counter;

    public static string UniqueName(string prefix) =>
        $"{prefix}{Interlocked.Increment(ref _counter)}_{Guid.NewGuid():N}".Substring(0, 16);

    /// <summary>Registers a fresh user and returns (username, accessToken, refreshToken).</summary>
    public static async Task<(string Username, string Access, string Refresh)> RegisterAsync(
        HttpClient client, string prefix = "user")
    {
        var username = UniqueName(prefix);
        var resp = await client.PostAsJsonAsync("/auth/register", new { username, password = "password123" });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return (username, json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!);
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<JsonElement> PostJsonAsync(HttpClient client, string token, string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var text = await resp.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<JsonElement>(text);
    }
}
