using System.Text.Json;
using ControlPanel.Protocol;

namespace ControlPanel.Agent.Messaging;

public static class AgentMessageSerializer
{
    private static readonly Dictionary<AgentMessageType, Type> _types = new()
    {
        [AgentMessageType.AgentInit] = typeof(AgentInitMessage),
        [AgentMessageType.Streams] = typeof(StreamsMessage),
        [AgentMessageType.SetVolume] = typeof(SetVolumeMessage),
        [AgentMessageType.SetMute] = typeof(SetMuteMessage),
        [AgentMessageType.GetIcon] = typeof(GetIconMessage),
        [AgentMessageType.Icon] = typeof(AudioStreamIconMessage)
    };
    
    public static AgentMessage Deserialize(string rawMessage)
    {
        var message = JsonSerializer.Deserialize<AgentMessage>(rawMessage) ?? throw new JsonException("Unable to deserialize message");

        if (!_types.TryGetValue(message.Type, out var type))
            throw new Exception($"Unable to deserialize unknown message {message.Type}");

        return (AgentMessage?)JsonSerializer.Deserialize(rawMessage, type)
               ?? throw new JsonException($"Unable to deserialize message {message.Type}");
    }

    public static string Serialize(AgentMessage message)
    {
        return JsonSerializer.Serialize(message, _types[message.Type]);
    }
}