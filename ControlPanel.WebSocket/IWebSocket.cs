using System.Net.WebSockets;

namespace ControlPanel.WebSocket;

public interface IWebSocket : IDisposable
{
    bool Connected { get; }
    
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken);
    Task SendAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
}