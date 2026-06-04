using System.IO.Ports;
using System.Text;
using ControlPanel.Bridge.Options;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Transport;

public sealed class TransportStream(Stream baseStream, IDisposable? parent = null) : Stream
{
    private bool _disposed;
    
    public Stream BaseStream { get; } = baseStream;

    public override bool CanRead => BaseStream.CanRead;
    public override bool CanSeek => BaseStream.CanSeek;
    public override bool CanTimeout => BaseStream.CanTimeout;
    public override bool CanWrite => BaseStream.CanWrite;
    public override long Length => BaseStream.Length;
    public override long Position
    {
        get => BaseStream.Position;
        set => BaseStream.Position = value;
    }

    public override int ReadTimeout
    {
        get => BaseStream.ReadTimeout;
        set => BaseStream.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => BaseStream.WriteTimeout;
        set => BaseStream.WriteTimeout = value;
    }

    public override void Flush() => BaseStream.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) => BaseStream.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => BaseStream.Read(buffer, offset, count);

    public override int Read(Span<byte> buffer) => BaseStream.Read(buffer);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        BaseStream.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        BaseStream.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);

    public override void SetLength(long value) => BaseStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => BaseStream.Write(buffer, offset, count);

    public override void Write(ReadOnlySpan<byte> buffer) => BaseStream.Write(buffer);

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        BaseStream.WriteAsync(buffer, offset, count, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        BaseStream.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            try
            {
                BaseStream.Dispose();
            }
            finally
            {
                parent?.Dispose();
            }
        }

        base.Dispose(disposing);
        
        _disposed = true;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        try
        {
            await BaseStream.DisposeAsync();
        }
        finally
        {
            if (parent is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else
                parent?.Dispose();
        }
    }
}

public interface ITransportStreamProvider
{
    Task<TransportStream> OpenStreamAsync(CancellationToken cancellationToken);
}

public class SerialPortTransportStreamProvider(IOptions<TransportOptions> options) : ITransportStreamProvider
{
    private readonly string _device = options.Value.Tty;
    private readonly int _baud = options.Value.BaudRate;

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
                WriteBufferSize = 8192,
                DtrEnable = true,
                RtsEnable = true
            };

            port.Open();

            return Task.FromResult(new TransportStream(port.BaseStream, port));
        }
        catch (Exception)
        {
            port?.Dispose();
            throw;
        }
    }
}
