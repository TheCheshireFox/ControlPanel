using System.Text.Json;
using ControlPanel.Bridge.Protocol;
using ControlPanel.Protocol;
using ControlPanel.WebSocket;

namespace ControlPanel.Bridge.Agent;

public interface IAgentConnection
{
    string AgentId { get; }
    Task SendAsync(BridgeMessage message, CancellationToken cancellationToken);
}

public sealed class AgentConnection : IAgentConnection, IDisposable
{
    private readonly ILogger<AgentConnection> _logger;
    private readonly ITextWebSocketClient _ws;
    private readonly IAudioStreamRepository _audioStreamRepository;
    private readonly IAudioStreamIconCache _audioStreamIconCache;
    private readonly IControllerConnection _controllerConnection;

    private readonly AgentAppIconProvider _agentAppIconProvider = new(32, 10);
    
    public string AgentId { get; }
    
    public AgentConnection(string agentId,
        ITextWebSocketClient ws,
        IAudioStreamRepository audioStreamRepository,
        IAudioStreamIconCache audioStreamIconCache,
        IControllerConnection controllerConnection,
        ILogger<AgentConnection> logger)
    {
        AgentId = agentId;
        _ws = ws;
        _audioStreamRepository = audioStreamRepository;
        _audioStreamIconCache = audioStreamIconCache;
        _controllerConnection = controllerConnection;
        _logger = logger;

        _audioStreamRepository.OnSnapshotChangedAsync += SnapshotChangedAsync;
    }
    
    public async Task HandleAgentAsync(CancellationToken cancellationToken)
    {
        await foreach(var json in _ws.ReceiveAsync(cancellationToken))
            await HandleAgentMessageAsync(json, cancellationToken);
    }

    public Task SendAsync(BridgeMessage message, CancellationToken cancellationToken)
    {
        return _ws.SendAsync(JsonSerializer.Serialize((dynamic)message), cancellationToken);
    }
    
    private async Task HandleAgentMessageAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            
            _logger.LogDebug("{json}", json);
            
            var type = doc.Deserialize<BridgeMessage>()?.Type;
            if (type == null)
            {
                _logger.LogError("message without Type, agent: {Id}", AgentId);
                return;
            }
            
            switch (type)
            {
                case BridgeMessageType.AgentInit:
                {
                    var msg = doc.Deserialize<AgentInitMessage>() ?? throw new JsonException($"Unable to parse {nameof(UartMessageType.Streams)} message");
                    _agentAppIconProvider.SetAgentIcon(msg.AgentIcon);
                    break;
                }
                case BridgeMessageType.Streams:
                {
                    var msg = doc.Deserialize<StreamsMessage>() ?? throw new JsonException($"Unable to parse {nameof(UartMessageType.Streams)} message");
                    await _audioStreamRepository.UpdateAsync(AgentId, msg.Streams, cancellationToken);
                    break;
                }
                case BridgeMessageType.Icon:
                {
                    var msg = doc.Deserialize<AudioStreamIconMessage>() ?? throw new JsonException($"Unable to parse {nameof(UartMessageType.Streams)} message");
                    var (size, icon) = ToUartIcon(msg);
                    await _controllerConnection.SendMessageAsync(new UartIconMessage(msg.Source, AgentId, size, icon), cancellationToken);
                    break;
                }
                default:
                    _logger.LogWarning("Unknown message type '{type}', agent: {Id}", type, AgentId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle message, agent: {Id}", AgentId);
        }
    }

    private (int Size, byte[] Icon) ToUartIcon(AudioStreamIconMessage msg)
    {
        using var appImg = _agentAppIconProvider.GetAgentAppIcon(msg.Icon);
        var icon = LvglImageConverter.ConvertToRgb565A8(appImg);
                    
        _logger.LogDebug("New icon: {Source}, size: {Size}", msg.Source, msg.Icon.Length);
        _audioStreamIconCache.AddIcon(msg.Source, AgentId, new AudioCacheIcon(_agentAppIconProvider.IconSize, icon));

        return (_agentAppIconProvider.IconSize, icon);
    }

    private Task SnapshotChangedAsync(AudioStreamIncrementalSnapshot snapshot, CancellationToken cancellationToken)
    {
        var deletedSources = snapshot.Deleted
            .Where(x => x.Id.AgentId == AgentId)
            .Select(x => x.Source)
            .Distinct();
        
        foreach (var source in deletedSources)
            _audioStreamIconCache.RemoveIcon(source, AgentId);
        
        return Task.CompletedTask;
    }
    
    public void Dispose()
    {
        _audioStreamRepository.OnSnapshotChangedAsync -= SnapshotChangedAsync;
        _audioStreamIconCache.RemoveIcons(AgentId);
    }
}