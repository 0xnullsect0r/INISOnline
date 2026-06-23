namespace InisServer.Game;

/// <summary>One seat in a pre-game lobby: open, taken by a user, or filled by AI.</summary>
public sealed class LobbySeat
{
    public int Index { get; init; }
    public Guid? UserId { get; set; }
    public string? Username { get; set; }
    public bool IsAi { get; set; }
    public bool Ready { get; set; }

    /// <summary>True when no human and no AI occupies the seat.</summary>
    public bool IsOpen => UserId is null && !IsAi;
}

/// <summary>
/// An in-memory pre-game lobby (2–5 seats). Lobbies are ephemeral — only started games are
/// persisted — so they live in the <see cref="GameSessionManager"/> rather than the database.
/// </summary>
public sealed class Lobby
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string InviteCode { get; init; }
    public required Guid HostUserId { get; init; }

    /// <summary>True if this game uses the Seasons of Inis expansion (enables a 5th seat).</summary>
    public bool Seasons { get; init; }

    public List<LobbySeat> Seats { get; } = new();

    /// <summary>Users explicitly invited (friend invites) — informational; codes still let anyone join.</summary>
    public HashSet<Guid> InvitedUserIds { get; } = new();

    public bool Started { get; set; }
    public Guid? GameId { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    public LobbySeat? SeatOf(Guid userId) => Seats.FirstOrDefault(s => s.UserId == userId);
    public bool Contains(Guid userId) => Seats.Any(s => s.UserId == userId);
    public int FilledSeats => Seats.Count(s => s.UserId is not null || s.IsAi);
}
