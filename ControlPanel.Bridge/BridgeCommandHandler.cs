using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Extensions;
using ControlPanel.Bridge.Protocol;
using ControlPanel.Protocol;

namespace ControlPanel.Bridge;

public interface IBridgeCommandHandler
{
    Task HandleAsync(UartMessage message, CancellationToken cancellationToken);
}

public class BridgeCommandHandler : IBridgeCommandHandler
{
    private readonly IAgentRegistry _agents;
    private readonly IControllerConnection _connection;
    private readonly IAudioStreamRepository _audioStreamRepository;
    private readonly ITextRenderer _textRenderer;
    private readonly IAudioStreamIconCache _audioStreamIconCache;
    private readonly ILogger<BridgeCommandHandler> _logger;

    public BridgeCommandHandler(IAgentRegistry agents,
        IControllerConnection connection,
        IAudioStreamRepository audioStreamRepository,
        ITextRenderer textRenderer,
        IAudioStreamIconCache audioStreamIconCache,
        ILogger<BridgeCommandHandler> logger)
    {
        _agents = agents;
        _connection = connection;
        _audioStreamRepository = audioStreamRepository;
        _textRenderer = textRenderer;
        _audioStreamIconCache = audioStreamIconCache;
        _logger = logger;
    }

    public async Task HandleAsync(UartMessage message, CancellationToken cancellationToken)
    {
        switch (message)
        {
            case UartSetVolumeMessage setVolume:
                await TrySendAsync(setVolume.Id.AgentId, setVolume, cancellationToken);
                break;
            case UartSetMuteMessage setMute:
                await TrySendAsync(setMute.Id.AgentId, setMute, cancellationToken);
                break;
            case UartGetIconMessage getIcons:
                await TrySendIconAsync(getIcons.Source, getIcons.AgentId, cancellationToken);
                break;
            case UartRequestRefreshMessage:
                await SendAllStreamsAsync(cancellationToken);
                break;
            default:
                _logger.LogWarning("Unknown message type {Type}", message.Type);
                break;
        }
    }

    private async Task TrySendAsync(string agentId, UartMessage uartMessage, CancellationToken cancellationToken)
    {
        if (!await _agents.TrySendAsync(agentId, BridgeProtocolMapper.ToTransport(uartMessage), cancellationToken))
            _logger.LogWarning("Failed to send message {Type} to agent {Agent}", uartMessage.Type, agentId);
    }

    private async Task TrySendIconAsync(string source, string agentId, CancellationToken cancellationToken)
    {
        if (_audioStreamIconCache.TryGetIcon(source, agentId, out var icon))
        {
            await _connection.SendMessageAsync(new UartIconMessage(source, agentId, icon.Size, icon.Icon), cancellationToken);
        }
        else
        {
            await _agents.TrySendAsync(agentId, new GetIconMessage(source), cancellationToken);
        }
    }

    private async Task SendAllStreamsAsync(CancellationToken cancellationToken)
    {
        var streamsInfoAsDiff = (await _audioStreamRepository.GetAllAsync(cancellationToken)).Select(AudioStreamDiff.FromStreamInfo).ToArray();
        var (updated, deleted) = new AudioStreamIncrementalSnapshot(streamsInfoAsDiff, []).ToUartAudioStreams(_textRenderer);
        await _connection.SendMessageAsync(new UartStreamsMessage(updated, deleted), cancellationToken);
    }
}