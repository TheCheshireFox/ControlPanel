using ControlPanel.Protocol;

namespace ControlPanel.Bridge;

public record AudioStreamState(string Id, string AgentId, string Name, bool Mute, double Volume);

public interface IAudioStreamRepository
{
    Task UpdateAsync(BridgeAudioStream[] streams, CancellationToken cancellationToken);
    Task ClearAsync(string agentId, CancellationToken cancellationToken);
    Task<byte[]> GetRgb565A8IconAsync(string id, string agentId, CancellationToken cancellationToken);
    Task<AudioStreamState[]> GetAsync(bool onlyChanged, CancellationToken cancellationToken);
}

public class AudioStreamRepository : IAudioStreamRepository
{
    private readonly SemaphoreSlim _streamsLock = new(1, 1);
    private readonly Dictionary<string, Dictionary<string, AudioStreamState>> _streams = new();
    private readonly List<AudioStreamState> _changedStreams = [];
    private readonly Dictionary<(string Id, string AgentId), byte[]> _rgb565A8Icons = new();
    private bool _fullRefresh = true;

    public async Task UpdateAsync(BridgeAudioStream[] streams, CancellationToken cancellationToken)
    {
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            var keyStreams = streams
                .GroupBy(x => x.AgentId)
                .ToDictionary(x => x.Key, x => x.ToDictionary(y => y.Id, y => y));

            foreach (var agentId in keyStreams.Keys)
            {
                var agentStreams = _streams[agentId];
                var newAgentStreams = keyStreams[agentId];
                
                foreach (var id in agentStreams.Keys.Where(id => !newAgentStreams.ContainsKey(id)))
                {
                    agentStreams.Remove(id);
                    _fullRefresh = true;
                }

                foreach (var (id, stream) in newAgentStreams)
                {
                    if (agentStreams.TryGetValue(id, out var state))
                    {
                        if (state.Mute != stream.Mute || Math.Abs(state.Volume - stream.Volume) > 0.01 || state.Name != stream.Name)
                        {
                            agentStreams[id] = state with { Mute = stream.Mute, Volume = stream.Volume, Name = stream.Name };
                            _changedStreams.Add(state);
                        }
                        continue;
                    }
                    
                    var newState = new AudioStreamState(id, agentId, stream.Name, stream.Mute, stream.Volume);
                    agentStreams.Add(id, newState);
                    _rgb565A8Icons.Add((id, agentId), Rgb565A8Converter.Convert(stream.Icon.Name, stream.Icon.Icon));
                    _changedStreams.Add(newState);
                }
            }
        }
        finally
        {
            _streamsLock.Release();
        }
    }

    public async Task ClearAsync(string agentId, CancellationToken cancellationToken)
    {
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            _streams.Remove(agentId);
        }
        finally
        {
            _streamsLock.Release();
        }
    }

    public async Task<byte[]> GetRgb565A8IconAsync(string id, string agentId, CancellationToken cancellationToken)
    {
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            return _rgb565A8Icons.TryGetValue((id, agentId), out var icon) ? icon : [];
        }
        finally
        {
            _streamsLock.Release();
        }
    }
    
    public async Task<AudioStreamState[]> GetAsync(bool onlyChanged, CancellationToken cancellationToken)
    {
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            if (!onlyChanged || _fullRefresh)
                return _streams.Values.SelectMany(x => x.Values).ToArray();
            
            var result = _changedStreams.ToArray();
            _changedStreams.Clear();
            return result;
        }
        finally
        {
            _streamsLock.Release();
        }
    }
}