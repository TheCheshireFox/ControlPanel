using ControlPanel.Bridge.Audio;
using ControlPanel.Bridge.Device.DeviceProtocol;
using AudioStreamId = ControlPanel.Bridge.Audio.AudioStreamId;

namespace ControlPanel.Bridge.Extensions;

public static class AudioStreamIncrementalSnapshotExtensions
{
    public static (AudioStream[] Updated, Device.DeviceProtocol.AudioStreamId[] Deleted) ToDeviceAudioStreams(
        this AudioStreamIncrementalSnapshot snapshot)
    {
        var uartUpdated = snapshot.Updated
            .OrderBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(ToDevice)
            .ToArray();

        var uartDeleted = snapshot.Deleted.Select(x => new Device.DeviceProtocol.AudioStreamId(x.Id.Id, x.Id.AgentId))
            .ToArray();

        return (uartUpdated, uartDeleted);
    }
    
    private static Device.DeviceProtocol.AudioStreamId ToDevice(AudioStreamId id) => new(id.Id, id.AgentId);
    
    private static AudioStream ToDevice(AudioStreamDiff diff)
        => new(ToDevice(diff.Id), diff.Source, diff.Name, diff.Mute, diff.Volume, diff.IconHash);
}