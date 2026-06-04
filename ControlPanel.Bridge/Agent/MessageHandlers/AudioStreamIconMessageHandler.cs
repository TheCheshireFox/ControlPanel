using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Bridge.Agent.MessageHandlers;

public class AudioStreamIconMessageHandler(
    IAudioStreamIconCache audioStreamIconCache,
    IAgentAppIconProvider agentAppIconProvider,
    IDeviceConnection deviceConnection,
    IAgentContext context,
    ILogger<AudioStreamIconMessageHandler> logger) : INotificationHandler<AudioStreamIconMessage>
{
    public async ValueTask Handle(AudioStreamIconMessage message, CancellationToken cancellationToken)
    {
        var (size, icon) = ToUartIcon(message);
        await deviceConnection.SendMessageAsync(new IconDeviceMessage(message.Source, context.AgentId, message.IconHash, size, icon), cancellationToken);
    }

    private (int Size, byte[] Icon) ToUartIcon(AudioStreamIconMessage msg)
    {
        using var appImg = agentAppIconProvider.GetAgentAppIcon(msg.Icon);
        var icon = LvglImageConverter.ConvertToRgb565A8(appImg);

        logger.LogDebug("New icon: {Source}, size: {Size}", msg.Source, msg.Icon.Length);
        audioStreamIconCache.AddIcon(msg.Source, context.AgentId, msg.IconHash, new AudioCacheIcon(agentAppIconProvider.IconSize, icon));

        return (agentAppIconProvider.IconSize, icon);
    }
}