using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Device.MessageHandlers;

public class SetVolumeMessageHandler(
    IAgentRegistry agents,
    ILogger<SetVolumeMessageHandler> logger) : INotificationHandler<SetVolumeDeviceMessage>
{
    public async ValueTask Handle(SetVolumeDeviceMessage deviceMessage, CancellationToken cancellationToken)
    {
        var agentMessage = new SetVolumeAgentMessage(deviceMessage.Id.Id, deviceMessage.Volume);
        
        if (!await agents.TrySendAsync(deviceMessage.Id.AgentId, agentMessage, cancellationToken))
            logger.LogWarning("Failed to send message {Type} to agent {Agent}", deviceMessage.Type, deviceMessage.Id.AgentId);
    }
}
