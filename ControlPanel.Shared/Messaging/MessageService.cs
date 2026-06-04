using System.Collections.Concurrent;
using System.Reflection;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    ILogger<MessageService<TMessage>> logger)
    : IMessageService<TMessage>
{
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in transport.ReadAsync(stoppingToken))
        {
            try
            {
                await mediator.PublishRuntimeAsync(message, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Error while processing message: {Type}", message?.GetType().Name ?? "<null>");
            }
        }
    }
}

internal static class MediatorRuntimePublishExtensions
{
    private static readonly ConcurrentDictionary<Type, Func<IMediator, INotification, CancellationToken, ValueTask>> _publishers = new();
    private static readonly MethodInfo _publishTypedMethod = typeof(MediatorRuntimePublishExtensions)
        .GetMethod(nameof(PublishTypedAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    public static ValueTask PublishRuntimeAsync(this IMediator mediator, object? notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification is not INotification mediatorNotification)
            throw new InvalidOperationException($"Message type {notification.GetType().Name} does not implement {nameof(INotification)}.");

        var publisher = _publishers.GetOrAdd(notification.GetType(), CreatePublisher);
        return publisher(mediator, mediatorNotification, cancellationToken);
    }

    private static Func<IMediator, INotification, CancellationToken, ValueTask> CreatePublisher(Type notificationType)
    {
        var method = _publishTypedMethod.MakeGenericMethod(notificationType);
        return method.CreateDelegate<Func<IMediator, INotification, CancellationToken, ValueTask>>();
    }

    private static ValueTask PublishTypedAsync<TNotification>(
        IMediator mediator,
        INotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        return mediator.Publish((TNotification)notification, cancellationToken);
    }
}

public class MessageServiceHost<TMessage>(
    IServiceScopeFactory scopeFactory,
    ILogger<MessageServiceHost<TMessage>> logger)
    : BackgroundService
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
