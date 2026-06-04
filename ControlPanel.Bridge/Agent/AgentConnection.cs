using ControlPanel.Agent.Messaging;
using ControlPanel.Protocol;
using ControlPanel.Shared.Messaging;
using ControlPanel.WebSocket;

namespace ControlPanel.Bridge.Agent;

public interface IAgentConnection
{
    string AgentId { get; }
    Task RunAsync(CancellationToken cancellationToken);
    Task SendAsync(AgentMessage message, CancellationToken cancellationToken);
}

public sealed class AgentConnection(
    IWebSocket ws,
    IAgentContext agentContext,
    IAudioStreamIconCache audioStreamIconCache,
    IMessageService<AgentMessage> messageService)
    : IAgentConnection, IMessageTransport<AgentMessage>, IDisposable
{
    public string AgentId => agentContext.AgentId;

    public Task RunAsync(CancellationToken cancellationToken)
        => messageService.RunAsync(cancellationToken);

    public Task SendAsync(AgentMessage message, CancellationToken cancellationToken)
        => ws.SendAsync(AgentMessageSerializer.Serialize(message), cancellationToken);

    public IAsyncEnumerable<AgentMessage> ReadAsync(CancellationToken cancellationToken)
        => ws.ReceiveAsync(cancellationToken).Select(AgentMessageSerializer.Deserialize);
    
    public void Dispose() 
        => audioStreamIconCache.RemoveIcons(AgentId);
}