using System.Text.Json;
using ControlPanel.WebSocket;

namespace ControlPanel.Agent.Extensions;

public static class WebSocketExtensions
{
    public static async Task SendJsonAsync<T>(this IWebSocket ws, T message, CancellationToken cancellationToken)
    {
        await ws.SendAsync(JsonSerializer.Serialize(message), cancellationToken);
    }
}