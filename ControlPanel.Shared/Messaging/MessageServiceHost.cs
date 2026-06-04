using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ControlPanel.Shared.Messaging;

public class MessageServiceHost<TMessage>(
    IServiceScopeFactory scopeFactory,
    ILogger<MessageServiceHost<TMessage>> logger)
    : BackgroundService
    where TMessage : notnull
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IMessageService<TMessage>>();
                await service.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error while running message service");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }
}