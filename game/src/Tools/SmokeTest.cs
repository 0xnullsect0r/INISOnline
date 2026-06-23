using System.Collections.Generic;
using Godot;
using INISOnline.App;
using INISOnline.Game;
using INISOnline.Screens;
using Inis.Core.Model;
using Inis.Core.Rules;

namespace INISOnline.Tools;

/// <summary>
/// Headless validation (CI does not build the Godot project): run with
/// <c>godot --headless res://scenes/SmokeTest.tscn</c>. It plays a full AI game on the embedded
/// engine and instantiates every screen, printing <c>SMOKE …</c> lines and exiting. Any Godot
/// runtime error fails loudly in the output.
/// </summary>
public partial class SmokeTest : Node
{
    public override void _Ready()
    {
        var seats = new List<SeatConfig>();
        for (var i = 0; i < 4; i++)
            seats.Add(new SeatConfig($"p{i}", $"AI {i}",
                (ClanColor)i, IsAi: true));

        // 1) Engine integration — an all-AI game runs to completion through LocalGame.
        var game = new LocalGame(12345, seats);
        var steps = 0;
        while (!game.IsGameOver && steps < 5000) { game.StepAi(); steps++; }
        GD.Print($"SMOKE engine: gameOver={game.IsGameOver} steps={steps} winner={game.State.WinnerId}");

        // 2) UI integration — each screen instantiates and builds its tree without error.
        var screens = new ScreenManager();
        AddChild(screens);
        screens.Show(new MainMenu());
        screens.Show(new ModeSelect());
        screens.Show(new Screens.GameSetup(Screens.GameSetup.Mode.SinglePlayer));
        screens.Show(new GameHud(new LocalGame(1, seats)));
        GD.Print("SMOKE ui: screens instantiated");

        // 3) Board integration — the 2.5D board builds tiles + pieces from a live state.
        var boardGame = new LocalGame(7, seats);
        for (var i = 0; i < 60 && !boardGame.IsGameOver; i++) boardGame.StepAi();
        var board = new INISOnline.Board.BoardView();
        AddChild(board);
        board.Sync(boardGame.State);
        GD.Print($"SMOKE board: tiles synced for {boardGame.State.Territories.Count} territories");

        var timer = GetTree().CreateTimer(1.5);
        timer.Timeout += () =>
        {
            GD.Print("SMOKE done");
            GetTree().Quit();
        };
    }
}
