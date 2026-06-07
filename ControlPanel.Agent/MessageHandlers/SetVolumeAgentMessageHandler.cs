using ControlPanel.Agent.Shared;
using ControlPanel.AgentProtocol;
using Mediator;

namespace ControlPanel.Agent.MessageHandlers;

public class SetVolumeAgentMessageHandler(IAudioAgent audioAgent) : INotificationHandler<SetVolumeAgentMessage>
{
    public async ValueTask Handle(SetVolumeAgentMessage agentMessage, CancellationToken cancellationToken)
    {
        await audioAgent.SetVolumeAsync(agentMessage.Id, agentMessage.Volume, cancellationToken);
    }
}