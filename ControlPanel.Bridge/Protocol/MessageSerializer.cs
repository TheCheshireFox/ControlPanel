using MessagePack;

namespace ControlPanel.Bridge.Protocol;

public class MessageSerializer
{
    public static Message Deserialize(byte[] data)
    {
        var message = MessagePackSerializer.Deserialize<dynamic>(data);
        var type = (MessageType)message["type"];
        return type switch
        {
            MessageType.Streams => MessagePackSerializer.Deserialize<StreamsMessage>(data),
            MessageType.SetVolume => MessagePackSerializer.Deserialize<SetVolumeMessage>(data),
            MessageType.SetMute => MessagePackSerializer.Deserialize<SetMuteMessage>(data),
            MessageType.GetIcon => MessagePackSerializer.Deserialize<GetIconMessage>(data),
            MessageType.RequestRefresh => MessagePackSerializer.Deserialize<RequestRefreshMessage>(data),
            MessageType.Log => MessagePackSerializer.Deserialize<LogMessage>(data),
            MessageType.TextRendererParameters => MessagePackSerializer.Deserialize<TextRendererParametersMessage>(data),
            _ => throw new Exception($"Unable to deserialize unknown message {type}")
        };
    }

    public static byte[] Serialize<T>(T message) where T : Message => MessagePackSerializer.Serialize(message);
}