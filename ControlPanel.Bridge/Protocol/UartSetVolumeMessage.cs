using MessagePack;

namespace ControlPanel.Bridge.Protocol;

[MessagePackObject(true)]
public record UartSetVolumeMessage(
    [property: Key("id")] UartAudioStreamId Id, 
    [property: Key("volume")] double Volume)
    : UartMessage(UartMessageType.SetVolume);