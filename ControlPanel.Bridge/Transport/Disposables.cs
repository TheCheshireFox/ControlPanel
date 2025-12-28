namespace ControlPanel.Bridge.Transport;

internal sealed class Disposables : IDisposable
{
    private IDisposable[]? _disposables;

    public Disposables(params IDisposable[] disposables)
    {
        _disposables = disposables;
    }

    public void Detach()
    {
        _disposables = null;
    }
    
    public void Dispose()
    {
        if (_disposables == null)
            return;
        
        foreach (var d in _disposables)
            d.Dispose();
        
        _disposables = null;
    }
}