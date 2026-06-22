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

    /// <summary>Epic Tale draw deck and (face-up) discard.</summary>
    public List<string> EpicDeck { get; } = new();
    public List<string> EpicDiscard { get; } = new();

    /// <summary>The one Action card set aside face-down during the deal (used by Cove etc.).</summary>
    public string? SetAsideActionCard { get; set; }

    /// <summary>Shared building reserves (10 each in the base game).</summary>
    public int CitadelsRemaining { get; set; } = 10;
    public int SanctuariesRemaining { get; set; } = 10;

    /// <summary>Deed-token pool (8 in the base game).</summary>
    public int DeedsRemaining { get; set; } = 8;

    /// <summary>Pretender-token pool (one per seat).</summary>
    public int PretendersRemaining { get; set; }

    /// <summary>Instance id of the Capital's territory, once chosen at setup.</summary>
    public string? CapitalInstanceId { get; set; }

    /// <summary>Turn-order direction set each Assembly by the Flock of Crows.</summary>
    public TurnDirection Direction { get; set; } = TurnDirection.Clockwise;

    public GamePhase Phase { get; set; } = GamePhase.Assembly;
    public AssemblyStep AssemblyStep { get; set; } = AssemblyStep.VictoryCheck;

    /// <summary>In-progress draft state during the Assembly draft step; null otherwise.</summary>
    public DraftState? Draft { get; set; }

    /// <summary>The clash currently being resolved, if any.</summary>
    public ClashState? ActiveClash { get; set; }

    /// <summary>What the engine is waiting on right now (whose move, and what kind).</summary>
    public PendingDecision? Pending { get; set; }

    /// <summary>Count of consecutive passes in the current Season (ends the phase at == player count).</summary>
    public int ConsecutivePasses { get; set; }

    /// <summary>True once the Brenn has played the opening card of the current Season.</summary>
    public bool BrennHasOpened { get; set; }

    /// <summary>Ordered log of applied moves — enables deterministic replay from the seed.</summary>
    public List<string> IntentLog { get; } = new();

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
