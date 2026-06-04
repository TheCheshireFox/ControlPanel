using System.Text.RegularExpressions;
using ControlPanel.Bridge.Options;
using ControlPanel.Protocol;
using Mediator;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Agent.MessageHandlers;

public class StreamsMessageHandler(
    IOptions<StreamsOptions> streamsOptions,
    IAudioStreamRepository audioStreamRepository,
    IAgentContext context) : INotificationHandler<StreamsMessage>
{
    public async ValueTask Handle(StreamsMessage message, CancellationToken cancellationToken)
    {
        var streams = message.Streams
            .Where(x => !streamsOptions.Value.Exclude
                .Any(r => Regex.IsMatch(x.Name, r)));
        await audioStreamRepository.UpdateAsync(context.AgentId, streams, cancellationToken);
    }
}