using System.Text.Json;
using System.Text.Json.Serialization;
using Inis.Core.Model;
using Inis.Core.Moves;

namespace Inis.Core.Net;

/// <summary>Protocol-level constants shared by host and client (see docs/protocol.md).</summary>
public static class Protocol
{
    /// <summary>
    /// Wire version. v2 added the reaction (Triskel) windows, the Seasons of Inis
    /// subsystems and their enum members — v1 peers would crash on the new names, so
    /// hosts reject any envelope whose version does not match.
    /// </summary>
    public const int Version = 2;

    // Host -> client message types.
    public const string Hello = "Hello";
    public const string StateSync = "StateSync";
    public const string Event = "Event";
    public const string TurnPrompt = "TurnPrompt";
    public const string Error = "Error";
    public const string Chat = "Chat";

    // Client -> host intent types. The canonical encoding is a full Move under "Intent"
    // (the legal moves in a TurnPrompt are Moves the client echoes back); the named verbs
    // are conveniences mapped to the same Move.
    public const string Intent = "Intent";
    public const string Pass = "Pass";
    public const string Resign = "Resign";
    public const string DraftPick = "DraftPick";
    public const string PlayCard = "PlayCard";
    public const string TakePretender = "TakePretender";
    public const string DebugCommand = "DebugCommand";

    /// <summary>LAN-only handshake: a peer announces its display name to claim a seat
    /// (online play instead authenticates via JWT and maps the user to a lobby seat).</summary>
    public const string Join = "Join";
}

/// <summary>
/// A protocol message envelope: <c>{ "v":1, "type":"...", "seq":42, "payload":{...} }</c>.
/// Inbound payloads stay as a raw <see cref="JsonElement"/> until the type is known.
/// </summary>
public sealed record Envelope
{
    [JsonPropertyName("v")] public int V { get; init; } = Protocol.Version;
    [JsonPropertyName("type")] public string Type { get; init; } = "";
    [JsonPropertyName("seq")] public int Seq { get; init; }
    [JsonPropertyName("payload")] public JsonElement? Payload { get; init; }

    public static Envelope? TryParse(string json)
    {
        try { return JsonSerializer.Deserialize<Envelope>(json, InisJson.Options); }
        catch (JsonException) { return null; }
    }

    /// <summary>Deserializes the payload as <typeparamref name="T"/> (default when absent).</summary>
    public T? PayloadAs<T>() =>
        Payload is { } p ? p.Deserialize<T>(InisJson.Options) : default;
}

/// <summary>Builds the JSON for host→client messages.</summary>
public static class ServerMessages
{
    public static string Hello(string gameId, string playerId, bool spectator) =>
        Build(Protocol.Hello, new { gameId, playerId, spectator });

    /// <summary>Full redacted state for <paramref name="recipientId"/>.</summary>
    public static string StateSync(GameState state, string? recipientId) =>
        Build(Protocol.StateSync, PlayerView.Redact(state, recipientId));

    public static string Event(GameEvent e) => Build(Protocol.Event, e);

    public static string TurnPrompt(string playerId, IReadOnlyList<Move> legalMoves) =>
        Build(Protocol.TurnPrompt, new { playerId, legalMoves });

    public static string Error(string code, string message) =>
        Build(Protocol.Error, new { code, message });

    public static string Chat(string fromPlayerId, string text) =>
        Build(Protocol.Chat, new { fromPlayerId, text });

    private static string Build(string type, object payload) =>
        JsonSerializer.Serialize(new
        {
            v = Protocol.Version,
            type,
            payload,
        }, InisJson.Options);
}

/// <summary>Maps an inbound intent envelope to an engine <see cref="Move"/>.</summary>
public static class MoveCodec
{
    /// <summary>
    /// Builds the <see cref="Move"/> an envelope represents, attributed to
    /// <paramref name="actorPlayerId"/> (the authenticated seat). Returns null for
    /// non-move messages (e.g. Chat) and throws on malformed move payloads.
    /// </summary>
    public static Move? ToMove(Envelope env, string actorPlayerId)
    {
        switch (env.Type)
        {
            case Protocol.Intent:
            {
                var move = env.PayloadAs<Move>() ?? throw new FormatException("Empty Intent payload.");
                // The host always attributes the move to the authenticated seat.
                return move with { PlayerId = actorPlayerId };
            }
            case Protocol.Pass:
                return new Move { Type = MoveType.Pass, PlayerId = actorPlayerId };
            case Protocol.Resign:
                return new Move { Type = MoveType.Resign, PlayerId = actorPlayerId };
            case Protocol.TakePretender:
                return new Move { Type = MoveType.TakePretender, PlayerId = actorPlayerId };
            case Protocol.DraftPick:
            {
                var p = env.PayloadAs<MovePayload>() ?? new MovePayload();
                return new Move { Type = MoveType.DraftPick, PlayerId = actorPlayerId, CardId = p.CardId };
            }
            case Protocol.PlayCard:
            {
                var p = env.PayloadAs<MovePayload>() ?? new MovePayload();
                return new Move
                {
                    Type = MoveType.PlayCard, PlayerId = actorPlayerId,
                    CardId = p.CardId, TerritoryId = p.TerritoryId,
                    FromTerritoryId = p.FromTerritoryId, ToTerritoryId = p.ToTerritoryId,
                    TargetPlayerId = p.TargetPlayerId, TargetColor = p.TargetColor,
                    Amount = p.Amount, CardIds = p.CardIds,
                };
            }
            case Protocol.DebugCommand:
            {
                var p = env.PayloadAs<MovePayload>() ?? new MovePayload();
                return new Move
                {
                    Type = MoveType.Debug, PlayerId = actorPlayerId,
                    DebugCommand = p.Command, CardId = p.CardId, CardIds = p.CardIds,
                    TerritoryId = p.TerritoryId, Amount = p.Amount,
                };
            }
            default:
                return null; // Chat and unknown types are not moves.
        }
    }

    /// <summary>Loose payload shape for the named intent verbs.</summary>
    public sealed record MovePayload
    {
        public string? CardId { get; init; }
        public string? TerritoryId { get; init; }
        public string? FromTerritoryId { get; init; }
        public string? ToTerritoryId { get; init; }
        public string? TargetPlayerId { get; init; }
        public ClanColor? TargetColor { get; init; }
        public int Amount { get; init; }
        public IReadOnlyList<string>? CardIds { get; init; }

        /// <summary>Debug command verb (grant/remove/swap/set_deeds/spawn_clan).</summary>
        public string? Command { get; init; }
    }
}
