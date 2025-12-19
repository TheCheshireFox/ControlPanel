using Microsoft.Extensions.Options;

namespace ControlPanel.Bridge.Extensions;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddOption<T>(this IServiceCollection services, T option) where T : class
    {
        return services.AddSingleton<IOptions<T>>(new OptionsWrapper<T>(option));
    }
}