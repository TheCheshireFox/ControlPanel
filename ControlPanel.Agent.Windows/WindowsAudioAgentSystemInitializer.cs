using ControlPanel.Agent.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Agent.Windows;

public class WindowsAudioAgentSystemInitializer : IAudioAgentSystemInitializer
{
    public IServiceCollection AddServices(IServiceCollection services)
    {
        services.AddSingleton<IIconLocator, IconLocator>();
        services.AddSingleton<IAudioAgent, WindowsAudioAgent>();
        return services;
    }
}