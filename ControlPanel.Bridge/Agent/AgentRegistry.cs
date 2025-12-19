using System.Collections.Concurrent;
using ControlPanel.Bridge.Protocol;
using ControlPanel.Protocol;

namespace ControlPanel.Bridge.Agent;

public interface IAgentRegistry
{
    Task AddAsync(IAgentConnection connection, CancellationToken cancellationToken);
    Task RemoveAsync(IAgentConnection connection, CancellationToken cancellationToken);
    Task<bool> TrySendAsync(string agentId, BridgeMessage message, CancellationToken cancellationToken);
}

public class AgentRegistry : IAgentRegistry
{
    private readonly IAudioStreamRepository _audioStreamRepository;
    private readonly ConcurrentDictionary<string, IAgentConnection> _agents = new();

    public AgentRegistry(IAudioStreamRepository audioStreamRepository)
    {
        _audioStreamRepository = audioStreamRepository;
    }

    public Task AddAsync(IAgentConnection connection, CancellationToken cancellationToken)
    {
        _agents[connection.AgentId] = connection;
        return Task.CompletedTask;
    }

    public async Task RemoveAsync(IAgentConnection connection, CancellationToken cancellationToken)
    {
        _agents.TryRemove(connection.AgentId, out _);
        await _audioStreamRepository.ClearAsync(connection.AgentId, cancellationToken);
    }

    public async Task<bool> TrySendAsync(string agentId, BridgeMessage message, CancellationToken cancellationToken)
    {
        if (!_agents.TryGetValue(agentId, out var conn))
            return false;

        await conn.SendAsync(message, cancellationToken);
        return true;
    }
}