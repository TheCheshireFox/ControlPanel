using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Device.MessageHandlers;

public class GetIconMessageHandler(
    IAgentRegistry agents,
    IDeviceConnection connection,
    IAudioStreamIconCache iconCache) : INotificationHandler<GetIconDeviceMessage>
{
    public async ValueTask Handle(GetIconDeviceMessage deviceMessage, CancellationToken cancellationToken)
    {
        if (iconCache.TryGetIcon(deviceMessage.Source, deviceMessage.AgentId, deviceMessage.IconHash, out var icon))
        {
            await connection.SendMessageAsync(new IconDeviceMessage(deviceMessage.Source, deviceMessage.AgentId, deviceMessage.IconHash, icon.Size, icon.Icon), cancellationToken);
        }
        else
        {
            await agents.TrySendAsync(deviceMessage.AgentId, new GetIconAgentMessage(deviceMessage.Source), cancellationToken);
        }
    }
}