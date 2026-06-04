using ControlPanel.Agent.Messaging;
using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Device.DeviceProtocol;
using ControlPanel.Bridge.Device.Messaging;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using ControlPanel.Bridge.Transport;
using ControlPanel.Protocol;
using ControlPanel.Shared;
using ControlPanel.Shared.Logging;
using ControlPanel.Shared.Messaging;
using ControlPanel.WebSocket;

namespace ControlPanel.Bridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        var app = BuildWebApplication(args);
        
        app.UseWebSockets(new WebSocketOptions{ KeepAliveInterval = TimeSpan.FromSeconds(30) });
        app.Map("/agents/{agentId}/ws", AgentHttpHandler.HandleAsync);

        await app.RunAsync();
    }

    private static WebApplication BuildWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration
            .AddJsonFile(ConfigPathProvider.Path, false, true)
            .AddEnvironmentVariables();
        
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsoleFormatter<TemplateConsoleFormatter, TemplateConsoleFormatterOptions>();
            loggingBuilder.AddConsole(opts => opts.FormatterName = TemplateConsoleFormatter.FormatterName);
        });
        
        builder.Services.AddSystemd();

        builder.Services.Configure<StreamsOptions>(builder.Configuration.GetSection("Streams"));
        builder.Services.Configure<TransportOptions>(builder.Configuration.GetSection("Transport"));
        builder.Services.Configure<AudioStreamIconCacheOptions>(builder.Configuration.GetSection("IconCache"));
        
        builder.Services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
        builder.Services.AddScopedProxy<IWebSocket>();
        builder.Services.AddSingleton<IAudioStreamRepository, AudioStreamRepository>();
        builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
        builder.Services.AddSingleton<IAgentAppIconProvider, AgentAppIconProvider>(_ => new AgentAppIconProvider(32, 10));
        builder.Services.AddSingleton<IDeviceConnection, DeviceConnection>();
        builder.Services.AddSingleton<IAudioStreamIconCache, AudioStreamIconCache>();
        builder.Services.AddSingleton<IFrameTransport, FrameTransport>();
        builder.Services.AddSingleton<IFrameProtocol, FrameProtocol>();

        builder.Services.AddSingleton<ITransportStreamProvider, SerialPortTransportStreamProvider>();
        
        AddMediator(builder.Services);
        AddDeviceMessaging(builder.Services);
        AddAgentMessaging(builder.Services);

        builder.Services.AddScoped<IAgentContext>(_ => new AgentContext());
        
        builder.Services.AddHostedService<AudioStreamSnapshotService>();

        return builder.Build();
    }

    private static void AddMediator(IServiceCollection services)
    {
        services.AddMediator(options =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(Program)];
        });
    }
    
    private static void AddDeviceMessaging(IServiceCollection services)
    {
        services
            .AddMessaging<DeviceMessage>(x => x.WithTransport<DeviceMessageTransport>())
            .AddHostedMessaging<DeviceMessage>();
    }
    
    private static void AddAgentMessaging(IServiceCollection services)
    {
        services.AddMessaging<AgentMessage>(x => x.WithTransport<AgentMessageTransport>());
    }
}
