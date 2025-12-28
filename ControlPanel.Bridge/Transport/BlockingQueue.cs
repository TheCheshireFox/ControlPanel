using Nito.AsyncEx;

namespace ControlPanel.Bridge.Transport;

internal class BlockingQueue<T>
{
    private readonly LinkedList<T> _list = [];
    private readonly AsyncLock _lock = new();
    private readonly AsyncAutoResetEvent _evt = new();

    public async Task EnqueueAsync(T item, CancellationToken cancellationToken)
    {
        using (await _lock.LockAsync(cancellationToken))
        {
            _list.AddLast(item);
            _evt.Set();
        }
    }

    public async Task TakeOrReplaceAsync(Func<T, T?> process, CancellationToken cancellationToken)
    {
        while (true)
        {
            using (await _lock.LockAsync(cancellationToken))
            {
                if (_list.Count > 0)
                {
                    var result = process(_list.First!.Value);
                    if (result == null)
                    {
                        _list.RemoveFirst();
                    }
                    else
                    {
                        _list.First.Value = result;
                    }
                    
                    return;
                }
            }
            
            await _evt.WaitAsync(cancellationToken);
        }
    }
}