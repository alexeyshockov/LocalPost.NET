using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

internal static class ServiceProviderLookups
{
    public static T GetRequiredService<T>(this IServiceProvider provider, string name)
        where T : INamedService =>
        provider.GetServices<T>().First(service => service.Name == name);

    // public static T GetRequiredServiceFor<T>(this IServiceProvider provider, string target)
    //     where T : IAssistantService =>
    //     provider.GetServices<T>().First(service => service.Target == target);

    public static T GetOptions<T>(this IServiceProvider provider) where T : class =>
        provider.GetRequiredService<IOptions<T>>().Value;

    public static T GetOptions<T>(this IServiceProvider provider, string name) where T : class =>
        provider.GetRequiredService<IOptionsMonitor<T>>().Get(name);

    public static ILogger<T> GetLoggerFor<T>(this IServiceProvider provider) =>
        provider.GetRequiredService<ILogger<T>>();

    public static BackgroundServiceRunner GetBackgroundServiceRunner<T>(this IServiceProvider provider)
        where T : IBackgroundService =>
        provider.GetRequiredService<BackgroundServices>().Runners
            .First(runner => runner.Service is T);

    public static BackgroundServiceRunner GetBackgroundServiceRunner<T>(this IServiceProvider provider,
        string name)
        where T : IBackgroundService, INamedService =>
        provider.GetRequiredService<BackgroundServices>().Runners
            .First(runner => runner.Service is T s && s.Name == name);

    // public static BackgroundServiceRunner GetBackgroundServiceRunnerFor<T>(this IServiceProvider provider,
    //     string target)
    //     where T : IBackgroundService, IAssistantService =>
    //     provider.GetRequiredService<BackgroundServices>().Runners
    //         .First(runner => runner.Service is T s && s.Target == target);

    public static IBackgroundServiceMonitor GetPipelineMonitorFor<T>(this IServiceProvider provider)
    {
        var target = AssistedService.From<T>();
        var services = provider.GetRequiredService<BackgroundServices>();
        var runners = services.Runners
            .Where(runner => runner.Service is IStreamRunner pr && pr.Target == target)
            .ToArray();

        return new BackgroundServicesMonitor(runners);
    }

    public static IBackgroundServiceMonitor GetPipelineMonitorFor<T>(this IServiceProvider provider, string name)
        where T : INamedService
    {
        var target = AssistedService.From<T>(name);
        var services = provider.GetRequiredService<BackgroundServices>();
        var runners = services.Runners
            .Where(runner => runner.Service is IStreamRunner pr && pr.Target == target)
            .ToArray();

        return new BackgroundServicesMonitor(runners);
    }

    public static IEnumerable<IStreamRunner> GetPipelineRunnersFor<T>(this IServiceProvider provider)
    {
        var target = AssistedService.From<T>();
        return provider.GetServices<IBackgroundService>()
            .OfType<IStreamRunner>()
            .Where(runner => runner.Target == target);
    }

    public static IEnumerable<IStreamRunner> GetPipelineRunnersFor<T>(this IServiceProvider provider, string name)
        where T : INamedService
    {
        var target = AssistedService.From<T>(name);
        return provider.GetServices<IBackgroundService>()
            .OfType<IStreamRunner>()
            .Where(runner => runner.Target == target);
    }
}
