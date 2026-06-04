using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Agent.MessageHandlers;

public class AgentInitMessageHandler(IAgentAppIconProvider agentAppIconProvider) : INotificationHandler<AgentInitMessage>
{
    public ValueTask Handle(AgentInitMessage message, CancellationToken cancellationToken)
    {
        agentAppIconProvider.SetAgentIcon(message.AgentIcon);
        return default;
    }
}