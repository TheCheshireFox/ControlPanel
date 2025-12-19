using System.Text.Json;

namespace ControlPanel.Protocol;

public static class BridgeMessageJsonSerializer
{
    public static BridgeMessage Deserialize(string line, JsonSerializerOptions? opts = null)
    {
        var message = JsonSerializer.Deserialize<BridgeMessage>(line, opts) ?? throw new JsonException("Unable to deserialize message");
        return message.Type switch
        {
            BridgeMessageType.Streams => JsonSerializer.Deserialize<StreamsMessage>(line, opts) ?? throw new JsonException($"Unable to deserialize message {message.Type}"),
            BridgeMessageType.SetVolume => JsonSerializer.Deserialize<SetVolumeMessage>(line, opts) ?? throw new JsonException($"Unable to deserialize message {message.Type}"),
            BridgeMessageType.SetMute => JsonSerializer.Deserialize<SetMuteMessage>(line, opts) ?? throw new JsonException($"Unable to deserialize message {message.Type}"),
            BridgeMessageType.GetIcon => JsonSerializer.Deserialize<GetIconMessage>(line, opts) ?? throw new JsonException($"Unable to deserialize message {message.Type}"),
            BridgeMessageType.Icon => JsonSerializer.Deserialize<AudioStreamIconMessage>(line, opts) ?? throw new JsonException($"Unable to deserialize message {message.Type}"),
            _ => throw new Exception($"Unable to deserialize unknown message {message.Type}")
        };
    }
}