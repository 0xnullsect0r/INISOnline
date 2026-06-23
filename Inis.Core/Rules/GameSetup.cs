using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Util;

namespace Inis.Core.Rules;

/// <summary>Player descriptor supplied when creating a game.</summary>
public sealed record SeatConfig(string PlayerId, string DisplayName, ClanColor Color, bool IsAi = false);

/// <summary>
/// Builds the initial <see cref="GameState"/> for the base game (2–5 seats).
/// Board placement, starting clans and the first Brenn follow the rulebook setup;
/// exact tile-layout-by-player-count is finalized during Phase 2.
/// </summary>
public static class GameSetup
{
    public const int VictoryThreshold = 6;

    public static GameState Create(string gameId, int seed, IReadOnlyList<SeatConfig> seats, GameData? data = null)
    {
        if (seats.Count is < 2 or > 5)
            throw new ArgumentOutOfRangeException(nameof(seats), "Base game supports 2–5 players.");

        data ??= GameData.Default;
        var rng = new DeterministicRng(seed);

        var state = new GameState { GameId = gameId, Seed = seed };

        foreach (var seat in seats)
        {
            state.Players.Add(new PlayerState
            {
                PlayerId = seat.PlayerId,
                DisplayName = seat.DisplayName,
                Color = seat.Color,
                IsAi = seat.IsAi,
                ClanReserve = 12, // 12 clan figures per color in the base game
            });
        }

        BuildDecks(state, data, rng);
        // Initial board, starting clans and first Brenn are applied by the phase
        // machine on first Assembly (Phase 2). The first player is provisional Brenn.
        state.BrennIndex = 0;
        state.CurrentPlayerIndex = 0;

        return state;
    }

    private static void BuildDecks(GameState state, GameData data, DeterministicRng rng)
    {
        foreach (var card in data.Cards)
        {
            var target = card.Type switch
            {
                CardType.Action => state.ActionDeck,
                CardType.EpicTale => state.EpicDeck,
                _ => null, // Advantage/Reference cards are not shuffled into draw decks
            };
            if (target is null) continue;
            for (var i = 0; i < card.Count; i++) target.Add(card.Id);
        }

        rng.Shuffle(state.ActionDeck);
        rng.Shuffle(state.EpicDeck);
    }
}
