using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Agent.MessageHandlers;

public class InitAgentMessageHandler(IAgentAppIconProvider agentAppIconProvider) : INotificationHandler<InitAgentMessage>
{
    public ValueTask Handle(InitAgentMessage message, CancellationToken cancellationToken)
    {
        agentAppIconProvider.SetAgentIcon(message.AgentIcon);
        return default;
    }
}