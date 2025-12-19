using System.Net.WebSockets;

namespace ControlPanel.WebSocket;

public sealed class WebSocket : IWebSocket
{
    private readonly System.Net.WebSockets.WebSocket _webSocket;
    private readonly Func<Uri, CancellationToken, Task> _connect;

    public bool Connected => _webSocket.State == WebSocketState.Open;
    
    public WebSocket(ClientWebSocket webSocket)
    {
        _webSocket = webSocket;
        _connect = webSocket.ConnectAsync;
    }
    
    public WebSocket(System.Net.WebSockets.WebSocket webSocket)
    {
        _webSocket = webSocket;
        _connect = (_, _) => throw new NotSupportedException();
    }
    
    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) => _connect(uri, cancellationToken);

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken) => _webSocket.CloseAsync(closeStatus, statusDescription, cancellationToken);
    
    public Task SendAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => _webSocket.SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);

    public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>  _webSocket.ReceiveAsync(buffer, cancellationToken);

    public void Dispose() => _webSocket.Dispose();
}