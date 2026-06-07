namespace ControlPanel.Agent.Windows;

internal class SessionIdMapper
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<long, string> _mapToSession = [];
    private readonly Dictionary<string, long> _sessionToMap = [];

    private long _nextId = 0;
    
    public async Task<string> GetMappedIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_sessionToMap.TryGetValue(sessionId, out var mappedId))
                return mappedId.ToString();
        
            mappedId = _sessionToMap[sessionId] = _nextId++;
            _mapToSession.Add(mappedId, sessionId);

            return mappedId.ToString();
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
            return long.TryParse(mapId, out var value)
                ? _mapToSession.GetValueOrDefault(value)
                : null;
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
            if (!long.TryParse(mapId, out var value))
                return;
            
            if (_mapToSession.Remove(value, out var sessionId))
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