using ControlPanel.Agent.Shared;
using ControlPanel.Protocol;
using Mediator;

namespace ControlPanel.Agent.MessageHandlers;

public class SetMuteMessageHandler(IAudioAgent audioAgent) : INotificationHandler<SetMuteMessage>
{
    public async ValueTask Handle(SetMuteMessage message, CancellationToken cancellationToken)
    {
        await audioAgent.ToggleMuteAsync(message.Id, message.Mute, cancellationToken);
    }
}