using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Extensions;

namespace ControlPanel.Bridge;

public class AudioStreamSnapshotService(
    IDeviceConnection deviceConnection,
    IAudioStreamRepository audioStreamRepository,
    IAudioStreamIconCache iconCache,
    ILogger<AudioStreamSnapshotService> logger)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        audioStreamRepository.OnSnapshotChangedAsync += OnStreamsUpdateAsync;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        audioStreamRepository.OnSnapshotChangedAsync -= OnStreamsUpdateAsync;
        return Task.CompletedTask;
    }
    
    private async Task OnStreamsUpdateAsync(AudioStreamIncrementalSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Deleted.Length == 0 && snapshot.Updated.Length == 0)
            return;

        CleanupIconCache(snapshot);
        
        var (updated, deleted) = snapshot.ToDeviceAudioStreams();
        var msg = new StreamsDeviceMessage(updated, deleted);
        
        logger.LogDebug("Sending streams, updated: {Updated}, deleted: {Deleted}", updated.Length, deleted.Length);

        try
        {
            await deviceConnection.SendMessageAsync(msg, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while sending streams");
        }
    }

    private void CleanupIconCache(AudioStreamIncrementalSnapshot snapshot)
    {
        foreach (var deleted in snapshot.Deleted)
            iconCache.RemoveIcon(deleted.Source, deleted.Id.AgentId);
    }
}