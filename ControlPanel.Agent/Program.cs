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

namespace ControlPanel.Agent;

public class Program
{
    public static async Task Main(string[] args)
    {
        SetUpEncoding();

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

        builder.Services.AddSingleton<IWebSocketFactory, WebSocketFactory>();
        builder.Services.AddSingleton<ITextWebSocketClientFactory, TextWebSocketClientFactory>();
        builder.Services.AddSingleton<TextWebSocketClient>();
        builder.Services.AddHostedService<AgentService>();
        
        var agentHost = CreateAudioAgentHost();
        agentHost.Configure(args, builder);
        
        await builder.Build().RunAsync();
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