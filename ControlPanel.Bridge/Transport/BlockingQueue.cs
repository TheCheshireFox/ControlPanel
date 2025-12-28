using Nito.AsyncEx;

namespace ControlPanel.Bridge.Transport;

internal class BlockingQueue<T>
{
    private readonly LinkedList<T> _list = [];
    private readonly AsyncMonitor _monitor = new();

    public async Task EnqueueAsync(T item, CancellationToken cancellationToken)
    {
        using (await _monitor.EnterAsync(cancellationToken))
        {
            _list.AddLast(item);
            _monitor.Pulse();
        }
    }

    public async Task TakeOrReplaceAsync(Func<T, T?> process, CancellationToken cancellationToken)
    {
        using (await _monitor.EnterAsync(cancellationToken))
        {
            if (_list.Count == 0)
                await _monitor.WaitAsync(cancellationToken);
                
            var result = process(_list.First!.Value);
            if (result == null)
            {
                _list.RemoveFirst();
            }
            else
            {
                _list.First.Value = result;
            }
        }
    }
}