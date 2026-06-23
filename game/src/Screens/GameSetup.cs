using System;
using System.Collections.Generic;
using Godot;
using INISOnline.App;
using INISOnline.Game;
using INISOnline.Theme;
using Inis.Core.Model;
using Inis.Core.Rules;

namespace INISOnline.Screens;

/// <summary>
/// Offline / hotseat setup: pick the player count (2–5). Single-player seats you plus AI; hotseat
/// seats local humans. Start builds a <see cref="LocalGame"/> and opens the in-game HUD.
/// </summary>
public partial class GameSetup : Screen
{
    public enum Mode { SinglePlayer, Hotseat }

    private static readonly ClanColor[] Colors =
    {
        ClanColor.Red, ClanColor.Blue, ClanColor.Green, ClanColor.Yellow,
        ClanColor.White, ClanColor.Purple, ClanColor.Orange, ClanColor.Teal,
    };

    private readonly Mode _mode;
    private int _count = 4;
    private bool _seasons;
    private bool _extended;
    private Label _countLabel = null!;

    public GameSetup(Mode mode) => _mode = mode;

    private int MaxPlayers => _extended ? 8 : _seasons ? 5 : 4;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var column = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        column.SetAnchorsPreset(LayoutPreset.Center);
        column.AddThemeConstantOverride("separation", 18);
        AddChild(column);

        column.AddChild(Ui.Heading(_mode == Mode.SinglePlayer ? "Single-player" : "Local Hotseat"));

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        var minus = Ui.MenuButton("−", 72);
        minus.Pressed += () => SetCount(_count - 1);
        _countLabel = Ui.Heading($"{_count} players");
        _countLabel.CustomMinimumSize = new Vector2(220, 0);
        var plus = Ui.MenuButton("+", 72);
        plus.Pressed += () => SetCount(_count + 1);
        row.AddChild(minus);
        row.AddChild(_countLabel);
        row.AddChild(plus);
        column.AddChild(row);

        column.AddChild(Ui.Body(_mode == Mode.SinglePlayer
            ? "You play seat 1; the rest are AI opponents."
            : "All seats are local players sharing this device.", Palette.Muted));

        var seasons = new CheckButton { Text = "Seasons of Inis expansion (adds cards + 5th clan)", ButtonPressed = _seasons };
        seasons.Toggled += on =>
        {
            _seasons = on;
            SetCount(_count); // re-clamp to the new max
        };
        column.AddChild(seasons);

        var extended = new CheckButton { Text = "Extended 6–8 players (house-ruled)", ButtonPressed = _extended };
        extended.Toggled += on =>
        {
            _extended = on;
            SetCount(_count);
        };
        column.AddChild(extended);

        column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        var start = Ui.MenuButton("Start Game");
        start.Pressed += Start;
        column.AddChild(start);

        var back = Ui.MenuButton("Back");
        back.Pressed += () => Nav.Show(new ModeSelect());
        column.AddChild(back);
    }

    private void SetCount(int value)
    {
        _count = Math.Clamp(value, 2, MaxPlayers);
        _countLabel.Text = $"{_count} players";
    }

    private void Start()
    {
        var seats = new List<SeatConfig>(_count);
        for (var i = 0; i < _count; i++)
        {
            var isAi = _mode == Mode.SinglePlayer && i > 0;
            var name = isAi ? $"AI {i}" : _mode == Mode.SinglePlayer ? "You" : $"Player {i + 1}";
            seats.Add(new SeatConfig($"p{i}", name, Colors[i], isAi));
        }

        var seed = (int)(Time.GetUnixTimeFromSystem() % int.MaxValue);
        Nav.Show(new GameHud(new LocalGame(seed, seats, new GameOptions(_seasons, _extended))));
    }
}
