using System.Text.Json;
using System.Text.Json.Serialization;
using Inis.Core.Model;

namespace Inis.Core.Net;

/// <summary>
/// Canonical JSON (de)serialization for engine state and wire messages. A single options
/// instance keeps the server's persistence, the network layer, and the client in lockstep —
/// there is one way game data is encoded. Enums are written by name for forward-compatible,
/// human-readable storage; <see cref="GameState"/> round-trips through its collections.
/// </summary>
public static class InisJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");

    /// <summary>Serializes a full game's authoritative state for persistence.</summary>
    public static string SerializeState(GameState state) => Serialize(state);

    /// <summary>Reconstructs a game's authoritative state from persisted JSON.</summary>
    public static GameState DeserializeState(string json) => Deserialize<GameState>(json);
}
