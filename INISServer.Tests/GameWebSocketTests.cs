using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Inis.Core.Model;
using Inis.Core.Moves;
using Inis.Core.Net;
using Xunit;

namespace INISServer.Tests;

/// <summary>
/// End-to-end: a human creates an AI-filled lobby, starts the game, connects the authoritative
/// WebSocket, and a scripted bot answers every TurnPrompt by echoing a legal move until the
/// game ends — exercising the full intent → engine.Apply → redacted-broadcast pipeline plus
/// AI seat auto-play.
/// </summary>
public sealed class GameWebSocketTests : IClassFixture<InisAppFactory>
{
    private readonly InisAppFactory _factory;
    public GameWebSocketTests(InisAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Bot_Plays_A_Full_Game_Against_AI_Over_The_Socket()
    {
        var client = _factory.CreateClient();
        var (_, token, _) = await ApiHelpers.RegisterAsync(client, "bot");

        // One human (seat 0) + two AI seats.
        var lobby = await ApiHelpers.PostJsonAsync(client, token, "/lobbies", new { capacity = 3 });
        var lobbyId = lobby.GetProperty("id").GetString();
        await ApiHelpers.PostJsonAsync(client, token, $"/lobbies/{lobbyId}/seats/1/ai", new { ai = true });
        await ApiHelpers.PostJsonAsync(client, token, $"/lobbies/{lobbyId}/seats/2/ai", new { ai = true });
        await ApiHelpers.PostJsonAsync(client, token, $"/lobbies/{lobbyId}/ready", new { ready = true });
        var started = await ApiHelpers.PostJsonAsync(client, token, $"/lobbies/{lobbyId}/start", new { });
        var gameId = started.GetProperty("gameId").GetString();

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var ws = await ConnectAsync(gameId!, token, cts.Token);

        var prompts = 0;
        var gameOver = false;
        var seq = 0;

        while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
        {
            var message = await ReceiveTextAsync(ws, cts.Token);
            if (message is null) break;

            using var doc = JsonDocument.Parse(message);
            var type = doc.RootElement.GetProperty("type").GetString();
            var payload = doc.RootElement.GetProperty("payload");

            if (type == Protocol.StateSync)
            {
                if (payload.GetProperty("Phase").GetString() == "GameOver") { gameOver = true; break; }
            }
            else if (type == Protocol.TurnPrompt)
            {
                prompts++;
                var legal = payload.GetProperty("legalMoves");
                if (legal.GetArrayLength() == 0) continue;

                var chosen = ChooseMove(legal);
                var intent = JsonSerializer.Serialize(new
                {
                    v = Protocol.Version,
                    type = Protocol.Intent,
                    seq = ++seq,
                    payload = chosen,
                }, InisJson.Options);
                await SendTextAsync(ws, intent, cts.Token);
            }
        }

        Assert.True(prompts > 0, "The bot was never prompted to act.");
        Assert.True(gameOver, $"Game did not reach GameOver (answered {prompts} prompts).");

        if (ws.State == WebSocketState.Open)
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
    }

    /// <summary>Prefer claiming victory when offered, else take the first legal move.</summary>
    private static Move ChooseMove(JsonElement legalMoves)
    {
        Move? first = null;
        foreach (var m in legalMoves.EnumerateArray())
        {
            var move = m.Deserialize<Move>(InisJson.Options)!;
            first ??= move;
            if (move.Type == MoveType.TakePretender) return move;
        }
        return first!;
    }

    private async Task<WebSocket> ConnectAsync(string gameId, string token, CancellationToken ct)
    {
        var wsClient = _factory.Server.CreateWebSocketClient();
        var uri = new UriBuilder(_factory.Server.BaseAddress)
        {
            Scheme = "ws",
            Path = $"/ws/game/{gameId}",
            Query = $"access_token={token}",
        }.Uri;
        return await wsClient.ConnectAsync(uri, ct);
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(buffer, ct);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            ms.Write(buffer, 0, result.Count);
        }
        while (!result.EndOfMessage);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static Task SendTextAsync(WebSocket ws, string text, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);
}
