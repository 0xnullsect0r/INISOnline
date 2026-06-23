using System.Collections.Concurrent;
using System.Security.Cryptography;
using Inis.Core.Model;
using Inis.Core.Net;
using Inis.Core.Rules;
using InisServer.Data;
using Microsoft.EntityFrameworkCore;

namespace InisServer.Game;

/// <summary>
/// Singleton owning every lobby and live <see cref="GameSession"/>. Lobbies are in-memory and
/// ephemeral; started games are persisted, so a session missing from memory (e.g. after a
/// restart) is reconstructed from the database — the engine resumes deterministically thanks
/// to the persisted RNG cursor. All engine mutation happens inside the sessions.
/// </summary>
public sealed class GameSessionManager(IServiceScopeFactory scopes, ILoggerFactory loggers)
{
    private static readonly ClanColor[] SeatColors =
    {
        ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow,
        ClanColor.White, ClanColor.Purple, ClanColor.Orange, ClanColor.Teal,
    };

    private readonly ConcurrentDictionary<Guid, Lobby> _lobbies = new();
    private readonly ConcurrentDictionary<string, Guid> _codeToLobby = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, GameSession> _sessions = new();
    private readonly ILogger<GameSessionManager> _log = loggers.CreateLogger<GameSessionManager>();

    // --------------------------------------------------------------------- lobbies

    public Lobby CreateLobby(Guid hostUserId, string hostUsername, int capacity, bool seasons = false, bool extended = false)
    {
        var max = extended ? 8 : seasons ? 5 : 4;
        if (capacity < 2 || capacity > max)
            throw new ArgumentOutOfRangeException(nameof(capacity), $"Lobbies seat 2–{max} players in this mode.");

        var lobby = new Lobby
        {
            InviteCode = NewInviteCode(), HostUserId = hostUserId, Seasons = seasons, Extended = extended,
        };
        for (var i = 0; i < capacity; i++) lobby.Seats.Add(new LobbySeat { Index = i });
        lobby.Seats[0].UserId = hostUserId;
        lobby.Seats[0].Username = hostUsername;
        lobby.Seats[0].Ready = false;

        _lobbies[lobby.Id] = lobby;
        _codeToLobby[lobby.InviteCode] = lobby.Id;
        return lobby;
    }

    public Lobby? Get(Guid lobbyId) => _lobbies.GetValueOrDefault(lobbyId);

    public IEnumerable<Lobby> OpenLobbies() => _lobbies.Values.Where(l => !l.Started);

    public Lobby? ByCode(string code) =>
        _codeToLobby.TryGetValue(code, out var id) ? _lobbies.GetValueOrDefault(id) : null;

    public bool Join(Lobby lobby, Guid userId, string username, out string? error)
    {
        error = null;
        lock (lobby)
        {
            if (lobby.Started) { error = "Game already started."; return false; }
            if (lobby.Contains(userId)) return true; // idempotent re-join
            var seat = lobby.Seats.FirstOrDefault(s => s.IsOpen);
            if (seat is null) { error = "Lobby is full."; return false; }
            seat.UserId = userId;
            seat.Username = username;
            seat.Ready = false;
            return true;
        }
    }

    public bool Leave(Lobby lobby, Guid userId)
    {
        lock (lobby)
        {
            var seat = lobby.SeatOf(userId);
            if (seat is null) return false;
            seat.UserId = null;
            seat.Username = null;
            seat.Ready = false;
            // If the host leaves and the lobby empties, drop it entirely.
            if (lobby.Seats.All(s => s.UserId is null))
            {
                _lobbies.TryRemove(lobby.Id, out _);
                _codeToLobby.TryRemove(lobby.InviteCode, out _);
            }
            return true;
        }
    }

    public bool SetReady(Lobby lobby, Guid userId, bool ready, out string? error)
    {
        error = null;
        lock (lobby)
        {
            var seat = lobby.SeatOf(userId);
            if (seat is null) { error = "You are not in this lobby."; return false; }
            seat.Ready = ready;
            return true;
        }
    }

    /// <summary>Host toggles an open seat to AI (or back to open).</summary>
    public bool SetSeatAi(Lobby lobby, Guid hostUserId, int index, bool ai, out string? error)
    {
        error = null;
        lock (lobby)
        {
            if (lobby.HostUserId != hostUserId) { error = "Only the host can configure seats."; return false; }
            if (index < 0 || index >= lobby.Seats.Count) { error = "No such seat."; return false; }
            var seat = lobby.Seats[index];
            if (seat.UserId is not null) { error = "Seat is occupied by a player."; return false; }
            seat.IsAi = ai;
            return true;
        }
    }

