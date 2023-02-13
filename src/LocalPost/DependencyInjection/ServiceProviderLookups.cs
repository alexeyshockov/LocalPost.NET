using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

internal static class ServiceProviderLookups
{
    public static T GetRequiredService<T>(this IServiceProvider provider, string name)
        where T : INamedService
    {
        return provider.GetRequiredService<IEnumerable<T>>().First(x => x.Name == name);
    }

    public static BackgroundServiceSupervisor<T> GetSupervisor<T>(this IServiceProvider provider, string name)
        where T : class, IBackgroundService => provider.GetRequiredService<BackgroundServiceSupervisor<T>>(name);
}
