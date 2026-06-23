using Godot;
using INISOnline.Lan;
using Inis.Core.Model;
using Inis.Core.Moves;

namespace INISOnline.Tools;

/// <summary>
/// Headless loopback validation of LAN play: <c>godot --headless res://scenes/LanSmoke.tscn</c>.
/// Starts a <see cref="LanHost"/>, connects a <see cref="LanClientGame"/> peer to it over a real
/// (loopback) WebSocket, fills the rest with AI, and plays a full game to completion — proving the
/// in-client host serves the same protocol the online server does. Prints <c>LAN …</c> lines.
/// </summary>
public partial class LanSmoke : Node
{
    private LanHost _host = null!;
    private LanClientGame _client = null!;
    private int _frames;
    private int _moves;
    private bool _done;

    public override void _Ready()
    {
        _host = new LanHost();
        AddChild(_host);
        if (!_host.Open(3)) { GD.Print("LAN smoke: host open FAILED"); Quit(); return; }
        GD.Print($"LAN smoke: host listening on port {_host.Port}");
        _client = new LanClientGame($"ws://127.0.0.1:{_host.Port}", "Peer");
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        _frames++;
        _client.Poll(delta);

        if (!_host.Started && _host.SeatNames.Count > 0 && _host.SeatNames[0] is not null)
        {
            GD.Print("LAN smoke: peer joined seat 0 — starting game");
            _host.Start();
        }

        if (_host.Started && _client.CanLocalAct)
        {
            var legal = _client.LegalMoves();
            Move? pick = null;
            foreach (var m in legal) if (m.Type == MoveType.TakePretender) { pick = m; break; }
            pick ??= legal.Count > 0 ? legal[0] : null;
            if (pick is not null) { _client.Submit(pick); _moves++; }
        }

        if (_client.Ready && _client.IsGameOver)
        {
            GD.Print($"LAN play: gameOver=True movesSubmitted={_moves} winner={_client.State.WinnerId}");
            Quit();
            return;
        }
        if (_frames > 12000)
        {
            GD.Print($"LAN smoke: TIMEOUT ready={_client.Ready} started={_host.Started} moves={_moves}");
            Quit();
        }
    }

    private void Quit()
    {
        _done = true;
        GetTree().Quit();
    }
}
