using ControlPanel.Agent.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Serilog;

namespace ControlPanel.Agent.Windows;

internal class ConsoleHidingService : IHostedLifecycleService
{
    public Task StartingAsync(CancellationToken cancellationToken)
    {
        ConsoleWindow.Hide();
        return Task.CompletedTask;
    }
    
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public class WindowsAudioAgentHost : IAudioAgentHost
{
    public void Configure(string[] args, IHostApplicationBuilder builder)
    {
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