using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace INISOnline.Net;

/// <summary>
/// Thin async REST client for the INIS server (auth, friends, lobbies). Calls run on background
/// threads; callers marshal results back to Godot's main thread (e.g. via CallDeferred) before
/// touching the scene tree.
/// </summary>
public sealed class InisHttp
{
    private static readonly HttpClient Http = new();

    public sealed record Result(bool Ok, HttpStatusCode Status, JsonElement Body, string? Error);

    // ---- auth ----

    public Task<Result> RegisterAsync(string username, string password) =>
        AuthAsync("/auth/register", username, password);

    public Task<Result> LoginAsync(string username, string password) =>
        AuthAsync("/auth/login", username, password);

    private async Task<Result> AuthAsync(string path, string username, string password)
    {
        var result = await PostAsync(path, new { username, password }, authed: false);
        if (result.Ok)
        {
            Session.AccessToken = result.Body.GetProperty("accessToken").GetString();
            Session.RefreshToken = result.Body.GetProperty("refreshToken").GetString();
            Session.Username = result.Body.GetProperty("username").GetString();
        }
        return result;
    }

    // ---- lobbies ----

    public Task<Result> CreateLobbyAsync(int capacity) => PostAsync("/lobbies", new { capacity });
    public Task<Result> JoinByCodeAsync(string code) => PostAsync("/lobbies/join", new { code });
    public Task<Result> GetLobbyAsync(Guid id) => GetAsync($"/lobbies/{id}");
    public Task<Result> ReadyAsync(Guid id, bool ready) => PostAsync($"/lobbies/{id}/ready", new { ready });
    public Task<Result> SetSeatAiAsync(Guid id, int index, bool ai) => PostAsync($"/lobbies/{id}/seats/{index}/ai", new { ai });
    public Task<Result> LeaveAsync(Guid id) => PostAsync($"/lobbies/{id}/leave", new { });
    public Task<Result> StartAsync(Guid id) => PostAsync($"/lobbies/{id}/start", new { });
    public Task<Result> InviteAsync(Guid id, string username) => PostAsync($"/lobbies/{id}/invite", new { username });

    // ---- friends ----

    public Task<Result> FriendsAsync() => GetAsync("/friends");
    public Task<Result> SendFriendRequestAsync(string username) => PostAsync("/friends/requests", new { username });

    // ---- transport ----

    private async Task<Result> GetAsync(string path)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Url(path));
            Authorize(req);
            return await SendAsync(req);
        }
        catch (Exception ex) { return Fail(ex); }
    }

    private async Task<Result> PostAsync(string path, object body, bool authed = true)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Url(path)) { Content = JsonContent.Create(body) };
            if (authed) Authorize(req);
            return await SendAsync(req);
        }
        catch (Exception ex) { return Fail(ex); }
    }

    private static async Task<Result> SendAsync(HttpRequestMessage req)
    {
        using var resp = await Http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        var body = string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<JsonElement>(text);
        var error = resp.IsSuccessStatusCode ? null : ExtractError(body, resp.StatusCode);
        return new Result(resp.IsSuccessStatusCode, resp.StatusCode, body, error);
    }

    private static void Authorize(HttpRequestMessage req)
    {
        if (Session.AccessToken is { } token)
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string Url(string path) => Session.ServerUrl.TrimEnd('/') + path;

    private static string ExtractError(JsonElement body, HttpStatusCode status) =>
        body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out var e)
            ? e.GetString() ?? status.ToString()
            : status.ToString();

    private static Result Fail(Exception ex) =>
        new(false, HttpStatusCode.ServiceUnavailable, default, $"Network error: {ex.Message}");
}
