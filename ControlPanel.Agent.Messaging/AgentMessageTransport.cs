using ControlPanel.AgentProtocol;
using ControlPanel.Shared.Messaging;
using ControlPanel.WebSocket;

namespace ControlPanel.Agent.Messaging;

public class AgentMessageTransport(IWebSocket ws) : IMessageTransport<AgentMessage>
{
    public IAsyncEnumerable<AgentMessage> ReadAsync(CancellationToken cancellationToken)
        => ws.ReceiveAsync(cancellationToken).Select(AgentMessageSerializer.Deserialize);
}