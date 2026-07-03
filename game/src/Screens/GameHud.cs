using System.Collections.Generic;
using System.Linq;
using Godot;
using INISOnline.App;
using INISOnline.Audio;
using INISOnline.Board;
using INISOnline.Game;
using INISOnline.Theme;
using Inis.Core.Data;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Rules;

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
    private PanelContainer _actionPanel = null!;
    private BoardView _board = null!;
    private string? _selectedTerritory;
    private bool _renderedOnce;
    private readonly GameData _data = GameData.Default;

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

        _actionPanel = new PanelContainer { CustomMinimumSize = new Vector2(0, 80) };
        _actionPanel.ClipContents = true;
        var actionScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _actions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _actions.AddThemeConstantOverride("separation", 8);
        actionScroll.AddChild(_actions);
        _actionPanel.AddChild(actionScroll);
        root.AddChild(_actionPanel);

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
        col.AddChild(Ui.Body(_source.SupportsChat ? "Log & Chat" : "Action Log", Palette.Gold));

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

        if (_source.SupportsChat)
        {
            var chat = new LineEdit { PlaceholderText = "Say something…" };
            chat.TextSubmitted += text =>
            {
                _source.SendChat(text);
                chat.Text = "";
            };
            col.AddChild(chat);
        }
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
        var season = s.CurrentSeason is { } w ? $" · {w}" : "";
        _phaseLabel.Text = _source.IsGameOver
            ? $"Game over — {_source.StatusLine}"
            : $"Round {s.RoundNumber} · {s.Phase}{season} · {_source.StatusLine}";
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
            var progress = VictoryEvaluator.Evaluate(s, player);
            var pips = string.Join(" · ", progress.Select(x =>
                $"{x.Condition.ToString()[..4]} {x.Value}/{x.Threshold}{(x.Met ? "✓" : "")}"));
            row.AddChild(Ui.Body(
                $"{_source.SeatName(player.PlayerId)}{marks}\n" +
                $"clans {player.ClanReserve} · deeds {player.Deeds} · hand {player.Hand.Count}\n{pips}",
                isCurrent ? Palette.GoldBright : Palette.Cream));
            _banners.AddChild(row);
        }

        if (s.ActiveClash is { } clash) _banners.AddChild(BuildClashPanel(s, clash));
    }

    /// <summary>A compact combat summary shown under the player list while a clash runs.</summary>
    private Control BuildClashPanel(GameState s, ClashState clash)
    {
        var panel = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 4);
        panel.AddChild(col);

        var step = clash.InResolution ? "Resolution" : "Citadels";
        col.AddChild(Ui.Body($"⚔ Clash — {_source.TerritoryName(clash.TerritoryId)} ({step})", Palette.Danger));
        var territory = s.Territories[clash.TerritoryId];
        foreach (var p in s.Players)
        {
            var total = territory.ClansOf(p.Color);
            if (total <= 0) continue;
            var sheltered = clash.Sheltered.GetValueOrDefault(p.Color);
            var notes = string.Concat(
                clash.InstigatorId == p.PlayerId ? " (instigator)" : "",
                clash.AgreedToEnd.Contains(p.PlayerId) ? " · wants peace" : "",
                clash.CoalitionPlayerIds.Contains(p.PlayerId) ? " · coalition" : "");
            col.AddChild(Ui.Body(
                $"{_source.SeatName(p.PlayerId)}: {total - sheltered} exposed" +
                (sheltered > 0 ? $", {sheltered} sheltered" : "") + notes,
                Palette.Cream));
        }
        if (clash.TriskelsBlocked) col.AddChild(Ui.Body("Lug's Spear: no Triskels", Palette.Muted));
        return panel;
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

        var pending = _source.Pending;
        var pendingName = pending is { } p ? _source.SeatName(p.PlayerId) : "";
        if (pending?.Kind == PendingKind.Reaction)
        {
            var trigger = pending.Trigger ?? "a trigger";
            var about = pending.CardId is { } tc && _data.TryGetCard(tc, out var td) ? $" — {td.Name}" : "";
            _actions.AddChild(Ui.Body($"⚡ {pendingName} may react to {trigger}{about}:", Palette.GoldBright));
        }
        else
        {
            _actions.AddChild(Ui.Body($"{pendingName} — choose an action:", Palette.Gold));
        }

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

        // Card-shaped moves render as a hand dock of card art; everything else stays a button.
        var legal = _source.LegalMoves();
        var cardMoves = legal.Where(IsCardMove).ToList();
        var buttonMoves = legal.Where(m => !IsCardMove(m));

        if (cardMoves.Count > 0)
        {
            var dock = new HFlowContainer();
            dock.AddThemeConstantOverride("h_separation", 10);
            dock.AddThemeConstantOverride("v_separation", 10);
            _actions.AddChild(dock);
            foreach (var move in cardMoves)
            {
                var captured = move;
                _data.TryGetCard(move.CardId!, out var def);
                var caption = move.Type switch
                {
                    MoveType.DraftPick => "Draft",
                    MoveType.PlayReaction => "React",
                    _ => "Play",
                };
                var widget = new CardWidget(def!, caption);
                widget.Pressed += () => Submit(captured);
                dock.AddChild(widget);
            }
        }

        var flow = new HFlowContainer();
        flow.AddThemeConstantOverride("h_separation", 8);
        flow.AddThemeConstantOverride("v_separation", 8);
        _actions.AddChild(flow);

        foreach (var move in buttonMoves)
        {
            var captured = move;
            var button = new Button { Text = _source.Describe(move) };
            button.Pressed += () => Submit(captured);
            flow.AddChild(button);
        }

        _actionPanel.CustomMinimumSize = new Vector2(0, cardMoves.Count > 0 ? 250 : 80);
    }

    /// <summary>A legal move rendered as a card in the dock: it names a real, known card.</summary>
    private bool IsCardMove(Move m) =>
        m.Type is MoveType.PlayCard or MoveType.DraftPick or MoveType.PlayReaction
        && m.CardId is { } cid && _data.TryGetCard(cid, out _);

    private void Submit(Move move)
    {
        if (move.Type is MoveType.PlayCard or MoveType.PlayReaction && _selectedTerritory is not null)
            move = move with { TerritoryId = _selectedTerritory };

        _source.Submit(move);
        _selectedTerritory = null;
        _board.SetSelected(null);
        if (_source.Ready) Refresh();
    }
}
