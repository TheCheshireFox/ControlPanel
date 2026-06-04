namespace ControlPanel.Bridge.Transport;

public sealed class StreamConnection(Stream stream, IDisposable? owner = null) : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    public Stream Stream { get; } = stream;

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            Stream.Dispose();
        }
        finally
        {
            owner?.Dispose();
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await Stream.DisposeAsync();
        }
        finally
        {
            if (owner is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                owner?.Dispose();

            _disposed = true;
        }
    }
}

public interface IStreamConnector
{
    Task<StreamConnection> ConnectAsync(CancellationToken cancellationToken);
}