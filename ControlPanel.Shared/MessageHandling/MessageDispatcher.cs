using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Shared.MessageHandling;

public interface IMessageDispatcher<in TBaseMessage>
{
    Task HandleAsync(TBaseMessage message, CancellationToken cancellationToken);
}

public class MessageDispatcher<TBaseMessage>(IServiceProvider serviceProvider)
    : IMessageDispatcher<TBaseMessage>
{
    public async Task HandleAsync(TBaseMessage message, CancellationToken cancellationToken)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));
        
        await InvokeHandlerAsync((dynamic)message, cancellationToken);
    }

    private async Task InvokeHandlerAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetRequiredService<IMessageHandler<TMessage>>();
        await handler.HandleAsync(message, cancellationToken);
    }
}