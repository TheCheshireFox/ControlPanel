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
        public MessagingBuilder<TMessage> AddMessaging<TMessage>()
            where TMessage : notnull
        {
            services.AddScoped<IMessageService<TMessage>, MessageService<TMessage>>();

            return new MessagingBuilder<TMessage>(services);
        }

        public IServiceCollection AddHostedMessaging<TMessage>()
            where TMessage : notnull
        {
            services.AddHostedService<MessageServiceHost<TMessage>>();
            return services;
        }
    }
}
