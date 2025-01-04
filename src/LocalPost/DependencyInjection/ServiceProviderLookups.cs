using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

internal static class ServiceProviderLookups
{
    public static T GetOptions<T>(this IServiceProvider provider) where T : class =>
        provider.GetRequiredService<IOptions<T>>().Value;

    public static T GetOptions<T>(this IServiceProvider provider, string name) where T : class =>
        provider.GetRequiredService<IOptionsMonitor<T>>().Get(name);

    public static ILogger<T> GetLoggerFor<T>(this IServiceProvider provider) =>
        provider.GetRequiredService<ILogger<T>>();
}
