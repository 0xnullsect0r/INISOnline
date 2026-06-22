namespace Inis.Core.Model;

/// <summary>
/// In-progress state of the Assembly pick-and-pass Action-card draft. The draft runs
/// in rounds; in each round every seat holds <see cref="KeepCounts"/>[round] cards from
/// its working hand, then the remaining cards rotate to the next seat (in turn
/// direction) and are combined with the held cards for the next round. Two-player games
/// run two sub-drafts.
/// </summary>
public sealed class DraftState
{
    /// <summary>Cards held per round before passing, e.g. {1,2,3} for 3–4 players, {1,2} for 2.</summary>
    public required int[] KeepCounts { get; init; }

    /// <summary>Working hand for each seat (index = seat order).</summary>
    public required List<string>[] Hands { get; init; }

    /// <summary>Cards held so far this round, per seat.</summary>
    public required List<string>[] Held { get; init; }

    /// <summary>Final drafted cards accumulated across sub-drafts, per seat (used by 2-player).</summary>
    public required List<string>[] Accumulated { get; init; }

    public int Round { get; set; }
    public int PickerSeat { get; set; }

    /// <summary>Number of sub-drafts (1 for 3–4 players, 2 for the 2-player double draft).</summary>
    public int SubDraftCount { get; init; } = 1;
    public int SubDraft { get; set; }

    /// <summary>
    /// Action cards left over after the initial deal — the draw pool for the 2-player
    /// second sub-draft. Kept in the (serialized) draft state, not an engine field, so a
    /// game persisted mid-draft can be reconstructed and continue dealing identically.
    /// </summary>
    public List<string> LeftoverDeck { get; init; } = new();
}
