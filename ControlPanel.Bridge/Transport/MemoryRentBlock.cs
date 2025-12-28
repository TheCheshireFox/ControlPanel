using System.Buffers;

namespace ControlPanel.Bridge.Transport;

internal sealed record MemoryRentBlock : IDisposable
{
    private readonly byte[] _rent;
    private bool _disposed;
        
    public Memory<byte> Data { get; init; }
        
    public MemoryRentBlock(int size)
    {
        _rent = ArrayPool<byte>.Shared.Rent(size);
        Data = new Memory<byte>(_rent, 0, size);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        ArrayPool<byte>.Shared.Return(_rent);
        _disposed = true;
    }
}