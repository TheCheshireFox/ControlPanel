using System.IO.Ports;
using System.Text;
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

    private readonly SharedGrowOnlyBuffer _toSerial = new();
    private readonly SharedGrowOnlyBuffer _fromSerial = new();
    private readonly CancellableTask _serialLoop;
    
    public UartFrameTransport(IOptions<UartOptions> options, ILogger<UartFrameTransport> logger)
    {
        _device = options.Value.Tty;
        _baud = options.Value.BaudRate;
        _reconnectInterval = options.Value.ReconnectInterval;
        _logger = logger;
        
        _serialLoop = new CancellableTask(SerialLoopAsync);
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _toSerial.WriteAsync(data, cancellationToken);
    }

    public async Task<int> ReadAsync(Memory<byte> data, CancellationToken cancellationToken)
    {
        return await _fromSerial.ReadAsync(data, cancellationToken);
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

                _logger.LogInformation("UART {Device} opened.", _device);

                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task[] tasks =
                [
                    Task.Run(async () => await ReadAsync(stream, cts.Token), cts.Token),
                    Task.Run(async () => await WriteAsync(stream, cts.Token), cts.Token),
                ];

                await Task.WhenAny(tasks);
                await cts.CancelAsync();
                await Task.WhenAll(tasks).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
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
        => await ReadWriteLoopAsync(stream.ReadAsync, _fromSerial.WriteAsync, cancellationToken);

    private async Task WriteAsync(Stream stream, CancellationToken cancellationToken)
        => await ReadWriteLoopAsync(_toSerial.ReadAsync, stream.WriteAsync, cancellationToken);

    private static async Task ReadWriteLoopAsync(Func<Memory<byte>, CancellationToken, ValueTask<int>> read, Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> write,
        CancellationToken cancellationToken)
    {
        Memory<byte> buffer = new byte[8192];
        while (!cancellationToken.IsCancellationRequested)
        {
            var count = await read(buffer, cancellationToken);
            await write(buffer[..count], cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _serialLoop.DisposeAsync();
    }
}