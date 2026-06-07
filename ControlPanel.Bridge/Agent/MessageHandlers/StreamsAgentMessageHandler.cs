using System.Text.RegularExpressions;
using ControlPanel.AgentProtocol;
using ControlPanel.Bridge.Audio;
using ControlPanel.Bridge.Options;
using Mediator;
using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Agent.MessageHandlers;

public class StreamsAgentMessageHandler(
    IOptions<StreamsOptions> streamsOptions,
    IAudioStreamRepository audioStreamRepository,
    IAgentContext context) : INotificationHandler<StreamsAgentMessage>
{
    public async ValueTask Handle(StreamsAgentMessage agentMessage, CancellationToken cancellationToken)
    {
        var streams = agentMessage.Streams
            .Where(x => !streamsOptions.Value.Exclude
                .Any(r => Regex.IsMatch(x.Name, r)));
        await audioStreamRepository.UpdateAsync(context.AgentId, streams, cancellationToken);
    }
}