namespace INISOnline.Net;

/// <summary>
/// Holds the signed-in user's tokens and the configured server endpoint for the lifetime of the
/// app. (Persisting tokens to <c>user://</c> is a Phase 8 settings concern.)
/// </summary>
public static class Session
{
    /// <summary>Base REST endpoint. The reverse proxy fronts the server at this host.</summary>
    public static string ServerUrl { get; set; } = "https://inis.aricummings.com";

    public static string? AccessToken { get; set; }
    public static string? RefreshToken { get; set; }
    public static string? Username { get; set; }

    public static bool LoggedIn => !string.IsNullOrEmpty(AccessToken);

    /// <summary>WebSocket base derived from <see cref="ServerUrl"/> (https→wss, http→ws).</summary>
    public static string WebSocketBase => ServerUrl.StartsWith("https")
        ? "wss" + ServerUrl["https".Length..]
        : "ws" + ServerUrl["http".Length..];

    public static void Clear()
    {
        AccessToken = RefreshToken = Username = null;
    }
}
