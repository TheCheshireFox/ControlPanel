using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Agent.Shared;

public interface IAudioAgentSystemInitializer
{
    IServiceCollection AddServices(IServiceCollection services);
}