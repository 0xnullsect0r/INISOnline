namespace Inis.Core.Model;

/// <summary>Runtime state for a single player (seat) in a game.</summary>
public sealed class PlayerState
{
    public required string PlayerId { get; init; }
    public required string DisplayName { get; init; }
    public required ClanColor Color { get; init; }
    public bool IsAi { get; init; }

    /// <summary>Action + Epic Tale cards currently held (by card definition id).</summary>
    public List<string> Hand { get; } = new();

    /// <summary>Advantage cards this player holds face-up (territory advantages in play).</summary>
    public List<string> Advantages { get; } = new();

    /// <summary>Deed tokens — each acts as a wild +1 toward one victory condition.</summary>
    public int Deeds { get; set; }

    /// <summary>Clans of this player still off the board (reserve / available to deploy).</summary>
    public int ClanReserve { get; set; }

    public bool HasPretenderToken { get; set; }

    /// <summary>Set once the player has consecutively passed in the current Season phase.</summary>
    public bool HasPassed { get; set; }
}
