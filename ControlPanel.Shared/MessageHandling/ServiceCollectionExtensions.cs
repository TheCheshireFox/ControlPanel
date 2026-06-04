using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Shared.MessageHandling;

public class MessageDispatcherBuilder<TBaseMessage>(
    IServiceCollection services)
{
    public MessageDispatcherBuilder<TBaseMessage> AddHandler<TMessage, THandler>()
        where TMessage : TBaseMessage
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        return this;
    }
}

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMessageDispatcher<TBaseMessage>(Action<MessageDispatcherBuilder<TBaseMessage>> configure)
        {
            return services.AddMessageDispatcher(ServiceLifetime.Singleton, configure);
        }

        public IServiceCollection AddMessageDispatcher<TBaseMessage>(ServiceLifetime serviceLifetime, Action<MessageDispatcherBuilder<TBaseMessage>> configure)
        {
            services.Add(new ServiceDescriptor(typeof(IMessageDispatcher<TBaseMessage>), typeof(MessageDispatcher<TBaseMessage>), serviceLifetime));

            var builder = new MessageDispatcherBuilder<TBaseMessage>(services);
            configure(builder);

            return services;
        }
    }
}