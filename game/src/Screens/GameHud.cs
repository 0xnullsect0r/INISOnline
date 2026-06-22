using System.Linq;
using Godot;
using INISOnline.App;
using INISOnline.Game;
using INISOnline.Theme;

namespace INISOnline.Screens;

/// <summary>
/// The in-game HUD for offline / hotseat play. It renders the authoritative engine state —
/// phase/turn, player banners, action log — and presents the pending player's legal moves as
/// buttons; AI seats auto-play on a timer. The 2.5D board view is layered in by a later chunk;
/// this proves the engine drives a full game through the UI.
/// </summary>
public partial class GameHud : Screen
{
    private readonly LocalGame _game;

    private Label _phaseLabel = null!;
    private VBoxContainer _banners = null!;
    private Label _logLabel = null!;
    private ScrollContainer _logScroll = null!;
    private VBoxContainer _actions = null!;
    private Timer _aiTimer = null!;

    public GameHud(LocalGame game) => _game = game;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        // Top bar: phase / whose turn.
        var top = new PanelContainer();
        _phaseLabel = Ui.Heading("");
        _phaseLabel.HorizontalAlignment = HorizontalAlignment.Left;
        top.AddChild(_phaseLabel);
        root.AddChild(top);

        // Middle: banners | board placeholder | log.
        var middle = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        middle.AddThemeConstantOverride("separation", 12);
        root.AddChild(middle);

        middle.AddChild(BuildBannerPanel());
        middle.AddChild(BuildBoardPlaceholder());
        middle.AddChild(BuildLogPanel());

        // Bottom: the pending player's actions.
        var actionPanel = new PanelContainer();
        _actions = new VBoxContainer();
        _actions.AddThemeConstantOverride("separation", 8);
        actionPanel.AddChild(_actions);
        root.AddChild(actionPanel);

        _aiTimer = new Timer { WaitTime = 0.25, Autostart = true };
        _aiTimer.Timeout += OnAiTick;
        AddChild(_aiTimer);

        Refresh();
    }

    private PanelContainer BuildBannerPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 0) };
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        col.AddChild(Ui.Body("Players", Palette.Gold));
        _banners = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _banners.AddThemeConstantOverride("separation", 10);
        col.AddChild(_banners);
        return panel;
    }

    private static PanelContainer BuildBoardPlaceholder()
    {
        var board = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var label = Ui.Body("Board view (2.5D) — added in the next chunk.", Palette.Muted);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        board.AddChild(label);
        return board;
    }

    private PanelContainer BuildLogPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) };
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);
        col.AddChild(Ui.Body("Action Log", Palette.Gold));

        _logScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        _logLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.AddThemeColorOverride("font_color", Palette.Cream);
        _logScroll.AddChild(_logLabel);
        col.AddChild(_logScroll);
        return panel;
    }

    private void OnAiTick()
    {
        if (_game.IsGameOver) { _aiTimer.Stop(); return; }
        if (_game.IsAiTurn)
        {
            _game.StepAi();
            Refresh();
        }
    }

    private void Refresh()
    {
        RefreshPhase();
        RefreshBanners();
        RefreshLog();
        RefreshActions();
    }

    private void RefreshPhase()
    {
        var s = _game.State;
        if (_game.IsGameOver)
        {
            _phaseLabel.Text = $"Game over — winner: {_game.SeatName(s.WinnerId ?? "?")}";
            return;
        }
        var turn = _game.Pending is { } p ? _game.SeatName(p.PlayerId) : "—";
        _phaseLabel.Text = $"Round {s.RoundNumber} · {s.Phase} · {turn}";
    }

    private void RefreshBanners()
    {
        foreach (var child in _banners.GetChildren()) child.QueueFree();

        var s = _game.State;
        foreach (var player in s.Players)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            row.AddChild(new ColorRect
            {
                Color = Palette.Clan(player.Color),
                CustomMinimumSize = new Vector2(18, 18),
            });

            var isCurrent = _game.Pending?.PlayerId == player.PlayerId;
            var marks = string.Concat(
                player == s.Brenn ? " ♚" : "",
                player.HasPretenderToken ? " ✦" : "");
            row.AddChild(Ui.Body(
                $"{_game.SeatName(player.PlayerId)}{marks}\nclans {player.ClanReserve} · deeds {player.Deeds} · hand {player.Hand.Count}",
                isCurrent ? Palette.GoldBright : Palette.Cream));
            _banners.AddChild(row);
        }
    }

    private void RefreshLog()
    {
        _logLabel.Text = string.Join("\n", _game.Log.TakeLast(40));
        CallDeferred(nameof(ScrollLogToEnd));
    }

    private void ScrollLogToEnd() =>
        _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;

    private void RefreshActions()
    {
        foreach (var child in _actions.GetChildren()) child.QueueFree();

        if (_game.IsGameOver)
        {
            var back = Ui.MenuButton("Back to Menu");
            back.Pressed += () => Nav.Show(new MainMenu());
            _actions.AddChild(back);
            return;
        }

        if (_game.IsAiTurn)
        {
            _actions.AddChild(Ui.Body("AI is thinking…", Palette.Muted));
            return;
        }

        var pendingName = _game.Pending is { } p ? _game.SeatName(p.PlayerId) : "";
        _actions.AddChild(Ui.Body($"{pendingName} — choose an action:", Palette.Gold));

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        _actions.AddChild(flow);

        foreach (var move in _game.LegalMoves())
        {
            var captured = move;
            var button = new Button { Text = _game.Describe(move) };
            button.Pressed += () =>
            {
                _game.Apply(captured);
                Refresh();
            };
            flow.AddChild(button);
        }
    }
}
