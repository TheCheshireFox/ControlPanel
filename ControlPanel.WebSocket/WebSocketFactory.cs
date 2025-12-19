using System.Net.WebSockets;

namespace ControlPanel.WebSocket;

public class WebSocketFactory : IWebSocketFactory
{
    public IWebSocket Create() => new WebSocket(new ClientWebSocket());
    public IWebSocket Create(System.Net.WebSockets.WebSocket webSocket) => new WebSocket(webSocket);
}