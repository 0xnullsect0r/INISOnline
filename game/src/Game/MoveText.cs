using System;
using System.Linq;
using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;

namespace INISOnline.Game;

/// <summary>
/// Shared human-readable labels for moves and engine events, used by both the local and remote
/// game sources so the HUD reads identically online and offline.
/// </summary>
public static class MoveText
{
    public static string Describe(Move move, Func<string, string> seatName, GameData data) => move.Type switch
    {
        MoveType.DraftPick => $"Draft: {CardName(data, move.CardId)}",
        MoveType.PlayCard => $"Play: {CardName(data, move.CardId)}",
        MoveType.Pass => "Pass",
        MoveType.TakePretender => "Claim victory (take a Pretender)",
        MoveType.Attack => $"Attack {seatName(move.TargetPlayerId ?? "")}",
        MoveType.Withdraw => "Withdraw",
        MoveType.EndClash => "Offer to end the clash",
        MoveType.ClashShelter => "Shelter a clan in a Citadel",
        MoveType.ClashSkipShelter => "Do not shelter",
        MoveType.AttackRemoveClan => "Lose a clan",
        MoveType.AttackDiscardCard => "Discard an Action card",
        MoveType.Resign => "Resign",
        _ => move.Type.ToString(),
    };

    public static string DescribeEvent(GameEvent e, Func<string, string> seatName, GameData data)
    {
        var who = e.PlayerId is not null ? seatName(e.PlayerId) : null;
        var card = e.CardId is not null && data.TryGetCard(e.CardId, out var def) ? def.Name : e.CardId;
        var parts = new[] { who, e.Kind, card, e.Detail }.Where(s => !string.IsNullOrEmpty(s));
        return string.Join(" · ", parts);
    }

    private static string CardName(GameData data, string? cardId) =>
        cardId is not null && data.TryGetCard(cardId, out var def) ? def.Name : cardId ?? "?";
}
