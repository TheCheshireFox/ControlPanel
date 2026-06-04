using ControlPanel.Bridge.Device.DeviceProtocol;

namespace ControlPanel.Bridge.Extensions;

public static class AudioStreamIncrementalSnapshotExtensions
{
    public static (AudioStream[] Updated, Device.DeviceProtocol.AudioStreamId[] Deleted) ToDeviceAudioStreams(this AudioStreamIncrementalSnapshot snapshot)
    {
        var uartUpdated = snapshot.Updated
            .OrderBy(x => x.Name, StringComparer.InvariantCultureIgnoreCase)
            .Select(x => new AudioStream(new Device.DeviceProtocol.AudioStreamId(x.Id.Id, x.Id.AgentId), x.Source, x.Name, x.Mute, x.Volume, 0)) // TODO: calc icon hash
            .ToArray();

        var uartDeleted = snapshot.Deleted.Select(x => new Device.DeviceProtocol.AudioStreamId(x.Id.Id, x.Id.AgentId)).ToArray();
        
        return (uartUpdated, uartDeleted);
    }
}