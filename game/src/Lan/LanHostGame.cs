using System;
using System.Collections.Generic;
using INISOnline.Game;
using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;

namespace INISOnline.Lan;

/// <summary>
/// The host's own player, plugged into the HUD as an <see cref="IGameSource"/>. It reads the
/// authoritative <see cref="LanHost"/> engine in-process (no socket), applying the same per-player
/// redaction the peers receive, and submits the host's moves directly to the host.
/// </summary>
public sealed class LanHostGame : IGameSource
{
    private readonly LanHost _host;
    private readonly GameData _data = GameData.Default;
    private GameState? _view;
    private int _seenVersion = -1;

    public LanHostGame(LanHost host) => _host = host;

    public bool Ready => _host.Started && _view is not null;
    public GameState State => _view ?? throw new InvalidOperationException("Game not started.");
    public PendingDecision? Pending => _view?.Pending;
    public bool IsGameOver => _view?.Phase == GamePhase.GameOver;

    public bool CanLocalAct =>
        Ready && !IsGameOver && Pending is { } p && p.PlayerId == _host.LocalHostPlayerId;

    public string StatusLine
    {
        get
        {
            if (!Ready) return "Waiting for players…";
            if (IsGameOver) return $"Winner: {SeatName(State.WinnerId ?? "?")}";
            if (Pending is { } p)
                return p.PlayerId == _host.LocalHostPlayerId ? "Your move" : $"Waiting for {SeatName(p.PlayerId)}…";
            return "—";
        }
    }

    public IReadOnlyList<Move> LegalMoves() => CanLocalAct ? _host.HostLegalMoves() : Array.Empty<Move>();
    public IReadOnlyList<string> Log => _host.HostLog;

    public string? LocalPlayerId => _host.LocalHostPlayerId;
    public bool SupportsChat => true;
    public void SendChat(string text) => _host.HostChat(text);

    public string SeatName(string playerId) =>
        _view?.Players.Find(p => p.PlayerId == playerId)?.DisplayName ?? playerId;

    public string TerritoryName(string instanceId) =>
        _view is not null && _view.Territories.TryGetValue(instanceId, out var t)
            ? _data.Territory(t.DefinitionId).Name : instanceId;

    public string Describe(Move move) => MoveText.Describe(move, SeatName, _data);

    public void Submit(Move move) => _host.HostApply(move);

    public void Debug(string command, string? cardId, int amount)
    {
        if (_host.LocalHostPlayerId is null) return;
        _host.HostDebug(new Move
        {
            Type = MoveType.Debug, PlayerId = _host.LocalHostPlayerId,
            DebugCommand = command, CardId = cardId, Amount = amount,
        });
    }

    public bool Poll(double delta)
    {
        if (!_host.Started || _host.Version == _seenVersion) return false;
        _seenVersion = _host.Version;
        // Redact for the host's own seat so masked cards read identically to the peers' view.
        _view = PlayerView.Redact(_host.Engine.State, _host.LocalHostPlayerId);
        return true;
    }
}
