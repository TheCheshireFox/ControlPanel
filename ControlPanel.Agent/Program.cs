using ControlPanel.Agent.Linux;
using ControlPanel.Agent.Messaging;
using ControlPanel.Agent.Options;
using ControlPanel.Agent.Shared;
using ControlPanel.Agent.Windows;
using ControlPanel.Protocol;
using ControlPanel.Shared;
using ControlPanel.Shared.Logging;
using ControlPanel.Shared.Messaging;
using ControlPanel.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlPanel.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration
            .AddJsonFile(ConfigPathProvider.Path, true, true)
            .AddEnvironmentVariables();
        builder.Services.Configure<AgentServiceOptions>(builder.Configuration.GetSection("Agent"));
        
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole(opts => opts.FormatterName = TemplateConsoleFormatter.FormatterName);
            loggingBuilder.AddConsoleFormatter<TemplateConsoleFormatter, TemplateConsoleFormatterOptions>();
        });

        AddMessaging(builder.Services);
        AddAgentServices(builder.Services);

        builder.Services.AddHostedService<AgentService>();
        
        var agentHost = CreateAudioAgentHost();
        agentHost.Configure(args, builder);
        
        await builder.Build().RunAsync();
    }

    private static void AddAgentServices(IServiceCollection services)
    {
        services.AddScopedProxy<IWebSocket>();
        services.AddScoped<IAudioStreamSnapshotService>();
        services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
    }
    
    private static void AddMessaging(IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(Program)];
        });
        services.AddMessaging<AgentMessage>(x => x.WithTransport<AgentMessageTransport>());
    }
    
    private static IAudioAgentHost CreateAudioAgentHost()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => new LinuxAudioAgentHost(),
            PlatformID.Win32NT or PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.WinCE => new WindowsAudioAgentHost(),
            _ => throw new NotSupportedException("Operation system not supported")
        };
    }
}