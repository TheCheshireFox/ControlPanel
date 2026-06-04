using Microsoft.Extensions.DependencyInjection;

namespace ControlPanel.Shared;

public static class ScopedProxyExtensions
{
    private class Proxy<T>
    {
        public T? Value { get; set; }
    }
    
    extension(IServiceCollection services)
    {
        public IServiceCollection AddScopedProxy<T>() where T : class
        {
            return services
                .AddScoped<Proxy<T>>()
                .AddScoped<T>(sp => sp.GetRequiredService<Proxy<T>>().Value ?? throw new NullReferenceException());
        }
    }
    
    extension(IServiceProvider serviceProvider)
    {
        public void SetScopedProxy<T>(T value) where T : class
        {
            serviceProvider.GetRequiredService<Proxy<T>>().Value = value;
        }
    }
}