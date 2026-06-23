using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace INISOnline.Net;

/// <summary>
/// Online transport for <see cref="WsGameSourceBase"/>: a background <see cref="ClientWebSocket"/>
/// to the authoritative server (<c>wss://…/ws/game/{id}?access_token=…</c>). The receive loop only
/// enqueues frames; the base applies them on the main thread. Drops auto-reconnect — the server
/// replays a full StateSync on connect.
/// </summary>
public sealed class RemoteGame : WsGameSourceBase, IDisposable
{
    private readonly Uri _uri;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentQueue<string> _outgoing = new();
    private ClientWebSocket? _ws;

    public RemoteGame(string gameId)
    {
        _uri = new Uri($"{Session.WebSocketBase}/ws/game/{gameId}?access_token={Session.AccessToken}");
        _ = Task.Run(ReceiveLoopAsync);
    }

    protected override void SendRaw(string json)
    {
        _outgoing.Enqueue(json);
        _ = FlushAsync();
    }

    private async Task ReceiveLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                _ws = new ClientWebSocket();
                await _ws.ConnectAsync(_uri, _cts.Token);
                ConnectStatus = "Connected";
                await FlushAsync();

                var buffer = new byte[32 * 1024];
                using var ms = new MemoryStream();
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(buffer, _cts.Token);
                        if (result.MessageType == WebSocketMessageType.Close) break;
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                    EnqueueIncoming(Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length));
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception) { ConnectStatus = "Reconnecting…"; }

            if (_cts.IsCancellationRequested) return;
            try { await Task.Delay(1000, _cts.Token); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task FlushAsync()
    {
        if (_ws is not { State: WebSocketState.Open }) return;
        await _sendGate.WaitAsync(_cts.Token);
        try
        {
            while (_outgoing.TryDequeue(out var msg))
                await _ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, _cts.Token);
        }
        catch (Exception) { /* surfaced via reconnect */ }
        finally { _sendGate.Release(); }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _ws?.Dispose(); } catch { /* ignore */ }
        _cts.Dispose();
    }
}
