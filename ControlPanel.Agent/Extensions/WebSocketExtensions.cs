using System.Text;
using System.Text.Json;
using ControlPanel.WebSocket;

namespace ControlPanel.Agent.Extensions;

public static class WebSocketExtensions
{
    public static async Task SendJsonAsync<T>(this IWebSocket ws, T message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await ws.SendAsync(bytes, cancellationToken);
    }
}