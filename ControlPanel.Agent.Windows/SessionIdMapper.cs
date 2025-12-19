namespace ControlPanel.Agent.Windows;

internal class SessionIdMapper
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, string> _mapToSession = [];
    private readonly Dictionary<string, string> _sessionToMap = [];

    public async Task<string> GetMappedIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_sessionToMap.TryGetValue(sessionId, out var mappedId))
                return mappedId;
        
            mappedId = _sessionToMap[sessionId] = Guid.NewGuid().ToString("N");
            _mapToSession.Add(mappedId, sessionId);

            return mappedId;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> FindSessionIdAsync(string mapId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _mapToSession.GetValueOrDefault(mapId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RemoveAsync(string mapId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_mapToSession.Remove(mapId, out var sessionId))
                _sessionToMap.Remove(sessionId);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PruneAsync(IEnumerable<string> existingSessionIds, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var sessionsToPrune = _sessionToMap.Keys.Except(existingSessionIds).ToArray();
            foreach (var sessionId in sessionsToPrune)
            {
                if (_sessionToMap.Remove(sessionId, out var mapId))
                    _mapToSession.Remove(mapId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}