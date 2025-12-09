using System.IO.Ports;

namespace ControlPanel.Bridge;

public sealed class UartClient : IUartClient
{
    private readonly SerialPort _port;
    private readonly Stream _stream;
    
    public UartClient(string device = "/dev/ttyUSB0", int baudRate = 115200)
    {
        _port = new SerialPort(device, baudRate)
        {
            Parity = Parity.None,
            DataBits = 8,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            Encoding = System.Text.Encoding.UTF8,
            ReadTimeout = -1,
            WriteTimeout = -1
        };

        _port.Open();
        _stream = _port.BaseStream;
    }
    
    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
        => await _stream.WriteAsync(data, ct);

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        => await _stream.ReadAsync(buffer, ct);

    public async ValueTask DisposeAsync()
    {
        await _stream.FlushAsync();
        _port.Dispose();
    }
}