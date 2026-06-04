using ControlPanel.Agent.Messaging;
using ControlPanel.Agent.Shared;
using ControlPanel.Protocol;
using ControlPanel.WebSocket;
using Mediator;

namespace ControlPanel.Agent.MessageHandlers;

public class GetIconMessageHandler(IAudioAgent audioAgent, IWebSocket ws) : INotificationHandler<GetIconMessage>
{
    public async ValueTask Handle(GetIconMessage message, CancellationToken cancellationToken)
    {
        var icon = await audioAgent.GetAudioStreamIconAsync(message.Source, cancellationToken);
        var iconMessage = new AudioStreamIconMessage(message.Source, icon.Icon, icon.IconHash);
        
        await ws.SendAsync(AgentMessageSerializer.Serialize(iconMessage), cancellationToken);
    }
}