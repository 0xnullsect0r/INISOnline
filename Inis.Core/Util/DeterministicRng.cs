namespace Inis.Core.Util;

/// <summary>
/// Small deterministic PRNG (xorshift-ish via <see cref="Random"/>) used for all
/// randomness in the engine so that a game is fully reproducible from its seed.
/// </summary>
public sealed class DeterministicRng
{
    private readonly Random _random;

    public DeterministicRng(int seed) => _random = new Random(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    /// <summary>In-place Fisher–Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
