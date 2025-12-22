using System.Globalization;
using System.Text;
using ControlPanel.Agent.Linux;
using ControlPanel.Agent.Options;
using ControlPanel.Agent.Shared;
using ControlPanel.Agent.WebSocket;
using ControlPanel.Agent.Windows;
using ControlPanel.Shared;
using ControlPanel.Shared.Logging;
using ControlPanel.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

namespace ControlPanel.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        SetUpEncoding();

        var builder = Host.CreateApplicationBuilder(args);
        
        builder.Services.AddWindowsService(opts => opts.ServiceName = "ControlPanel.Agent");
        builder.Services.AddSystemd();
        
        builder.Configuration
            .AddJsonFile(ConfigPathProvider.Path, true, true)
            .AddEnvironmentVariables();
        builder.Services.Configure<AgentServiceOptions>(builder.Configuration.GetSection("Agent"));
        
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConsole(opts => opts.FormatterName = TemplateConsoleFormatter.FormatterName);
            loggingBuilder.AddConsoleFormatter<TemplateConsoleFormatter, TemplateConsoleFormatterOptions>();
        });
        
        AddWindowsLogging(builder);

        var initializer = CreateAudioAgentSystemInitializer();
        initializer.AddServices(builder.Services);
        
        builder.Services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
        builder.Services.AddSingleton<ITextWebSocketClientFactory, TextWebSocketClientFactory>();
        builder.Services.AddSingleton<TextWebSocketClient>();
        builder.Services.AddHostedService<AgentService>();
        
        var host = builder.Build();
        await host.RunAsync();
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
    
    private static IAudioAgentSystemInitializer CreateAudioAgentSystemInitializer()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => new LinuxAudioAgentSystemInitializer(),
            PlatformID.Win32NT or PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.WinCE => new WindowsAudioAgentSystemInitializer(),
            _ => throw new NotSupportedException("Operation system not supported")
        };
    }

    private static void AddWindowsLogging(HostApplicationBuilder builder)
    {
        if (!OperatingSystem.IsWindows())
            return;
        
        LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
        
        var dir = Path.Combine(ConfigPathProvider.AppDir, "logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(dir, "agent-.log"), rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 1024 * 1024, shared: true)
            .CreateLogger();
        
        builder.Logging.AddSerilog();
    }
}