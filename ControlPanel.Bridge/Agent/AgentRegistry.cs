using System.Collections.Concurrent;
using ControlPanel.AgentProtocol;
using ControlPanel.Bridge.Audio;

namespace ControlPanel.Bridge.Agent;

public interface IAgentRegistry
{
    Task AddAsync(IAgentConnection connection, CancellationToken cancellationToken);
    Task RemoveAsync(IAgentConnection connection, CancellationToken cancellationToken);
    Task<bool> TrySendAsync(string agentId, AgentMessage message, CancellationToken cancellationToken);
}

public class AgentRegistry(IAudioStreamRepository audioStreamRepository) : IAgentRegistry
{
    private readonly ConcurrentDictionary<string, IAgentConnection> _agents = new();

    public Task AddAsync(IAgentConnection connection, CancellationToken cancellationToken)
    {
        _agents[connection.AgentId] = connection;
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(IAgentConnection connection, CancellationToken cancellationToken)
    {
        var removed = _agents.TryRemove(new KeyValuePair<string, IAgentConnection>(connection.AgentId, connection));
        if (removed)
            await audioStreamRepository.ClearAsync(connection.AgentId, cancellationToken);
    }

    public async Task<bool> TrySendAsync(string agentId, AgentMessage message, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(agentId, out var conn))
            return false;

        await conn.SendAsync(message, cancellationToken);
        return true;
    }
}