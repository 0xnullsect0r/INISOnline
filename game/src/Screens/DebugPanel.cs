using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using INISOnline.App;
using INISOnline.Game;
using INISOnline.Theme;
using Inis.Core.Data;
using Inis.Core.Debug;
using Inis.Core.Model;

namespace INISOnline.Screens;

/// <summary>
/// The gated debug / cheat panel (docs/design.md). Reached from the in-game gear → "Debug Code";
/// entering <c>INIS</c> unlocks cheats that go through the same server-authoritative
/// <see cref="IGameSource.Debug"/> path as everything else, so they work — and sync — online.
/// </summary>
public partial class DebugPanel : Overlay
{
    private readonly IGameSource _source;
    private readonly Action _onChanged;
    private readonly GameData _data = GameData.Default;
    private bool _unlocked;

    public DebugPanel(IGameSource source, Action onChanged)
    {
        _source = source;
        _onChanged = onChanged;
    }

    protected override void Build()
    {
        foreach (var child in Body.GetChildren()) child.QueueFree();
        Body.AddChild(Ui.Heading("Debug"));

        if (!_unlocked)
        {
            Body.AddChild(Ui.Body("Enter the debug code to unlock cheats.", Palette.Muted));
            var code = Ui.Field("Debug code");
            Body.AddChild(code);
            var unlock = Ui.MenuButton("Unlock");
            unlock.Pressed += () =>
            {
                if (code.Text.Trim().ToUpperInvariant() == DebugCommandApi.UnlockCode)
                {
                    _unlocked = true;
                    Build();
                }
                else code.Text = "";
            };
            Body.AddChild(unlock);
            AddCloseButton();
            return;
        }

        BuildCheats();
        AddCloseButton();
    }

    private void BuildCheats()
    {
        // Grant a card to the acting player.
        var cards = _data.Cards
            .Where(c => c.Type is CardType.Action or CardType.EpicTale)
            .OrderBy(c => c.Type).ThenBy(c => c.Name).ToList();
        var picker = new OptionButton { CustomMinimumSize = new Vector2(380, 0) };
        foreach (var c in cards) picker.AddItem($"{(c.Type == CardType.EpicTale ? "Epic" : "Action")}: {c.Name}");

        Body.AddChild(Ui.Body("Grant a card to the current player", Palette.Gold));
        Body.AddChild(picker);
        var grant = Ui.MenuButton("Grant card");
        grant.Pressed += () =>
        {
            var idx = picker.Selected;
            if (idx >= 0 && idx < cards.Count) { _source.Debug("grant", cards[idx].Id, 0); _onChanged(); }
        };
        Body.AddChild(grant);

        // Set deeds.
        Body.AddChild(Ui.Body("Set deeds (wild victory tokens)", Palette.Gold));
        var deeds = new HSlider { MinValue = 0, MaxValue = 6, Step = 1, Value = 0, CustomMinimumSize = new Vector2(380, 0) };
        Body.AddChild(deeds);
        var setDeeds = Ui.MenuButton("Set deeds");
        setDeeds.Pressed += () => { _source.Debug("set_deeds", null, (int)deeds.Value); _onChanged(); };
        Body.AddChild(setDeeds);

        Body.AddChild(Ui.Body(CurrentHand(), Palette.Muted));
    }

    private string CurrentHand()
    {
        if (!_source.Ready || _source.Pending is not { } p) return "";
        var player = _source.State.Players.FirstOrDefault(x => x.PlayerId == p.PlayerId);
        if (player is null) return "";
        var names = player.Hand.Select(c => _data.TryGetCard(c, out var d) ? d.Name : c);
        return $"Current hand: {string.Join(", ", names)}";
    }
}
