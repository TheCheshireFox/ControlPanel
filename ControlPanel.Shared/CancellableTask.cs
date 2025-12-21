namespace ControlPanel.Shared;

public sealed class CancellableTask : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    
    public Task Task { get; }

    public CancellableTask(Func<CancellationToken, Task> taskFactory)
    {
        Task = Task.Run(() => taskFactory(_cts.Token));
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await Task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        
        _cts.Dispose();
        Task.Dispose();
    }
}