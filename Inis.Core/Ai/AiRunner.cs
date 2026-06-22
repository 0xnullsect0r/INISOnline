using Inis.Core.Model;
using Inis.Core.Rules;

namespace Inis.Core.Ai;

/// <summary>Drives an engine with <see cref="HeuristicAi"/> for every seat. Used by
/// single-player/seat-fill and by the AI-vs-AI engine soak tests.</summary>
public static class AiRunner
{
    /// <summary>
    /// Auto-plays until the game ends or <paramref name="maxMoves"/> is reached.
    /// Returns the number of moves applied. An optional <paramref name="onStep"/> hook
    /// runs after each applied move (used by tests to assert invariants every step).
    /// </summary>
    public static int PlayToEnd(GameEngine e, int maxMoves = 5000, Action<GameEngine>? onStep = null)
    {
        var moves = 0;
        while (e.State.Phase != GamePhase.GameOver && e.Pending is not null && moves < maxMoves)
        {
            e.Apply(HeuristicAi.ChooseMove(e));
            moves++;
            onStep?.Invoke(e);
        }
        return moves;
    }
}
