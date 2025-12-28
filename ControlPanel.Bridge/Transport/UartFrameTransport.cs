using System.Threading.Channels;
using ControlPanel.Bridge.Extensions;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using ControlPanel.Shared;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Transport;

public sealed class UartFrameTransport : IFrameTransport, IAsyncDisposable
{
    private readonly ITransportStreamProvider _streamProvider;
    private readonly TimeSpan _reconnectInterval;
    private readonly ILogger<UartFrameTransport> _logger;
    private readonly CancellableTask _connectionLoop;

    private readonly BlockingQueue<MemoryRentBlock> _fromStream = new();
    private readonly Channel<MemoryRentBlock> _toStream = Channel.CreateUnbounded<MemoryRentBlock>(new UnboundedChannelOptions{ SingleReader = true });
    
    public event Func<CancellationToken, Task>? OnReconnectedAsync;
    
    public UartFrameTransport(IOptions<TransportOptions> options, ITransportStreamProvider streamProvider, ILogger<UartFrameTransport> logger)
    {
        _streamProvider = streamProvider;
        _logger = logger;
        _reconnectInterval = options.Value.ReconnectInterval;
        
        _connectionLoop = new CancellableTask(ConnectionLoopAsync);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = 0;
        
        await _fromStream.TakeOrReplaceAsync(block =>
        {
            count = Math.Min(buffer.Length, block.Data.Length);
            block.Data[..count].CopyTo(buffer);

            if (count > buffer.Length)
                return block with { Data = block.Data[count..] };
            
            block.Dispose();
            return null;

        }, cancellationToken);

        return count;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        var block = new MemoryRentBlock(buffer.Length);
        using var disposables = new Disposables(block);

        buffer.CopyTo(block.Data);
        await _toStream.Writer.WriteAsync(block, cancellationToken);
        
        disposables.Detach();
    }
    
    private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var transportStream = await _streamProvider.OpenStreamAsync(cancellationToken);
                var stream = transportStream.Stream;

                await OnReconnectedAsync.InvokeAllAsync(cancellationToken);
                
                _logger.LogInformation("Stream opened.");

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task[] tasks =
                [
                    Task.Run(async () => await ReadAsync(stream, cts.Token), cts.Token),
                    Task.Run(async () => await WriteAsync(stream, cts.Token), cts.Token),
                ];

                await Task.WhenAny(tasks);
                await cts.CancelAsync();
                await Task.WhenAll(tasks); // will throw
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Stream error.");
            }

            await Task.Delay(_reconnectInterval, cancellationToken);
        }
    }

    private async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var block = new MemoryRentBlock(2048);
            using var disposables = new Disposables(block);

            var read = await stream.ReadAsync(block.Data, cancellationToken);
            if (read <= 0)
                return;

            await _fromStream.EnqueueAsync(block with { Data = block.Data[..read] }, cancellationToken);
            
            disposables.Detach();
        }
    }

    private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await foreach (var block in _toStream.Reader.ReadAllAsync(cancellationToken))
        {
            using (block)
            {
                await stream.WriteAsync(block.Data, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionLoop.DisposeAsync();
    }
}