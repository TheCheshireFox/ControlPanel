using Mediator;
using Microsoft.Extensions.Logging;

namespace ControlPanel.Shared.Messaging;

public interface IMessageTransport<out TMessage>
{
    IAsyncEnumerable<TMessage> ReadAsync(CancellationToken cancellationToken);
}

public interface IMessageService<TMessage>
{
    Task RunAsync(CancellationToken stoppingToken);
}

public class MessageService<TMessage>(
    IMessageTransport<TMessage> transport,
    IMediator mediator,
    ILogger<MessageService<TMessage>> logger) : IMessageService<TMessage>
    where TMessage: notnull
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in transport.ReadAsync(stoppingToken))
        {
            try
            {
                await mediator.Publish(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error while processing message: {Type}", message?.GetType().Name ?? "<null>");
            }
        }
    }
}