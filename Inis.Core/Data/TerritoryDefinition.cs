using Inis.Core.Model;

namespace Inis.Core.Data;

/// <summary>
/// Static definition of a territory tile, loaded from <c>Data/territories.json</c>.
/// The board graph (adjacency) is built at setup from the placed tiles; this record
/// describes a tile's identity, terrain and the Advantage power tied to it.
/// </summary>
public sealed record TerritoryDefinition
{
    /// <summary>Stable identifier, e.g. "territory.plains_of_battle".</summary>
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required TerrainType Terrain { get; init; }

    /// <summary>Id of the Advantage card associated with this territory, if any.</summary>
    public string? AdvantageCardId { get; init; }

    /// <summary>True if this tile carries a sanctuary at game start.</summary>
    public bool StartsWithSanctuary { get; init; }

    /// <summary>Relative path (under assets) to the tile art.</summary>
    public string? Art { get; init; }
}
