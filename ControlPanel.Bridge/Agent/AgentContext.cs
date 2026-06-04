namespace ControlPanel.Bridge.Agent;

public interface IAgentContext
{
    string AgentId { get; }
}

public class AgentContext : IAgentContext
{
    public string AgentId { get; set; } = string.Empty;
}