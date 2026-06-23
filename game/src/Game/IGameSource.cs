using System.Collections.Generic;
using Inis.Core.Model;
using Inis.Core.Moves;

namespace INISOnline.Game;

/// <summary>
/// What the in-game HUD needs from whatever is driving the game — the embedded engine
/// (<see cref="LocalGame"/>, offline/hotseat) or a server connection (<c>RemoteGame</c>, online).
/// The HUD renders <see cref="State"/>, offers <see cref="LegalMoves"/> when
/// <see cref="CanLocalAct"/>, and submits the chosen move; <see cref="Poll"/> is pumped each
/// frame to advance AI (local) or apply incoming server updates (remote).
/// </summary>
public interface IGameSource
{
    /// <summary>True once a state is available to render (always true offline; after first sync online).</summary>
    bool Ready { get; }

    GameState State { get; }
    PendingDecision? Pending { get; }
    bool IsGameOver { get; }

    /// <summary>True when a local human may pick a move right now.</summary>
    bool CanLocalAct { get; }

    /// <summary>A short status line (e.g. "AI is thinking…", "Waiting for Alice…", "Your move").</summary>
    string StatusLine { get; }

    IReadOnlyList<Move> LegalMoves();
    IReadOnlyList<string> Log { get; }

    string SeatName(string playerId);
    string TerritoryName(string instanceId);
    string Describe(Move move);

    /// <summary>Submit a chosen move (applies locally, or sends an intent to the server).</summary>
    void Submit(Move move);

    /// <summary>Issue a server-authoritative debug/cheat command (grant/set_deeds/…); works online.</summary>
    void Debug(string command, string? cardId, int amount);

    /// <summary>Advance the source by <paramref name="delta"/> seconds; returns true if state changed.</summary>
    bool Poll(double delta);
}
