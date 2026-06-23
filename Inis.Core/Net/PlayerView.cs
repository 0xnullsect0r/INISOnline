using Inis.Core.Model;

namespace Inis.Core.Net;

/// <summary>
/// Computes a per-player <em>redacted</em> view of the authoritative state — the anti-cheat
/// boundary described in docs/protocol.md. The recipient sees all public state and their own
/// hidden information (hand, current draft hand); every other player's hidden zones are
/// masked to a placeholder so only their <em>counts</em> leak, never their contents. Hidden
/// information therefore never leaves the host in online play.
///
/// The result is a <see cref="GameState"/>-shaped clone so the client deserializes the same
/// type it already knows; masked cards carry the <see cref="Hidden"/> sentinel id.
/// </summary>
public static class PlayerView
{
    /// <summary>Placeholder card id for a face-down / hidden card.</summary>
    public const string Hidden = "?";

    /// <summary>
    /// Returns a redacted deep copy of <paramref name="state"/> for <paramref name="recipientId"/>.
    /// A null/unknown recipient (a spectator) sees no hidden information at all.
    /// </summary>
    public static GameState Redact(GameState state, string? recipientId)
    {
        // Deep clone via the canonical serializer, then mask in place — the original is untouched.
        var view = InisJson.DeserializeState(InisJson.SerializeState(state));

        // Secret-order draw zones: keep counts, hide contents.
        Mask(view.ActionDeck);
        Mask(view.EpicDeck);
        if (view.SetAsideActionCard is not null) view.SetAsideActionCard = Hidden;

        // The intent log can encode opponents' draft picks — it's an internal replay aid, not
        // for clients, so it is never broadcast.
        view.IntentLog.Clear();

        foreach (var p in view.Players)
            if (p.PlayerId != recipientId)
                Mask(p.Hand); // opponents' hand contents hidden (count preserved)

        if (view.Draft is not null)
        {
            for (var i = 0; i < view.Players.Count; i++)
            {
                if (view.Players[i].PlayerId == recipientId) continue;
                Mask(view.Draft.Hands[i]);
                Mask(view.Draft.Held[i]);
                Mask(view.Draft.Accumulated[i]);
            }
            Mask(view.Draft.LeftoverDeck);
        }

        return view;
    }

    private static void Mask(List<string> cards)
    {
        for (var i = 0; i < cards.Count; i++) cards[i] = Hidden;
    }
}
