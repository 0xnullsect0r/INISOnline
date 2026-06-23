using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;

namespace Inis.Core.Debug;

/// <summary>
/// The gated debug / cheat command API (unlocked client-side by the code <c>INIS</c>). Commands
/// are applied to the authoritative state through the engine — the same state the network layer
/// then broadcasts — so a cheat in an online game is recognized everywhere. Commands are logged
/// for audit but not blocked. See docs/protocol.md and docs/design.md.
/// </summary>
public static class DebugCommandApi
{
    public const string UnlockCode = "INIS";

    /// <summary>Available command verbs (for a debug UI's picker).</summary>
    public static readonly IReadOnlyList<string> Commands =
        new[] { "grant", "remove", "swap", "set_deeds", "spawn_clan" };

    /// <summary>
    /// Applies a debug command to the authoritative game state. The target player defaults to
    /// <see cref="Move.PlayerId"/>. Returns the resulting events for broadcast.
    /// </summary>
    public static IReadOnlyList<GameEvent> Apply(GameEngine engine, Move move)
    {
        var state = engine.State;
        var events = new List<GameEvent>();
        var player = state.PlayerById(move.PlayerId ?? move.TargetPlayerId ?? "")
            ?? throw new InvalidOperationException("Debug command needs a valid player.");
        state.IntentLog.Add($"DEBUG:{move.DebugCommand}:{player.PlayerId}:{move.CardId}");

        switch (move.DebugCommand)
        {
            case "grant":
                RequireKnownCard(engine.Data, move.CardId);
                player.Hand.Add(move.CardId!);
                events.Add(new GameEvent("DebugGrant", player.PlayerId, move.CardId));
                break;

            case "remove":
                if (move.CardId is not null && player.Hand.Remove(move.CardId))
                    events.Add(new GameEvent("DebugRemove", player.PlayerId, move.CardId));
                break;

            case "swap":
                // Replace one held card (CardId) with another definition (CardIds[0]).
                RequireKnownCard(engine.Data, move.CardIds?.FirstOrDefault());
                if (move.CardId is not null) player.Hand.Remove(move.CardId);
                player.Hand.Add(move.CardIds![0]);
                events.Add(new GameEvent("DebugSwap", player.PlayerId, move.CardIds![0], Detail: move.CardId));
                break;

            case "set_deeds":
                player.Deeds = Math.Max(0, move.Amount);
                events.Add(new GameEvent("DebugSetDeeds", player.PlayerId, Detail: player.Deeds.ToString()));
                break;

            case "spawn_clan":
                var t = engine.Territory(move.TerritoryId)
                    ?? throw new InvalidOperationException("Unknown territory for spawn_clan.");
                t.AddClans(player.Color, Math.Max(1, move.Amount));
                events.Add(new GameEvent("DebugSpawnClan", player.PlayerId, TerritoryId: t.InstanceId));
                break;

            default:
                throw new InvalidOperationException($"Unknown debug command '{move.DebugCommand}'.");
        }
        return events;
    }

    private static void RequireKnownCard(GameData data, string? cardId)
    {
        if (cardId is null || !data.TryGetCard(cardId, out _))
            throw new InvalidOperationException($"Unknown card id '{cardId}'.");
    }
}
