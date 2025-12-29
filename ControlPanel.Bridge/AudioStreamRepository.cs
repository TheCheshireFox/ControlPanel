using System.Diagnostics.CodeAnalysis;
using ControlPanel.Bridge.Extensions;
using ControlPanel.Protocol;

namespace ControlPanel.Bridge;

public class Comparer
{
    private static class ValueComparer<T>
    {
        public static IEqualityComparer<T> Instance = EqualityComparer<T>.Default;
    }

    private class DelegateEqualityComparer<T>(Func<T?, T?, bool> comparer) : IEqualityComparer<T>
    {
        public bool Equals(T? x, T? y) => comparer(x, y);
        public int GetHashCode([DisallowNull] T obj) => obj.GetHashCode();
    }
    
    public Comparer WithEqualityComparer<T>(Func<T?, T?, bool> comparer)
    {
        ValueComparer<T>.Instance = new DelegateEqualityComparer<T>(comparer);
        return this;
    }
    
    public bool IsEquals<T>(T x, T y) => ValueComparer<T>.Instance.Equals(x, y);
}

public record AudioStreamId(string Id, string AgentId);

public record AudioStreamInfo(AudioStreamId Id, string Source, string Name, bool Mute, double Volume)
{
    public static AudioStreamInfo FromStream(AudioStreamId streamId, BridgeAudioStream stream)
        => new(
            streamId,
            stream.Source,
            stream.Name,
            stream.Mute,
            stream.Volume
        );
}

public record AudioStreamDiff(AudioStreamId Id, string Source, string? Name, bool? Mute, double? Volume)
{
    public bool HasChanges => Name != null || Mute != null || Volume != null; 
    
    public static AudioStreamDiff FromStreamInfo(AudioStreamInfo streamInfo)
        => new(streamInfo.Id, streamInfo.Source, streamInfo.Name, streamInfo.Mute, streamInfo.Volume);
}

public record AudioStreamIncrementalSnapshot(AudioStreamDiff[] Updated, AudioStreamInfo[] Deleted);

public interface IAudioStreamRepository
{
    event Func<AudioStreamIncrementalSnapshot, CancellationToken, Task> OnSnapshotChangedAsync;
    
    Task UpdateAsync(string agentId, BridgeAudioStream[] streams, CancellationToken cancellationToken);
    Task ClearAsync(string agentId, CancellationToken cancellationToken);
    Task<AudioStreamInfo[]> GetAllAsync(CancellationToken cancellationToken);
}

public class AudioStreamRepository : IAudioStreamRepository
{
    private static readonly Comparer _comparer = new Comparer()
        .WithEqualityComparer<double>((x, y) => Math.Abs(x - y) < 0.01);
    
    private readonly ILogger<AudioStreamRepository> _logger;
    
    private readonly SemaphoreSlim _streamsLock = new(1, 1);
    private readonly Dictionary<string, Dictionary<string, AudioStreamInfo>> _streams = new();

    public AudioStreamRepository(ILogger<AudioStreamRepository> logger)
    {
        _logger = logger;
    }

    public event Func<AudioStreamIncrementalSnapshot, CancellationToken, Task>? OnSnapshotChangedAsync;

    public async Task UpdateAsync(string agentId, BridgeAudioStream[] streams, CancellationToken cancellationToken)
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
        if (OnSnapshotChangedAsync == null || (changed.Count == 0 && removed.Count == 0))
            return;
        
        var snapshot = new AudioStreamIncrementalSnapshot(changed.ToArray(), removed.ToArray());

        try
        {
            await OnSnapshotChangedAsync.InvokeAllAsync(snapshot, cancellationToken);
        }
        catch (Exception ex) when (ex is not TaskCanceledException and not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to notify about stream changes");
        }
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

    private static List<AudioStreamDiff> UpdateAgentStreams(string agentId, Dictionary<string, AudioStreamInfo> agentStreams, Dictionary<string, BridgeAudioStream> bridgeAudioStreams)
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
            var newDiff = new AudioStreamDiff(streamId, newInfo.Source, newInfo.Name, newInfo.Mute, newInfo.Volume);
            
            agentStreams.Add(id, newInfo);
            diffs.Add(newDiff);
        }
        
        return diffs;
    }

    private static bool TryGetAudioStreamDiff(AudioStreamInfo info, BridgeAudioStream stream, out AudioStreamDiff diff, out AudioStreamInfo updatedInfo)
    {
        updatedInfo = null!;

        diff = new AudioStreamDiff(
            Id: info.Id,
            Source: info.Source,
            Name: _comparer.IsEquals(info.Name, stream.Name) ? null : stream.Name,
            Mute: _comparer.IsEquals(info.Mute, stream.Mute) ? null : stream.Mute,
            Volume: _comparer.IsEquals(info.Volume, stream.Volume) ? null : stream.Volume);
        
        if (!diff.HasChanges)
            return false;

        updatedInfo = info with
        {
            Name = stream.Name,
            Mute = stream.Mute,
            Volume = stream.Volume
        };

        return true;
    }
    
    private static List<AudioStreamInfo> RemoveAgentStreams(Dictionary<string,AudioStreamInfo> currentAgentStreams, Dictionary<string, BridgeAudioStream> bridgeAudioStreams)
    {
        var removed = new List<AudioStreamInfo>();
        var removedIds = currentAgentStreams.Keys.Where(x => !bridgeAudioStreams.ContainsKey(x));
        
        foreach (var id in removedIds)
        {
            if (currentAgentStreams.Remove(id, out var streamInfo))
                removed.Add(streamInfo);
        }

        return removed;
    }
}