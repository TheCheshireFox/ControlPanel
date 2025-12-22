using System.IO.Ports;
using System.Text;
using System.Threading.Channels;
using ControlPanel.Bridge.Extensions;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using ControlPanel.Shared;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Uart;

public sealed class UartFrameTransport : IFrameTransport, IAsyncDisposable
{
    private readonly string _device;
    private readonly int _baud;
    private readonly TimeSpan _reconnectInterval;
    private readonly ILogger<UartFrameTransport> _logger;
    private readonly CancellableTask _serialLoop;

    private readonly BlockingQueue<MemoryRentBlock> _fromSerial = new();
    private readonly Channel<MemoryRentBlock> _toSerial = Channel.CreateUnbounded<MemoryRentBlock>(new UnboundedChannelOptions{ SingleReader = true });
    
    public event Func<CancellationToken, Task>? OnReconnectedAsync;
    
    public UartFrameTransport(IOptions<UartOptions> options, ILogger<UartFrameTransport> logger)
    {
        _device = options.Value.Tty;
        _baud = options.Value.BaudRate;
        _reconnectInterval = options.Value.ReconnectInterval;
        _logger = logger;
        
        _serialLoop = new CancellableTask(SerialLoopAsync);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var count = 0;
        
        await _fromSerial.TakeOrReplaceAsync(block =>
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
        await _toSerial.Writer.WriteAsync(block, cancellationToken);
        
        disposables.Detach();
    }
    
    private async Task SerialLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SerialPort? port = null;

            try
            {
                port = new SerialPort(_device, _baud)
                {
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    Encoding = Encoding.UTF8,
                    ReadTimeout = -1,
                    WriteTimeout = -1
                };
            
                port.Open();
                var stream = port.BaseStream;

                await OnReconnectedAsync.InvokeAllAsync(cancellationToken);
                
                _logger.LogInformation("UART {Device} opened.", _device);

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
                _logger.LogWarning(ex, "UART {Device} error.", _device);
            }

            port?.Dispose();
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

            await _fromSerial.EnqueueAsync(block, cancellationToken);
            
            disposables.Detach();
        }
    }

    private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
    {
        await foreach (var block in _toSerial.Reader.ReadAllAsync(cancellationToken))
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
        await _serialLoop.DisposeAsync();
    }
}