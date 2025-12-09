using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ControlPanel.WebSocket;

public class WebSocketMessageHandler
{
    private readonly ILogger<WebSocketMessageHandler> _logger;
    private readonly IWebSocket _webSocket;

    public WebSocketMessageHandler(IWebSocket webSocket, ILogger<WebSocketMessageHandler> logger)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    public async IAsyncEnumerable<string> HandleAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && _webSocket.Connected)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult? result;
            do
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("server closed websocket");
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellationToken);
                    yield break;
                }

                ms.Write(buffer, 0, result.Count);

            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(json))
                continue;

            yield return json;
        }
    }
}