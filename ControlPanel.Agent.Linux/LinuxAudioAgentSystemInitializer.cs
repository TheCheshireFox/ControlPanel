using ControlPanel.Agent.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Agent.Linux;

public class LinuxAudioAgentSystemInitializer : IAudioAgentSystemInitializer
{
    public IServiceCollection AddServices(IServiceCollection services)
    {
        return services.AddSingleton<IAudioAgent, PipeWireAudioAgent>();
    }
}