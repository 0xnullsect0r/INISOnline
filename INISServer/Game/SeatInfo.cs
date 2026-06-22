using Inis.Core.Model;

namespace InisServer.Game;

/// <summary>
/// Persisted mapping between an engine seat and the user (or AI) occupying it. Stored as
/// <c>SeatsJson</c> on the game row so connections can be routed to the right
/// <c>PlayerId</c> after a server restart / game reload.
/// </summary>
public sealed record SeatInfo(
    int Index,
    string PlayerId,
    Guid? UserId,
    string DisplayName,
    ClanColor Color,
    bool IsAi);
