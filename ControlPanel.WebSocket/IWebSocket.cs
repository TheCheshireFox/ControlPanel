namespace ControlPanel.WebSocket;

public interface IWebSocket : IDisposable
{
    bool Connected { get; }
    
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task SendAsync(string message, CancellationToken cancellationToken);
    IAsyncEnumerable<string> ReceiveAsync(CancellationToken cancellationToken);
}