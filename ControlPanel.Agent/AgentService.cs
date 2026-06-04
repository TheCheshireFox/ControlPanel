using ControlPanel.Agent.Messaging;
using ControlPanel.Agent.Options;
using ControlPanel.Agent.Shared;
using ControlPanel.Protocol;
using ControlPanel.Shared;
using ControlPanel.Shared.Messaging;
using ControlPanel.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ControlPanel.Agent;

public class AgentService(
    IServiceProvider serviceProvider,
    IOptions<AgentServiceOptions> options,
    IAudioAgent audioAgent,
    IWebSocketFactory webSocketFactory,
    ILogger<AgentService> logger)
    : BackgroundService
{
    private readonly Uri _bridgeUri = new($"ws://{options.Value.Address}/agents/{options.Value.AgentId}/ws");
    private readonly TimeSpan _reconnectDelay = TimeSpan.FromSeconds(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var ws = webSocketFactory.Create();

            try
            {
                await RunConnectionAsync(ws, stoppingToken);
                logger.LogInformation("connection ended");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "connection error");
            }

            if (stoppingToken.IsCancellationRequested)
                break;
            
            logger.LogInformation("reconnecting in {Delay}...", _reconnectDelay);
            await Task.Delay(_reconnectDelay, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        logger.LogInformation("stopped");
    }

    private async Task RunConnectionAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        logger.LogInformation("connecting to {Uri}", _bridgeUri);
        await ws.ConnectAsync(_bridgeUri, cancellationToken);
        logger.LogInformation("connected");

        await using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.SetScopedProxy(ws);

        var messageService = scope.ServiceProvider.GetRequiredService<IMessageService<AgentMessage>>();
        var snapshotService = scope.ServiceProvider.GetRequiredService<IAudioStreamSnapshotService>();

        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await SendAgentInitMessageAsync(ws, connectionCts.Token);

        var tasks = new[]
        {
            messageService.RunAsync(connectionCts.Token),
            snapshotService.RunAsync(connectionCts.Token)
        };
        
        try
        {
            await await Task.WhenAny(tasks);
        }
        finally
        {
            await connectionCts.CancelAsync();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception) when (connectionCts.IsCancellationRequested)
            {
                // NOP
            }
        }
    }
    
    private async Task SendAgentInitMessageAsync(IWebSocket ws, CancellationToken cancellationToken)
    {
        var dsc = await audioAgent.GetAudioAgentDescription();
        var msg = new AgentInitMessage(dsc.AgentIcon);
        await ws.SendAsync(AgentMessageSerializer.Serialize(msg), cancellationToken);
    }
}