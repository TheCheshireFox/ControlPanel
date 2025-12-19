namespace ControlPanel.WebSocket;

public interface IWebSocketFactory
{
    IWebSocket Create();
    IWebSocket Create(System.Net.WebSockets.WebSocket webSocket);
}