using ControlPanel.WebSocket;
using Microsoft.Extensions.Logging;

namespace ControlPanel.Agent.WebSocket;

public interface ITextWebSocketClientFactory
{
    ITextWebSocketClient Create(IWebSocket socket);
}

public class TextWebSocketClientFactory : ITextWebSocketClientFactory
{
    private readonly ILogger<TextWebSocketClient> _logger;

    public TextWebSocketClientFactory(ILogger<TextWebSocketClient> logger)
    {
        _logger = logger;
    }

    public ITextWebSocketClient Create(IWebSocket socket) => new TextWebSocketClient(socket, _logger);
}