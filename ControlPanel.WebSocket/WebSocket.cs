using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

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
    
    public async Task SendAsync(string message, CancellationToken cancellationToken)
    {
        await _webSocket.SendAsync(Encoding.UTF8.GetBytes(message), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async IAsyncEnumerable<string> ReceiveAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && Connected)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult? result;
            do
            {
                result = await _webSocket.ReceiveAsync(buffer, cancellationToken);

                switch (result.MessageType)
                {
                    case WebSocketMessageType.Text:
                        ms.Write(buffer, 0, result.Count);
                        break;
                    case WebSocketMessageType.Close:
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cancellationToken);
                        yield break;
                    case WebSocketMessageType.Binary:
                        throw new Exception("Unexpected binary message");
                }

            } while (!result.EndOfMessage);

            var text = Encoding.UTF8.GetString(ms.ToArray());
            if (string.IsNullOrWhiteSpace(text))
                continue;

            yield return text;
        }
    }

    public void Dispose() => _webSocket.Dispose();
}