using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace ControlPanel.Bridge.Transport;

internal sealed class TransportStreamHolder(
    ITransportStreamProvider streamProvider,
    TimeSpan reconnectInterval,
    ILogger logger) : IAsyncDisposable
{
    private readonly AsyncLock _connectionLock = new();
    private readonly CancellationTokenSource _cts = new();
    private Task _reconnectingTask = Task.CompletedTask;
    private TransportStream? _stream;

    public async Task<TransportStream> GetStreamAsync(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        
        using (await _connectionLock.LockAsync(cts.Token))
        {
            if (_stream != null)
                return _stream;

            if (_reconnectingTask.IsCompleted)
                _reconnectingTask = ReconnectInternalAsync(cts.Token);

            await _reconnectingTask;

            return _stream ?? throw new InvalidOperationException("Stream not available");
        }
    }

    public void SetDisconnected(CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
        
        using (_connectionLock.Lock(cts.Token))
        {
            _stream?.Dispose();
            _stream = null;
        }
    }

    private async Task ReconnectInternalAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                logger.LogInformation("Opening stream...");

                _stream = await streamProvider.OpenStreamAsync(cancellationToken);

                logger.LogInformation("Stream opened.");
                
                return;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Stream error.");
                await Task.Delay(reconnectInterval, cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts.IsCancellationRequested)
            return;
        
        await _cts.CancelAsync();
        await _reconnectingTask.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        
        if (_stream != null)
            await _stream.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }
}

public sealed class FrameTransport(
    IOptions<TransportOptions> options,
    ITransportStreamProvider streamProvider,
    ILogger<FrameTransport> logger)
    : IFrameTransport, IAsyncDisposable
{
    private readonly AsyncLock _readLock = new();
    private readonly AsyncLock _writeLock = new();
    private readonly TransportStreamHolder _streamHolder = new(streamProvider, options.Value.ReconnectInterval, logger);

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return await DoStreamAsync(async s =>
        {
            using (await _readLock.LockAsync(cancellationToken))
                return await s.ReadAsync(buffer, cancellationToken);
        }, cancellationToken);
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        await DoStreamAsync(async s =>
        {
            using (await _writeLock.LockAsync(cancellationToken))
                await s.WriteAsync(buffer, cancellationToken);
        }, cancellationToken);
    }

    private async ValueTask<T> DoStreamAsync<T>(Func<Stream, ValueTask<T>> action, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stream = await _streamHolder.GetStreamAsync(cancellationToken);
                return await action(stream);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Stream error.");
                _streamHolder.SetDisconnected(cancellationToken);
            }
        }
        
        throw new OperationCanceledException();
    }

    private async ValueTask DoStreamAsync(Func<Stream, ValueTask> action, CancellationToken cancellationToken) =>
        await DoStreamAsync(async s => { await action(s); return 0; }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        await _streamHolder.DisposeAsync();
    }
}