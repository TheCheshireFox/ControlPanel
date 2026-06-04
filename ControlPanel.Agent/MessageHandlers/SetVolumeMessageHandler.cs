using ControlPanel.Agent.Shared;
using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Agent.MessageHandlers;

public class SetVolumeMessageHandler(IAudioAgent audioAgent) : INotificationHandler<SetVolumeMessage>
{
    public async ValueTask Handle(SetVolumeMessage message, CancellationToken cancellationToken)
    {
        await audioAgent.SetVolumeAsync(message.Id, message.Volume, cancellationToken);
    }
}