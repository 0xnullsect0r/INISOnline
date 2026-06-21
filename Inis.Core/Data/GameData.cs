using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Inis.Core.Data;

/// <summary>
/// Loads and caches the canonical card/territory catalogue from the embedded JSON
/// resources. Both the server and the client resolve the exact same definitions
/// through this type, so there is a single source of truth for game content.
/// </summary>
public sealed class GameData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public IReadOnlyList<CardDefinition> Cards { get; }
    public IReadOnlyList<TerritoryDefinition> Territories { get; }

    private readonly Dictionary<string, CardDefinition> _cardsById;
    private readonly Dictionary<string, TerritoryDefinition> _territoriesById;

    private GameData(IReadOnlyList<CardDefinition> cards, IReadOnlyList<TerritoryDefinition> territories)
    {
        Cards = cards;
        Territories = territories;
        _cardsById = cards.ToDictionary(c => c.Id);
        _territoriesById = territories.ToDictionary(t => t.Id);
    }

    /// <summary>Lazily-loaded default catalogue (base game).</summary>
    public static GameData Default { get; } = Load();

    public CardDefinition Card(string id) => _cardsById[id];
    public TerritoryDefinition Territory(string id) => _territoriesById[id];

    public bool TryGetCard(string id, out CardDefinition card) => _cardsById.TryGetValue(id, out card!);

    /// <summary>Loads the embedded catalogue. Exposed for tests / alternate content sets.</summary>
    public static GameData Load(Assembly? assembly = null)
    {
        assembly ??= typeof(GameData).Assembly;
        var cards = ReadResource<List<CardDefinition>>(assembly, "cards.json");
        var territories = ReadResource<List<TerritoryDefinition>>(assembly, "territories.json");
        return new GameData(cards, territories);
    }

    private static T ReadResource<T>(Assembly assembly, string fileName)
    {
        // Embedded resource names are namespace-qualified; match by suffix.
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded data resource '{fileName}' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize '{fileName}'.");
    }
}
