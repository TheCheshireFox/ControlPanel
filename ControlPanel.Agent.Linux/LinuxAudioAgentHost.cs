using ControlPanel.Agent.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ControlPanel.Agent.Linux;

public class LinuxAudioAgentHost : IAudioAgentHost
{
    public void Configure(string[] args, IHostApplicationBuilder builder)
    {
        builder.Services.AddSystemd();
        builder.Services.AddSingleton<IIconLocator, IconLocator>();
        builder.Services.AddSingleton<IAudioAgent, PipeWireAudioAgent>();
    }
}