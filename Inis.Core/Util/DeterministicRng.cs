namespace Inis.Core.Util;

/// <summary>
/// Small deterministic PRNG (built on seeded <see cref="Random"/>) used for all
/// randomness in the engine so that a game is fully reproducible from its seed.
///
/// It also tracks a <see cref="Cursor"/> — the number of primitive draws consumed so
/// far. Persisting that cursor (in <c>GameState</c>) and replaying it on construction lets
/// a game that was serialized mid-play resume with the <em>same</em> future random sequence.
/// A seeded <see cref="Random"/> consumes exactly one internal sample per draw regardless
/// of the bound, so advancing the same number of draws reproduces the position exactly.
/// </summary>
public sealed class DeterministicRng
{
    private readonly Random _random;

    /// <summary>Number of draws consumed so far. Persist this to resume deterministically.</summary>
    public int Cursor { get; private set; }

    /// <summary>
    /// Creates a generator at <paramref name="seed"/>, fast-forwarded past
    /// <paramref name="cursor"/> previously-consumed draws (0 for a brand-new game).
    /// </summary>
    public DeterministicRng(int seed, int cursor = 0)
    {
        _random = new Random(seed);
        // Each consumed draw advanced the seeded stream by exactly one sample; replay them.
        for (var i = 0; i < cursor; i++) _random.Next(2);
        Cursor = cursor;
    }

    public int Next(int maxExclusive)
    {
        Cursor++;
        return _random.Next(maxExclusive);
    }

    /// <summary>In-place Fisher–Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
