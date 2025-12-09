using System.Text.Json;
using ControlPanel.Protocol;
using ControlPanel.WebSocket;

namespace ControlPanel.Bridge.Agent;

public class AgentHandler
{
    private readonly string _agentId;
    private readonly IControlPanelBridge _controlPanelBridge;
    private readonly ILogger<AgentHandler> _logger;
    private readonly ILogger<WebSocketJsonMessageHandler> _wsLogger;

    public AgentHandler(string agentId, IControlPanelBridge controlPanelBridge, ILoggerFactory loggerFactory)
    {
        _agentId = agentId;
        _controlPanelBridge = controlPanelBridge;
        _logger = loggerFactory.CreateLogger<AgentHandler>();
        _wsLogger = loggerFactory.CreateLogger<WebSocketJsonMessageHandler>();
    }
    
    public async Task HandleAgentAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        var wsHandler = new WebSocketJsonMessageHandler(ws, _wsLogger);
       
        await foreach(var json in wsHandler.HandleAsync(cancellationToken))
            await HandleAgentMessageAsync(json, cancellationToken);
    }

    private async Task HandleAgentMessageAsync(string json, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            
            var type = doc.Deserialize<BridgeMessage>()?.Type;
            if (type == null)
            {
                _logger.LogError("message without Type, agent: {Id}", _agentId);
                return;
            }
            
            switch (type)
            {
                case BridgeMessageType.Streams:
                {
                    var msg = doc.Deserialize<StreamsMessage>() ?? throw new Exception($"Unable to parse {nameof(BridgeMessageType.Streams)} message");
                    await _controlPanelBridge.SendStreamsAsync(msg.Streams,  cancellationToken);
                    break;
                }
                default:
                    _logger.LogWarning("unknown message type '{type}', agent: {Id}", type, _agentId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "failed to handle message, agent: {Id}", _agentId);
        }
    }
}