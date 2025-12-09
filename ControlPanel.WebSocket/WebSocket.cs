using System.Net.WebSockets;

namespace ControlPanel.Agent.WebSocket;

public sealed class WebSocket : IWebSocket
{
    private readonly ClientWebSocket _webSocket;

    public bool Connected => _webSocket.State == WebSocketState.Open;
    
    public WebSocket(ClientWebSocket webSocket)
    {
        _webSocket = webSocket;
    }
    
    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => _webSocket.ConnectAsync(uri, cancellationToken);

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) => _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
    
    public Task SendAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>  _webSocket.ReceiveAsync(buffer, cancellationToken);

    public void Dispose() => _webSocket.Dispose();
}