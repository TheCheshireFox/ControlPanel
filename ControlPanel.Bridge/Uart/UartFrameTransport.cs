using System.IO.Ports;
using System.Text;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;

namespace ControlPanel.Bridge.Uart;

public sealed class UartFrameTransport : IFrameTransport, IDisposable
{
    private readonly string _device;
    private readonly int _baud;
    private readonly int _reconnectInterval;
    private readonly ILogger<UartFrameTransport> _logger;
    
    private readonly AsyncLock _portLock = new();
    private readonly AsyncLock _readLock = new();
    private readonly AsyncLock _writeLock = new();
    private SerialPort? _port;
    
    public UartFrameTransport(IOptions<UartOptions> options, ILogger<UartFrameTransport> logger)
    {
        _device = options.Value.Tty;
        _baud = options.Value.BaudRate;
        _reconnectInterval = options.Value.ReconnectInterval;
        _logger = logger;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await WithStreamAsync(async s =>
        {
            using var @lock = await _writeLock.LockAsync(cancellationToken);
            await s.WriteAsync(data, cancellationToken);
            await s.FlushAsync(cancellationToken);
            return 0;
        }, cancellationToken);
    }

    public async Task<int> ReadAsync(Memory<byte> data, CancellationToken cancellationToken)
    {
        return await WithStreamAsync(async s =>
        {
            using var @lock = await _readLock.LockAsync(cancellationToken);
            return await s.ReadAsync(data, cancellationToken);
        }, cancellationToken);
    }

    private async Task<T> WithStreamAsync<T>(Func<Stream, Task<T>> func, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var stream = await GetStreamAsync(cancellationToken);
                return await func(stream);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "UART stream error.");
                
                using (await _portLock.LockAsync(cancellationToken))
                {
                    if (_port?.IsOpen is not true)
                        _port = null;
                }
            }
        }
        
        throw new OperationCanceledException();
    }
    
    private async Task<Stream> GetStreamAsync(CancellationToken cancellationToken)
    {
        using (await _portLock.LockAsync(cancellationToken))
        {
            if (_port?.IsOpen ?? false)
                return _port.BaseStream;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    _port = new SerialPort(_device, _baud)
                    {
                        Parity = Parity.None,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        Handshake = Handshake.None,
                        Encoding = Encoding.UTF8,
                        ReadTimeout = -1,
                        WriteTimeout = -1
                    };
            
                    _port.Open();
            
                    _logger.LogInformation("UART {Device} opened.", _device);
            
                    return _port.BaseStream;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "UART {Device} error.", _device);
                }

                await Task.Delay(_reconnectInterval, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
            
            throw new OperationCanceledException();
        }
    }

    public void Dispose()
    {
        _port?.Dispose();
    }
}