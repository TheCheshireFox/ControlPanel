using Microsoft.Extensions.Hosting;

namespace ControlPanel.Agent.Shared;

public interface IAudioAgentHost
{
    void Configure(string[] args, IHostApplicationBuilder builder);
}