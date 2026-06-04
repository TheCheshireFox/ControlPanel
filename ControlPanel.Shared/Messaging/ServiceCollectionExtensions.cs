using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Shared.Messaging;

public class MessagingBuilder<TMessage>(
    IServiceCollection services)
    where TMessage : notnull
{
    public MessagingBuilder<TMessage> WithTransport<TTransport>()
        where TTransport : class, IMessageTransport<TMessage>
    {
        services.AddScoped<IMessageTransport<TMessage>, TTransport>();
        return this;
    }
}

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMessaging<TMessage>()
            where TMessage : notnull
        {
            return services.AddMessaging<TMessage>(_ => { });
        }

        public IServiceCollection AddMessaging<TMessage>(
            Action<MessagingBuilder<TMessage>> configure)
            where TMessage : notnull
        {
            services.AddScoped<IMessageService<TMessage>, MessageService<TMessage>>();

            var builder = new MessagingBuilder<TMessage>(services);
            configure(builder);

            return services;
        }

        public IServiceCollection AddHostedMessaging<TMessage>()
            where TMessage : notnull
        {
            services.AddHostedService<MessageServiceHost<TMessage>>();
            return services;
        }
    }
}
