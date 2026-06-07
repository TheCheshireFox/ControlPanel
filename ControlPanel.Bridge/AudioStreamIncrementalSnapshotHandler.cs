using ControlPanel.Bridge.Audio;
using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Extensions;
using Mediator;

namespace ControlPanel.Bridge;

public class AudioStreamIncrementalSnapshotHandler(
    IDeviceConnection deviceConnection,
    IAudioStreamIconCache iconCache,
    ILogger<AudioStreamIncrementalSnapshotHandler> logger)
    : INotificationHandler<AudioStreamIncrementalSnapshot>
{
    public async ValueTask Handle(AudioStreamIncrementalSnapshot notification, CancellationToken cancellationToken)
    {
        if (notification.Deleted.Length == 0 && notification.Updated.Length == 0)
            return;

        CleanupIconCache(notification);
        
        var (updated, deleted) = notification.ToDeviceAudioStreams();
        var msg = new StreamsDeviceMessage(updated, deleted);
        
        logger.LogDebug("Sending streams, updated: {Updated}, deleted: {Deleted}", updated.Length, deleted.Length);

        try
        {
            await deviceConnection.SendMessageAsync(msg, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // NOP
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