namespace InisServer.Data;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastSeenAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = new();
}

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}

public enum GameStatus { Active, Completed, Abandoned }

/// <summary>
/// A persisted authoritative game. <see cref="StateJson"/> is the serialized
/// <c>Inis.Core</c> <c>GameState</c> (stored as jsonb on PostgreSQL); the engine is
/// reconstructed from it on reload — which is why the engine persists its RNG cursor.
/// <see cref="SeatsJson"/> records the seat→player/user/AI mapping for connection routing.
/// </summary>
public sealed class Game
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Seed { get; set; }
    public GameStatus Status { get; set; } = GameStatus.Active;
    public required string StateJson { get; set; }
    public required string SeatsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum FriendshipStatus { Pending, Accepted, Declined, Blocked }

public sealed class Friendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public User? Requester { get; set; }
    public Guid AddresseeId { get; set; }
    public User? Addressee { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}
