using ControlPanel.Bridge.Device.DeviceProtocol;
using MessagePack;

namespace ControlPanel.Bridge.Device.Messaging;

public static class DeviceMessageSerializer
{
    private static readonly Dictionary<MessageType, Type> _types = new()
    {
        [MessageType.Streams] = typeof(StreamsDeviceMessage),
        [MessageType.SetVolume] = typeof(SetVolumeDeviceMessage),
        [MessageType.SetMute] = typeof(SetMuteDeviceMessage),
        [MessageType.GetIcon] = typeof(GetIconDeviceMessage),
        [MessageType.Icon] = typeof(IconDeviceMessage),
        [MessageType.RequestRefresh] = typeof(RequestRefreshDeviceMessage)
    };
    
    public static DeviceMessage Deserialize(byte[] data)
    {
        var message = MessagePackSerializer.Deserialize<dynamic>(data);
        var messageType = (MessageType)message["type"];

        if (!_types.TryGetValue(messageType, out var type))
            throw new Exception($"Unable to deserialize unknown message {messageType}");

        return (DeviceMessage?)MessagePackSerializer.Deserialize(type, data)
            ?? throw new Exception($"Unable to deserialize message {messageType}");
    }

    public static byte[] Serialize(DeviceMessage message)
    {
        if (!_types.TryGetValue(message.Type, out var type))
            throw new Exception($"Unable to serialize unknown message {message.Type}");

        return MessagePackSerializer.Serialize(type, message);
    }
}
