using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Agent.MessageHandlers;

public class AudioStreamIconAgentMessageHandler(
    IAudioStreamIconCache audioStreamIconCache,
    IAgentAppIconProvider agentAppIconProvider,
    IDeviceConnection deviceConnection,
    IAgentContext context,
    ILogger<AudioStreamIconAgentMessageHandler> logger) : INotificationHandler<AudioStreamIconAgentMessage>
{
    public async ValueTask Handle(AudioStreamIconAgentMessage agentMessage, CancellationToken cancellationToken)
    {
        var (size, icon) = ToUartIcon(agentMessage);
        await deviceConnection.SendMessageAsync(new IconDeviceMessage(agentMessage.Source, context.AgentId, agentMessage.IconHash, size, icon), cancellationToken);
    }

    private (int Size, byte[] Icon) ToUartIcon(AudioStreamIconAgentMessage msg)
    {
        using var appImg = agentAppIconProvider.GetAgentAppIcon(msg.Icon);
        var icon = LvglImageConverter.ConvertToRgb565A8(appImg);

        logger.LogDebug("New icon: {Source}, size: {Size}", msg.Source, msg.Icon.Length);
        audioStreamIconCache.AddIcon(msg.Source, context.AgentId, msg.IconHash, new AudioCacheIcon(agentAppIconProvider.IconSize, icon));

        return (agentAppIconProvider.IconSize, icon);
    }
}