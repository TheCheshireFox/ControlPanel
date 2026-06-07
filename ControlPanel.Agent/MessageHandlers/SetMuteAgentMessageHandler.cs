using ControlPanel.Agent.Shared;
using ControlPanel.AgentProtocol;
using Mediator;

namespace ControlPanel.Agent.MessageHandlers;

public class SetMuteAgentMessageHandler(IAudioAgent audioAgent) : INotificationHandler<SetMuteAgentMessage>
{
    public async ValueTask Handle(SetMuteAgentMessage agentMessage, CancellationToken cancellationToken)
    {
        await audioAgent.ToggleMuteAsync(agentMessage.Id, agentMessage.Mute, cancellationToken);
    }
}