using ControlPanel.Agent.Messaging;
using ControlPanel.Agent.Shared;
using ControlPanel.Protocol;
using ControlPanel.WebSocket;
using Mediator;

namespace ControlPanel.Agent.MessageHandlers;

public class GetIconAgentMessageHandler(IAudioAgent audioAgent, IWebSocket ws) : INotificationHandler<GetIconAgentMessage>
{
    public async ValueTask Handle(GetIconAgentMessage agentMessage, CancellationToken cancellationToken)
    {
        var icon = await audioAgent.GetAudioStreamIconAsync(agentMessage.Source, cancellationToken);
        var iconMessage = new AudioStreamIconAgentMessage(agentMessage.Source, icon.Icon, icon.IconHash);
        
        await ws.SendAsync(AgentMessageSerializer.Serialize(iconMessage), cancellationToken);
    }
}