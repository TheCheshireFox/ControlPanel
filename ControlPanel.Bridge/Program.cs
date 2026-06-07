using ControlPanel.Agent.Messaging;
using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Audio;
using ControlPanel.Bridge.Device.DeviceProtocol;
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
        builder.Services.AddSingleton<IAudioStreamRepository, AudioStreamRepository>();
        builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
        builder.Services.AddSingleton<IAudioStreamIconCache, AudioStreamIconCache>();
        
        builder.Services.AddScopedProxy<IWebSocket>();
        builder.Services.AddScopedProxy<IAgentContext>();
        builder.Services.AddScoped<IAgentAppIconProvider, AgentAppIconProvider>(_ => new AgentAppIconProvider(32, 10));
        
        AddMediator(builder.Services);
        AddDeviceTransport(builder.Services);
        AddDeviceMessaging(builder.Services);
        AddAgentMessaging(builder.Services);

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

    private static void AddDeviceTransport(IServiceCollection services)
    {
        services.AddSingleton<IStreamConnector, SerialPortConnector>();
        services.AddSingleton<IFrameChannel, FramedByteChannel>();
        services.AddSingleton<DeviceMessageChannel>();
        services.AddSingleton<IDeviceConnection>(sp => sp.GetRequiredService<DeviceMessageChannel>());
        services.AddScoped<IMessageTransport<DeviceMessage>>(sp => sp.GetRequiredService<DeviceMessageChannel>());
    }
    
    private static void AddDeviceMessaging(IServiceCollection services)
    {
        services.AddMessaging<DeviceMessage>();
        services.AddHostedMessaging<DeviceMessage>();
    }
    
    private static void AddAgentMessaging(IServiceCollection services)
    {
        services.AddMessaging<AgentMessage>()
            .WithTransport<AgentMessageTransport>();
    }
}
