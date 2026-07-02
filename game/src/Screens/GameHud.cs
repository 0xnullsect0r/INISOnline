using System.Linq;
using Godot;
using INISOnline.App;
using INISOnline.Audio;
using INISOnline.Board;
using INISOnline.Game;
using INISOnline.Theme;
using Inis.Core.Model;
using Inis.Core.Moves;

namespace INISOnline.Screens;

/// <summary>
/// The in-game HUD. It renders whatever <see cref="IGameSource"/> drives the game — the embedded
/// engine (offline/hotseat) or a server connection (online) — showing phase/turn, player banners,
/// action log and the 2.5D board, and presenting the pending player's legal moves as buttons. The
/// source is pumped each frame to advance AI locally or apply incoming server updates.
/// </summary>
public partial class GameHud : Screen
{
    private readonly IGameSource _source;

    private Label _phaseLabel = null!;
    private VBoxContainer _banners = null!;
    private Label _logLabel = null!;
    private ScrollContainer _logScroll = null!;
    private VBoxContainer _actions = null!;
    private BoardView _board = null!;
    private string? _selectedTerritory;
    private bool _renderedOnce;

    public GameHud(IGameSource source) => _source = source;

    public override void _Ready()
    {
        AddChild(Ui.Background());

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        AddChild(margin);

        var root = new VBoxContainer { ClipContents = true };
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var top = new PanelContainer();
        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 12);
        top.AddChild(topRow);
        _phaseLabel = Ui.Heading("Connecting…");
        _phaseLabel.HorizontalAlignment = HorizontalAlignment.Left;
        _phaseLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        topRow.AddChild(_phaseLabel);
        var gear = new Button { Text = "☰ Menu" };
        gear.Pressed += OpenGearMenu;
        topRow.AddChild(gear);
        root.AddChild(top);

        var middle = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        middle.AddThemeConstantOverride("separation", 12);
        root.AddChild(middle);

        middle.AddChild(BuildBannerPanel());
        middle.AddChild(BuildBoardPanel());
        middle.AddChild(BuildLogPanel());

