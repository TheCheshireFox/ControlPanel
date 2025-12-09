using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Agent.WebSocket;
using ControlPanel.Protocol;
using ControlPanel.Shared;
using ControlPanel.Shared.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Agent;

public sealed class TestWebSocket : IWebSocket, IWebSocketFactory
{
    public bool Connected => true;

    public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Connect {uri}");
        return Task.CompletedTask;
    }

    public Task CloseAsync(WebSocketCloseStatus closeStatus, string statusDescription, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Close {closeStatus} {statusDescription}");
        return Task.CompletedTask;
    }

    public Task SendAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Send {Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count)}");
        return Task.CompletedTask;
    }

    public async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        await Task.Delay(10000, cancellationToken);
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new SetVolumeMessage("99999", 0.1)));
        jsonBytes.CopyTo(buffer.Array!, buffer.Offset);

        return new WebSocketReceiveResult(jsonBytes.Length, WebSocketMessageType.Text, true);
    }
    
    public void Dispose()
    {

    }

    public IWebSocket Create() => new TestWebSocket();
}

class Program
{
    static async Task Main(string[] args)
    {
        Encoding.RegisterProvider(new AliasEncodingProvider(new Dictionary<string, Encoding>
        {
            ["utf8"] = Encoding.UTF8
        }));
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        var loggerProvider = CreateLoggerProvider();
        var loggerFactory = new LoggerFactory([loggerProvider]);
        var agent = CreateAudioAgent();
        var service = new AgentService(new Uri("ws://localhost:8080"), agent, new TestWebSocket(), loggerFactory.CreateLogger<AgentService>());
        await service.RunAsync(CancellationToken.None);
    }

    private static IAudioAgent CreateAudioAgent()
    {
        return Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => new PipeWireAudioAgent(),
            PlatformID.Win32NT or PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.WinCE => new WindowsAudioAgent(),
            _ => throw new NotSupportedException("Operation system not supported")
        };
    }
    
    private static ConsoleLoggerProvider CreateLoggerProvider()
    {
        var formatter = new TemplateConsoleFormatter(new OptionsWrapper<TemplateConsoleFormatterOptions>(new TemplateConsoleFormatterOptions()));
        
        var configureOptions = new ConfigureOptions<ConsoleLoggerOptions>(x => x.FormatterName = TemplateConsoleFormatter.FormatterName);
        var factory = new OptionsFactory<ConsoleLoggerOptions>([configureOptions], []);
        var options = new OptionsMonitor<ConsoleLoggerOptions>(factory, [], new OptionsCache<ConsoleLoggerOptions>());
        return new ConsoleLoggerProvider(options, [formatter]);
    }
}