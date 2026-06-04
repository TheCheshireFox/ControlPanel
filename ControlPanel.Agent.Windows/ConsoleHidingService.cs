using Microsoft.Extensions.Hosting;

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