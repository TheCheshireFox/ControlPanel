using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Shared.Messaging;

public class MessagingBuilder<TMessage>(
    IServiceCollection services)
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
        {
            return services.AddMessaging<TMessage>(_ => { });
        }

        public IServiceCollection AddMessaging<TMessage>(
            Action<MessagingBuilder<TMessage>> configure)
        {
            services.AddScoped<IMessageService<TMessage>, MessageService<TMessage>>();

            var builder = new MessagingBuilder<TMessage>(services);
            configure(builder);

            return services;
        }

        public IServiceCollection AddHostedMessaging<TMessage>()
        {
            services.AddHostedService<MessageServiceHost<TMessage>>();
            return services;
        }
    }
}