        var actionPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 80) };
        actionPanel.ClipContents = true;
        var actionScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 60),
        };
        _actions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _actions.AddThemeConstantOverride("separation", 8);
        actionScroll.AddChild(_actions);
        actionPanel.AddChild(actionScroll);
        root.AddChild(actionPanel);

        if (_source.Ready) Refresh();
    }

    public override void _Process(double delta)
    {
        var changed = _source.Poll(delta);
        if (_source.Ready && (changed || !_renderedOnce)) Refresh();
    }

    private PanelContainer BuildBannerPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(280, 0) };
        panel.ClipContents = true;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);
        col.AddChild(Ui.Body("Players", Palette.Gold));
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _banners = new VBoxContainer();
        _banners.AddThemeConstantOverride("separation", 10);
        _banners.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_banners);
        col.AddChild(scroll);
        return panel;
    }

    private PanelContainer BuildBoardPanel()
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _board = new BoardView();
        _board.TerritoryPicked += OnTerritoryPicked;
        panel.AddChild(_board);
        return panel;
    }

    private PanelContainer BuildLogPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) };
        panel.ClipContents = true;
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);
        col.AddChild(Ui.Body("Action Log", Palette.Gold));

        _logScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _logLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _logLabel.AddThemeColorOverride("font_color", Palette.Cream);
        _logScroll.AddChild(_logLabel);
        col.AddChild(_logScroll);
        return panel;
    }

    private void OpenGearMenu() => AddChild(new MenuOverlay("Menu",
        ("Settings", () => AddChild(new SettingsPanel())),
        ("Debug Code", () => AddChild(new DebugPanel(_source, Refresh))),
        ("Leave Game", () => Nav.Show(new MainMenu()))));

    private void OnTerritoryPicked(string instanceId)
    {
        if (!_source.CanLocalAct) return;
        _selectedTerritory = _selectedTerritory == instanceId ? null : instanceId;
        _board.SetSelected(_selectedTerritory);
        RefreshActions();
    }

    private bool _wasGameOver;
    private bool _wasMyTurn;

    private void Refresh()
    {
        _renderedOnce = true;
        _board.Sync(_source.State);
        RefreshPhase();
        RefreshBanners();
        RefreshLog();
        RefreshActions();
        PlayCues();
    }

    private void PlayCues()
    {
        var audio = AudioManager.Instance;
        if (_source.IsGameOver && !_wasGameOver) audio?.PlaySfx("victory");
        else if (_source.CanLocalAct && !_wasMyTurn) audio?.PlaySfx("chime");
        _wasGameOver = _source.IsGameOver;
        _wasMyTurn = _source.CanLocalAct;
    }

    private void RefreshPhase()
    {
        var s = _source.State;
        _phaseLabel.Text = _source.IsGameOver
            ? $"Game over — {_source.StatusLine}"
            : $"Round {s.RoundNumber} · {s.Phase} · {_source.StatusLine}";
    }

    private void RefreshBanners()
    {
        foreach (var child in _banners.GetChildren()) child.QueueFree();

        var s = _source.State;
        foreach (var player in s.Players)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new ColorRect
            {
                Color = Palette.Clan(player.Color),
                CustomMinimumSize = new Vector2(18, 18),
            });

            var isCurrent = _source.Pending?.PlayerId == player.PlayerId;
            var marks = string.Concat(
                player == s.Brenn ? " ♚" : "",
                player.HasPretenderToken ? " ✦" : "");
            row.AddChild(Ui.Body(
                $"{_source.SeatName(player.PlayerId)}{marks}\nclans {player.ClanReserve} · deeds {player.Deeds} · hand {player.Hand.Count}",
                isCurrent ? Palette.GoldBright : Palette.Cream));
            _banners.AddChild(row);
        }
    }

    private void RefreshLog()
    {
        _logLabel.Text = string.Join("\n", _source.Log.TakeLast(40));
        CallDeferred(nameof(ScrollLogToEnd));
    }

    private void ScrollLogToEnd() =>
        _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;

    private void RefreshActions()
    {
        foreach (var child in _actions.GetChildren()) child.QueueFree();

        if (_source.IsGameOver)
        {
            var back = Ui.MenuButton("Back to Menu");
            back.Pressed += () => Nav.Show(new MainMenu());
            _actions.AddChild(back);
            return;
        }

        if (!_source.CanLocalAct)
        {
            _actions.AddChild(Ui.Body(_source.StatusLine, Palette.Muted));
            return;
        }

        var pendingName = _source.Pending is { } p ? _source.SeatName(p.PlayerId) : "";
        _actions.AddChild(Ui.Body($"{pendingName} — choose an action:", Palette.Gold));

        if (_selectedTerritory is not null)
        {
            var targetRow = new HBoxContainer();
            targetRow.AddThemeConstantOverride("separation", 8);
            targetRow.AddChild(Ui.Body($"Target: {_source.TerritoryName(_selectedTerritory)}", Palette.GoldBright));
            var clear = new Button { Text = "Clear" };
            clear.Pressed += () => { _selectedTerritory = null; _board.SetSelected(null); RefreshActions(); };
            targetRow.AddChild(clear);
            _actions.AddChild(targetRow);
        }

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        _actions.AddChild(flow);

        foreach (var move in _source.LegalMoves())
        {
            var captured = move;
            var button = new Button { Text = _source.Describe(move) };
            button.Pressed += () => Submit(captured);
            flow.AddChild(button);
        }
    }

    private void Submit(Move move)
    {
        if (move.Type == MoveType.PlayCard && _selectedTerritory is not null)
            move = move with { TerritoryId = _selectedTerritory };

        _source.Submit(move);
        _selectedTerritory = null;
        _board.SetSelected(null);
        if (_source.Ready) Refresh();
    }
}
