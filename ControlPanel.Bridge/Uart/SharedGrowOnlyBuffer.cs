using Nito.AsyncEx;

namespace ControlPanel.Bridge.Uart;

internal class SharedGrowOnlyBuffer(int initialSize = 8192)
{
    private readonly AsyncLock _sync = new();
    private readonly AsyncAutoResetEvent _newData = new ();

    private Memory<byte> _buffer = new byte[initialSize];
    private int _size;

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        using (await _sync.LockAsync(cancellationToken))
        {
            if (_size + data.Length >= _buffer.Length)
            {
                var size = _size + data.Length;
                var buffer = new Memory<byte>(new byte[size]);
                
                _buffer.CopyTo(buffer);
                data.CopyTo(_buffer[.._size]);
                
                _buffer =  buffer;
            }
            else
            {
                data.CopyTo(_buffer[_size..]);
                _size += data.Length;
            }
            
            _newData.Set();
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> data, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using (await _sync.LockAsync(cancellationToken))
            {
                if (_size > 0)
                {
                    var size = Math.Min(_size, data.Length);
                    _buffer[..size].CopyTo(data);
                    _buffer[..size].CopyTo(_buffer);
                    _size -= size;
                    return size;
                }
            }
            
            await _newData.WaitAsync(cancellationToken);
        }
        
        throw new OperationCanceledException();
    }
}