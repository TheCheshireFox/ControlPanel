using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Audio;

public interface IAudioStreamRepository
{
    Task UpdateAsync(string agentId, IEnumerable<AgentAudioStream> streams, CancellationToken cancellationToken);
    Task ClearAsync(string agentId, CancellationToken cancellationToken);
    Task<AudioStreamInfo[]> GetAllAsync(CancellationToken cancellationToken);
}

public class AudioStreamRepository(IMediator mediator) : IAudioStreamRepository
{
    private static readonly Comparer _comparer = new Comparer()
        .WithEqualityComparer<double>((x, y) => Math.Abs(x - y) < 0.01);

    private readonly SemaphoreSlim _streamsLock = new(1, 1);
    private readonly Dictionary<string, Dictionary<string, AudioStreamInfo>> _streams = new();

    public async Task UpdateAsync(string agentId, IEnumerable<AgentAudioStream> streams, CancellationToken cancellationToken)
    {
        var diff = new List<AudioStreamDiff>();
        var removed = new List<AudioStreamInfo>();
        
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            if (!_streams.TryGetValue(agentId, out var agentStreams))
                _streams[agentId] = agentStreams = [];

            var bridgeAgentStreams = streams.ToDictionary(x => x.Id, x => x);
                
            removed.AddRange(RemoveAgentStreams(agentStreams, bridgeAgentStreams));
            diff.AddRange(UpdateAgentStreams(agentId, agentStreams, bridgeAgentStreams));
        }
        finally
        {
            _streamsLock.Release();
        }

        await NotifyChangedAsync(diff, removed, cancellationToken);
    }

    private async Task NotifyChangedAsync(IReadOnlyCollection<AudioStreamDiff> changed, IReadOnlyCollection<AudioStreamInfo> removed, CancellationToken cancellationToken)
    {
        var snapshot = new AudioStreamIncrementalSnapshot(changed.ToArray(), removed.ToArray());
        await mediator.Publish(snapshot, cancellationToken);
    }
    
    public async Task ClearAsync(string agentId, CancellationToken cancellationToken)
    {
        var removed = new List<AudioStreamInfo>();
        
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            if (_streams.Remove(agentId, out var streams))
                removed.AddRange(streams.Values);
        }
        finally
        {
            _streamsLock.Release();
        }
        
        await NotifyChangedAsync([], removed, cancellationToken);
    }

    public async Task<AudioStreamInfo[]> GetAllAsync(CancellationToken cancellationToken)
    {
        await _streamsLock.WaitAsync(cancellationToken);
        try
        {
            return _streams.Values.SelectMany(x => x.Values).ToArray();
        }
        finally
        {
            _streamsLock.Release();
        }
    }

    private static List<AudioStreamDiff> UpdateAgentStreams(string agentId, Dictionary<string, AudioStreamInfo> agentStreams, Dictionary<string, AgentAudioStream> bridgeAudioStreams)
    {
        var diffs = new List<AudioStreamDiff>();
        
        foreach (var (id, stream) in bridgeAudioStreams)
        {
            if (agentStreams.TryGetValue(id, out var info))
            {
                if (TryGetAudioStreamDiff(info, stream, out var diff, out var updatedInfo))
                {
                    diffs.Add(diff);
                    agentStreams[id] = updatedInfo;
                }

                continue;
            }
                    
            var streamId = new AudioStreamId(id, agentId);
            var newInfo = AudioStreamInfo.FromStream(streamId, stream);
            var newDiff = new AudioStreamDiff(streamId, newInfo.Source, newInfo.Name, newInfo.Mute, newInfo.Volume, newInfo.IconHash);
            
            agentStreams.Add(id, newInfo);
            diffs.Add(newDiff);
        }
        
        return diffs;
    }

    private static bool TryGetAudioStreamDiff(AudioStreamInfo info, AgentAudioStream stream, out AudioStreamDiff diff, out AudioStreamInfo updatedInfo)
    {
        updatedInfo = null!;

        diff = new AudioStreamDiff(
            Id: info.Id,
            Source: info.Source,
            Name: _comparer.IsEquals(info.Name, stream.Name) ? null : stream.Name,
            Mute: _comparer.IsEquals(info.Mute, stream.Mute) ? null : stream.Mute,
            Volume: _comparer.IsEquals(info.Volume, stream.Volume) ? null : stream.Volume,
            IconHash: _comparer.IsEquals(info.IconHash, stream.IconHash) ? null : stream.IconHash);
        
        if (!diff.HasChanges)
            return false;

        updatedInfo = info with
        {
            Name = stream.Name,
            Mute = stream.Mute,
            Volume = stream.Volume,
            IconHash = stream.IconHash
        };

        return true;
    }
    
    private static List<AudioStreamInfo> RemoveAgentStreams(Dictionary<string,AudioStreamInfo> currentAgentStreams, Dictionary<string, AgentAudioStream> bridgeAudioStreams)
    {
        var removed = new List<AudioStreamInfo>();
        var removedIds = currentAgentStreams.Keys
            .Where(x => !bridgeAudioStreams.ContainsKey(x))
            .ToArray();
        
        foreach (var id in removedIds)
        {
            if (currentAgentStreams.Remove(id, out var streamInfo))
                removed.Add(streamInfo);
        }

        return removed;
    }
}