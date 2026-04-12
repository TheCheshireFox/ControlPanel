using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Extensions;
using ControlPanel.Bridge.Protocol;
using GetIconMessage = ControlPanel.Bridge.Protocol.GetIconMessage;
using SetMuteMessage = ControlPanel.Bridge.Protocol.SetMuteMessage;
using SetVolumeMessage = ControlPanel.Bridge.Protocol.SetVolumeMessage;
using StreamsMessage = ControlPanel.Bridge.Protocol.StreamsMessage;

namespace ControlPanel.Bridge;

public interface IBridgeCommandHandler
{
    Task HandleAsync(Message message, CancellationToken cancellationToken);
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

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        try
        {
            switch (message)
            {
                case SetVolumeMessage setVolume:
                    await TrySendAsync(setVolume.Id.AgentId, setVolume, cancellationToken);
                    break;
                case SetMuteMessage setMute:
                    await TrySendAsync(setMute.Id.AgentId, setMute, cancellationToken);
                    break;
                case GetIconMessage getIcons:
                    await TrySendIconAsync(getIcons.Source, getIcons.AgentId, cancellationToken);
                    break;
                case RequestRefreshMessage:
                    await SendAllStreamsAsync(cancellationToken);
                    break;
                case LogMessage logMessage:
                    PrintLogs(logMessage);
                    break;
                case TextRendererParametersMessage textRendererParams:
                    _textRenderer.SetParameters(textRendererParams.Dpi, textRendererParams.FontSize, textRendererParams.MaxSpriteWidth);
                    break;
                default:
                    _logger.LogWarning("Unknown message type {Type}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle message {Type}", message.Type);
        }
    }

    private async Task TrySendAsync(string agentId, Message message, CancellationToken cancellationToken)
    {
        if (!await _agents.TrySendAsync(agentId, BridgeProtocolMapper.ToTransport(message), cancellationToken))
            _logger.LogWarning("Failed to send message {Type} to agent {Agent}", message.Type, agentId);
    }

    private async Task TrySendIconAsync(string source, string agentId, CancellationToken cancellationToken)
    {
        if (_audioStreamIconCache.TryGetIcon(source, agentId, out var icon))
        {
            await _connection.SendMessageAsync(new IconMessage(source, agentId, icon.Size, icon.Icon), cancellationToken);
        }
        else
        {
            await _agents.TrySendAsync(agentId, new ControlPanel.Protocol.GetIconMessage(source), cancellationToken);
        }
    }

    private async Task SendAllStreamsAsync(CancellationToken cancellationToken)
    {
        var streamsInfoAsDiff = (await _audioStreamRepository.GetAllAsync(cancellationToken)).Select(AudioStreamDiff.FromStreamInfo).ToArray();
        var (updated, deleted) = new AudioStreamIncrementalSnapshot(streamsInfoAsDiff, []).ToUartAudioStreams(_textRenderer);
        await _connection.SendMessageAsync(new StreamsMessage(updated, deleted), cancellationToken);
    }

    private void PrintLogs(LogMessage logMessage)
    {
        var lines = logMessage.Line
            .Trim('\r')
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
            _logger.LogInformation("UART > {Message}", line);
    }
}