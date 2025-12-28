using System.IO.Ports;
using System.Text;
using ControlPanel.Bridge.Options;
using ControlPanel.Shared;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Transport;

public sealed class TransportStream(Stream stream, Action? onDispose = null) : IDisposable
{
    public Stream Stream { get; } = stream;

    public void Dispose()
    {
        Stream.Dispose();
        onDispose?.Invoke();
    }
}

public interface ITransportStreamProvider
{
    Task<TransportStream> OpenStreamAsync(CancellationToken cancellationToken);
}

public class SerialPortTransportStreamProvider : ITransportStreamProvider
{
    private readonly string _device;
    private readonly int _baud;
    
    public SerialPortTransportStreamProvider(IOptions<UartOptions> options)
    {
        _device = options.Value.Tty;
        _baud = options.Value.BaudRate;
    }
    
    public Task<TransportStream> OpenStreamAsync(CancellationToken cancellationToken)
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
                WriteTimeout = -1,
                ReadBufferSize = 8192,
                WriteBufferSize = 8192
            };

            port.Open();
            return Task.FromResult(new TransportStream(port.BaseStream, () => port.Dispose()));
        }
        catch (Exception)
        {
            port?.Dispose();
            throw;
        }
    }
}

public class BrRfcommTransportStreamProvider : ITransportStreamProvider
{
    private readonly string _addr;
    private readonly byte _channel;
    
    public BrRfcommTransportStreamProvider(IOptions<BtRfcommOptions> options)
    {
        _addr = options.Value.Address;
        _channel = options.Value.Channel;
    }
    
    public Task<TransportStream> OpenStreamAsync(CancellationToken cancellationToken)
    {
        var s = BtRfcomm.Connect(_addr, _channel, TimeSpan.FromSeconds(30), cancellationToken);
        return Task.FromResult(new TransportStream(s));
    }
}