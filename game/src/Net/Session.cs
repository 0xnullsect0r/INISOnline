using Godot;

namespace INISOnline.Net;

/// <summary>
/// Holds the signed-in user's tokens and the configured server endpoint for the lifetime of the
/// app. (Persisting tokens to <c>user://</c> is a Phase 8 settings concern.)
/// </summary>
public static class Session
{
    /// <summary>Base REST endpoint. The reverse proxy fronts the server at this host.</summary>
    public static string ServerUrl { get; set; } = "https://inis.aricummings.com";

    /// <summary>
    /// Lets each build ship a different server endpoint via the
    /// <c>application/config/server_url</c> project setting (overridable per export), so the
    /// same code targets dev/staging/production without edits.
    /// </summary>
    public static void InitFromProject()
    {
        if (ProjectSettings.HasSetting("application/config/server_url") &&
            ProjectSettings.GetSetting("application/config/server_url").AsString() is { Length: > 0 } url)
            ServerUrl = url;
    }

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
