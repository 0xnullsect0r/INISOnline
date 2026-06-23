using System.Text;
using System.Text.Json;
using Godot;
using INISOnline.Net;
using Inis.Core.Net;

namespace INISOnline.Lan;

/// <summary>
/// LAN transport for <see cref="WsGameSourceBase"/>: a Godot <see cref="WebSocketPeer"/> connected
/// to a host's <see cref="LanHost"/>. Godot's socket is polled on the main thread, so this pumps
/// it from <c>Poll</c>. On connect it sends a <c>Join</c> with the player's name to claim a seat
/// (LAN has no JWT); the host replies with Hello + a redacted StateSync.
/// </summary>
public sealed class LanClientGame : WsGameSourceBase
{
    private readonly WebSocketPeer _ws = new();
    private readonly string _joinName;
    private bool _joined;
    private bool _connecting = true;

    public LanClientGame(string url, string joinName)
    {
        _joinName = joinName;
        var err = _ws.ConnectToUrl(url);
        if (err != Error.Ok) ConnectStatus = $"Connect failed: {err}";
    }

    protected override void SendRaw(string json) => _ws.SendText(json);

    protected override void PumpTransport()
    {
        _ws.Poll();
        switch (_ws.GetReadyState())
        {
            case WebSocketPeer.State.Open:
                if (!_joined)
                {
                    _joined = true;
                    _connecting = false;
                    SendRaw(JsonSerializer.Serialize(new
                    {
                        v = Protocol.Version,
                        type = Protocol.Join,
                        payload = new { name = _joinName },
                    }, InisJson.Options));
                }
                while (_ws.GetAvailablePacketCount() > 0)
                    EnqueueIncoming(Encoding.UTF8.GetString(_ws.GetPacket()));
                break;
            case WebSocketPeer.State.Closed when !_connecting:
                ConnectStatus = "Disconnected";
                break;
        }
    }
}
