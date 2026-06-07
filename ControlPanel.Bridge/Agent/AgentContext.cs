namespace ControlPanel.Bridge.Agent;

public interface IAgentContext
{
    string AgentId { get; }
}

public class AgentContext : IAgentContext
{
    public required string AgentId { get; init; } = string.Empty;
}