using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ControlPanel.WebSocket;

public interface ITextWebSocketClient
{
    Task SendAsync(string message, CancellationToken cancellationToken);
    IAsyncEnumerable<string> ReceiveAsync(CancellationToken cancellationToken);
}

public sealed class TextWebSocketClient : ITextWebSocketClient
{
    private readonly ILogger<TextWebSocketClient> _logger;
    private readonly IWebSocket _webSocket;

    public TextWebSocketClient(IWebSocket webSocket, ILogger<TextWebSocketClient> logger)
    {
        _webSocket = webSocket;
        _logger = logger;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        await _webSocket.SendAsync(Encoding.UTF8.GetBytes(message), cancellationToken);
    }

    public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
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

            var text = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(text))
                continue;

            yield return text;
        }
    }
}