    public bool Invite(Lobby lobby, Guid hostUserId, Guid friendUserId, out string? error)
    {
        error = null;
        lock (lobby)
        {
            if (lobby.HostUserId != hostUserId) { error = "Only the host can invite."; return false; }
            lobby.InvitedUserIds.Add(friendUserId);
            return true;
        }
    }

    /// <summary>
    /// Validates the lobby and starts a persisted game: builds seat configs (humans + AI fill),
    /// constructs the engine, saves the initial game row, and registers a live session.
    /// </summary>
    public async Task<Guid> StartAsync(Lobby lobby, Guid hostUserId, CancellationToken ct)
    {
        List<SeatInfo> seatInfos;
        int seed;
        lock (lobby)
        {
            if (lobby.HostUserId != hostUserId) throw new InvalidOperationException("Only the host can start.");
            if (lobby.Started) throw new InvalidOperationException("Game already started.");

            var max = lobby.MaxSeats;
            var active = lobby.Seats.Where(s => s.UserId is not null || s.IsAi).OrderBy(s => s.Index).ToList();
            if (active.Count < 2 || active.Count > max) throw new InvalidOperationException($"Need 2–{max} filled seats.");
            if (lobby.Seats.Any(s => s.IsOpen)) throw new InvalidOperationException("Fill or remove empty seats first.");
            if (active.Any(s => s.UserId is not null && !s.Ready)) throw new InvalidOperationException("All players must be ready.");

            seed = RandomNumberGenerator.GetInt32(int.MaxValue);
            seatInfos = new List<SeatInfo>(active.Count);
            for (var i = 0; i < active.Count; i++)
            {
                var s = active[i];
                var isAi = s.UserId is null && s.IsAi;
                var playerId = isAi ? $"ai-{i}" : s.UserId!.Value.ToString();
                var name = isAi ? $"AI {i + 1}" : s.Username ?? "Player";
                seatInfos.Add(new SeatInfo(i, playerId, s.UserId, name, SeatColors[i], isAi));
            }
        }

        var gameId = Guid.NewGuid();
        var engine = GameEngine.Create(gameId.ToString(), seed,
            seatInfos.Select(s => new SeatConfig(s.PlayerId, s.DisplayName, s.Color, s.IsAi)).ToList(),
            options: new GameOptions(lobby.Seasons, lobby.Extended));

        using (var scope = _scopes())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Games.Add(new Data.Game
            {
                Id = gameId,
                Seed = seed,
                Status = GameStatus.Active,
                StateJson = InisJson.SerializeState(engine.State),
                SeatsJson = InisJson.Serialize(seatInfos),
            });
            await db.SaveChangesAsync(ct);
        }

        var session = new GameSession(gameId, engine, seatInfos, scopes, _log);
        _sessions[gameId] = session;
        await session.InitializeAsync(ct);

        lock (lobby) { lobby.Started = true; lobby.GameId = gameId; }
        _log.LogInformation("Started game {Game} from lobby {Lobby} with {Seats} seats", gameId, lobby.Id, seatInfos.Count);
        return gameId;
    }

    private IServiceScope _scopes() => scopes.CreateScope();

    // -------------------------------------------------------------------- sessions

    /// <summary>Returns the live session for a game, reconstructing it from the database if needed.</summary>
    public async Task<GameSession?> GetSessionAsync(Guid gameId, CancellationToken ct)
    {
        if (_sessions.TryGetValue(gameId, out var existing)) return existing;

        Data.Game? row;
        using (var scope = _scopes())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            row = await db.Games.FirstOrDefaultAsync(g => g.Id == gameId, ct);
        }
        if (row is null) return null;

        var state = InisJson.DeserializeState(row.StateJson);
        var seats = InisJson.Deserialize<List<SeatInfo>>(row.SeatsJson);
        var engine = new GameEngine(state); // RNG cursor in state resumes the sequence exactly
        var session = new GameSession(gameId, engine, seats, scopes, _log);

        // Another caller may have reconstructed concurrently; keep the first winner.
        return _sessions.GetOrAdd(gameId, session);
    }

    private static string NewInviteCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no easily-confused chars
        Span<char> code = stackalloc char[6];
        for (var i = 0; i < code.Length; i++)
            code[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        return new string(code);
    }
}
