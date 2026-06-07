using ControlPanel.Bridge.Agent;
using ControlPanel.Shared;
using ControlPanel.WebSocket;

namespace ControlPanel.Bridge;

public class AgentHttpHandler
{
    public static async Task HandleAsync(string agentId,
        HttpContext context,
        IServiceProvider serviceProvider,
        IWebSocketFactory webSocketFactory,
        IAgentRegistry agentRegistry,
        IHostApplicationLifetime applicationLifetime,
        ILogger<AgentHttpHandler> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket expected");
            return;
        }

        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        logger.LogInformation("agent connected: {Id}", agentId);

        await using var scope = serviceProvider.CreateAsyncScope();
        scope.ServiceProvider.SetScopedProxy<IAgentContext>(new AgentContext{ AgentId = agentId });
        
        AgentConnection? connection = null;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(applicationLifetime.ApplicationStopping, context.RequestAborted);
        try
        {
            using var webSocket = webSocketFactory.Create(ws);
            scope.ServiceProvider.SetScopedProxy(webSocket);
            connection = ActivatorUtilities.CreateInstance<AgentConnection>(scope.ServiceProvider);
            
            await agentRegistry.AddAsync(connection, cts.Token);
            await connection.RunAsync(cts.Token);
        }
        catch (Exception ex) when (!cts.IsCancellationRequested)
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
}
