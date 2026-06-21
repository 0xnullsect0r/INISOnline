namespace Inis.Core.Model;

/// <summary>
/// The full authoritative state of a game. Mutated only by the rules layer
/// (<c>Inis.Core.Rules</c>). Serialized for persistence and for client sync
/// (with per-player redaction applied at the networking boundary).
/// </summary>
public sealed class GameState
{
    public required string GameId { get; init; }

    /// <summary>Seed for the deterministic RNG — enables reproducible games / replays.</summary>
    public required int Seed { get; init; }

    /// <summary>Seats in turn order.</summary>
    public List<PlayerState> Players { get; } = new();

    /// <summary>Territories currently on the island, keyed by instance id.</summary>
    public Dictionary<string, TerritoryState> Territories { get; } = new();

    /// <summary>Action card draw deck (definition ids, expanded by count, shuffled).</summary>
    public List<string> ActionDeck { get; } = new();
    public List<string> ActionDiscard { get; } = new();

    /// <summary>Epic Tale draw deck.</summary>
    public List<string> EpicDeck { get; } = new();

    public GamePhase Phase { get; set; } = GamePhase.Assembly;
    public AssemblyStep AssemblyStep { get; set; } = AssemblyStep.VictoryCheck;

    /// <summary>Index into <see cref="Players"/> of the current Brenn (round leader).</summary>
    public int BrennIndex { get; set; }

    /// <summary>Index of the player whose turn it currently is.</summary>
    public int CurrentPlayerIndex { get; set; }

    public int RoundNumber { get; set; } = 1;

    /// <summary>Winner's player id once the game is over; null otherwise.</summary>
    public string? WinnerId { get; set; }

    public PlayerState CurrentPlayer => Players[CurrentPlayerIndex];
    public PlayerState Brenn => Players[BrennIndex];

    public PlayerState? PlayerById(string id) => Players.FirstOrDefault(p => p.PlayerId == id);
    public PlayerState? PlayerByColor(ClanColor color) => Players.FirstOrDefault(p => p.Color == color);
}
