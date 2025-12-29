using System.Globalization;
using System.Text;
using ControlPanel.Bridge.Agent;
using ControlPanel.Bridge.Framer;
using ControlPanel.Bridge.Options;
using ControlPanel.Bridge.Transport;
using ControlPanel.Shared;
using ControlPanel.Shared.Logging;
using ControlPanel.WebSocket;

namespace ControlPanel.Bridge;

public class Program
{
    public static async Task Main(string[] args)
    {
        SetUpEncoding();

        var app = BuildWebApplication(args);
        
        app.UseWebSockets(new WebSocketOptions{ KeepAliveInterval = TimeSpan.FromSeconds(30) });
        app.Map("/agents/{agentId}/ws", HandleAgentAsync);

        await app.RunAsync();
    }

    private static async Task HandleAgentAsync(string agentId,
        HttpContext context,
        IServiceProvider serviceProvider,
        IWebSocketFactory webSocketFactory,
        IAgentRegistry agentRegistry,
        IHostApplicationLifetime applicationLifetime,
        ILogger<Program> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket expected");
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("agent connected: {Id}", agentId);

        AgentConnection? connection = null;
        try
        {
            using var webSocket = webSocketFactory.Create(ws);
            connection = ActivatorUtilities.CreateInstance<AgentConnection>(serviceProvider, agentId, webSocket);
            
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping, context.RequestAborted);
            
            await agentRegistry.AddAsync(connection, cts.Token);
            await connection.HandleAgentAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "error for agent {Id}", agentId);
        }
        finally
        {
            if (connection != null)
                await agentRegistry.RemoveAsync(connection, applicationLifetime.ApplicationStopping);
            
            connection?.Dispose();
            
            logger.LogInformation("agent disconnected: {Id}", agentId);
        }
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

        builder.Services.Configure<TransportOptions>(builder.Configuration.GetSection("Transport"));
        builder.Services.Configure<BtRfcommOptions>(builder.Configuration.GetSection("BtRfcomm"));
        builder.Services.Configure<UartOptions>(builder.Configuration.GetSection("Uart"));
        builder.Services.Configure<TextRendererOptions>(builder.Configuration.GetSection("TextRenderer"));
        builder.Services.Configure<AudioStreamIconCacheOptions>(builder.Configuration.GetSection("IconCache"));
        
        builder.Services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
        builder.Services.AddSingleton<IAudioStreamRepository, AudioStreamRepository>();
        builder.Services.AddSingleton<IBridgeCommandHandler, BridgeCommandHandler>();
        builder.Services.AddSingleton<IAgentRegistry, AgentRegistry>();
        builder.Services.AddSingleton<ControlPanelBridge>();
        builder.Services.AddSingleton<IControllerConnection, ControllerConnection>();
        builder.Services.AddSingleton<ITextRenderer, TextRenderer>();
        builder.Services.AddSingleton<IAudioStreamIconCache, AudioStreamIconCache>();
        builder.Services.AddSingleton<IFrameTransport, UartFrameTransport>();
        builder.Services.AddSingleton<IFrameProtocol, FrameProtocol>();

        AddTransportStreamProvider(builder);
        
        builder.Services.AddHostedService(sp => sp.GetRequiredService<ControlPanelBridge>());

        return builder.Build();
    }

    private static void AddTransportStreamProvider(IHostApplicationBuilder builder)
    {
        var cfg = builder.Configuration.GetSection("Transport").Get<TransportOptions>() ?? throw new InvalidOperationException("Transport config section not found");

        switch (cfg.Type)
        {
            case TransportType.Serial:
                builder.Services.AddSingleton<ITransportStreamProvider, SerialPortTransportStreamProvider>();
                break;
            case TransportType.BtRfcomm:
                builder.Services.AddSingleton<ITransportStreamProvider, BrRfcommTransportStreamProvider>();
                break;
            default:
                throw new InvalidOperationException($"TransportType {cfg.Type} not supported");
        }
    }

    private static void SetUpEncoding()
    {
        Encoding.RegisterProvider(new AliasEncodingProvider(new Dictionary<string, Encoding>
        {
            ["utf8"] = Encoding.UTF8
        }));
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }
}