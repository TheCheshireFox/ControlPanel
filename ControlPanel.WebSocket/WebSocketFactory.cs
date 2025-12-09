using System.Net.WebSockets;

namespace ControlPanel.Agent.WebSocket;

public class WebSocketFactory : IWebSocketFactory
{
    public IWebSocket Create() => new WebSocket(new ClientWebSocket());
}