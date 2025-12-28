using System.Net.Sockets;

namespace ControlPanel.BtRfcomm;

public sealed class RfcommSocketStream : Stream
{
    private readonly Socket _socket;

    public RfcommSocketStream(Socket socket)
    {
        _socket = socket;
    }
    
    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    
    public override int ReadTimeout { get => _socket.ReceiveTimeout; set => _socket.ReceiveTimeout = value; }
    public override int WriteTimeout { get => _socket.SendTimeout; set => _socket.SendTimeout = value; }
    
    public override void Flush() { }
    
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    
    public override void SetLength(long value) => throw new NotSupportedException();
    
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => await _socket.ReceiveAsync(buffer, cancellationToken);
    
    public override int Read(byte[] buffer, int offset, int count) => Read(new Span<byte>(buffer, offset, count));
    public override int Read(Span<byte> buffer) => _socket.Receive(buffer);
    
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => await _socket.SendAsync(buffer, cancellationToken);
    
    public override void Write(byte[] buffer, int offset, int count) => Write(new ReadOnlySpan<byte>(buffer, offset, count));
    
    public override void Write(ReadOnlySpan<byte> buffer) => _socket.Send(buffer);
    
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _socket.Dispose();
    }
}