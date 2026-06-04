using ControlPanel.Agent.Shared;
using ControlPanel.Agent.Windows.WindowsAudioSystem;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

namespace ControlPanel.Agent.Windows;

public class WindowsAudioAgentHost : IAudioAgentHost
{
    public void Configure(string[] args, IHostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAudioSessionProvider, AudioSessionProvider>();
        builder.Services.AddSingleton<IIconLocator, IconLocator>();
        builder.Services.AddSingleton<IAudioAgent, WindowsAudioAgent>();
        
        LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(builder.Services);
        
        var dir = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath) ?? ".", "logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(Path.Combine(dir, "agent-.log"), rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 1024 * 1024, shared: true)
            .CreateLogger();
        
        builder.Services.AddWindowsService(opts => opts.ServiceName = "ControlPanel.Agent");
        builder.Logging.AddSerilog();
        
        if (args.Contains("--headless"))
            builder.Services.AddHostedService<ConsoleHidingService>();
    }
}