using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

internal static class ServiceProviderLookups
{
    public static T GetRequiredService<T>(this IServiceProvider provider, string name)
        where T : INamedService =>
        provider.GetRequiredService<IEnumerable<T>>().First(x => x.Name == name);

    public static T GetOptions<T>(this IServiceProvider provider) => provider.GetOptions<T>(Options.DefaultName);

    public static T GetOptions<T>(this IServiceProvider provider, string name) =>
        provider.GetRequiredService<IOptionsMonitor<T>>().Get(name);

    public static ILogger<T> GetLoggerFor<T>(this IServiceProvider provider) =>
        provider.GetRequiredService<ILogger<T>>();
}
