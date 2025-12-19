using MessagePack;

namespace ControlPanel.Bridge.Protocol;

public class UartMessageSerializer
{
    public UartMessage Deserialize(byte[] data)
    {
        var message = MessagePackSerializer.Deserialize<dynamic>(data);
        var type = (UartMessageType)message["type"];
        return type switch
        {
            UartMessageType.Streams => MessagePackSerializer.Deserialize<UartStreamsMessage>(data),
            UartMessageType.SetVolume => MessagePackSerializer.Deserialize<UartSetVolumeMessage>(data),
            UartMessageType.SetMute => MessagePackSerializer.Deserialize<UartSetMuteMessage>(data),
            UartMessageType.GetIcon => MessagePackSerializer.Deserialize<UartGetIconMessage>(data),
            UartMessageType.RequestRefresh => MessagePackSerializer.Deserialize<UartRequestRefreshMessage>(data),
            _ => throw new Exception($"Unable to deserialize unknown message {type}")
        };
    }

    public byte[] Serialize<T>(T message) where T : UartMessage => MessagePackSerializer.Serialize(message);
}