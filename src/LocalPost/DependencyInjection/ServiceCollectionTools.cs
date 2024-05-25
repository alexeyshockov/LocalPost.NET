using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost.DependencyInjection;

internal static class ServiceCollectionTools
{
    public static void AddConcurrentHostedServices(this IServiceCollection services) => services
        .AddHostedService<ConcurrentHostedServices>();

    public static void AddBackgroundServiceForNamed<T>(this IServiceCollection services, string name)
        where T : class, IBackgroundService, INamedService
    {
        services.AddConcurrentHostedServices();

        // We DO expect that this service is registered by the user already...
//        services.AddSingleton<T>();
//        services.AddSingleton<IBackgroundService, T>();

        var added = services.TryAddNamedSingleton<NamedBackgroundServiceRunner<T>>(name, provider =>
            new NamedBackgroundServiceRunner<T>(provider.GetRequiredService<T>(name),
                provider.GetRequiredService<IHostApplicationLifetime>()));
        if (!added)
            return;

        services.AddSingleton<IConcurrentHostedService>(provider =>
            provider.GetRequiredService<NamedBackgroundServiceRunner<T>>(name));
        services.AddSingleton<IBackgroundServiceMonitor>(provider =>
            provider.GetRequiredService<NamedBackgroundServiceRunner<T>>(name));
    }

    public static void AddBackgroundServiceFor<T>(this IServiceCollection services)
        where T : class, IBackgroundService
    {
        services.AddConcurrentHostedServices();

        // We DO expect that this service is registered by the user already...
//        services.AddSingleton<T>();
//        services.AddSingleton<IBackgroundService, T>();

        var added = services.TryAddSingleton<BackgroundServiceRunner<T>>(provider =>
            new BackgroundServiceRunner<T>(provider.GetRequiredService<T>(),
                provider.GetRequiredService<IHostApplicationLifetime>()));
        if (!added)
            return;

        services.AddSingleton<IConcurrentHostedService>(provider =>
            provider.GetRequiredService<BackgroundServiceRunner<T>>());
        services.AddSingleton<IBackgroundServiceMonitor>(provider =>
            provider.GetRequiredService<BackgroundServiceRunner<T>>());
    }

    public static bool TryAddNamedSingleton<TService>(this IServiceCollection services, string name,
        Func<IServiceProvider, TService> implementationFactory) where TService : class, INamedService =>
        services.TryAdd(NamedServiceDescriptor.Singleton(name, implementationFactory));

    public static bool TryAddSingleton<TService>(this IServiceCollection services) where TService : class =>
        services.TryAdd(ServiceDescriptor.Singleton<TService, TService>());

    public static bool TryAddSingleton<TService>(this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory) where TService : class =>
        services.TryAdd(ServiceDescriptor.Singleton(implementationFactory));

    // "If binary compatibility were not a problem, then the TryAdd methods could return bool"
    // from https://github.com/dotnet/runtime/issues/45114#issuecomment-733807639
    // See also: https://github.com/dotnet/runtime/issues/44728#issuecomment-831413792
    public static bool TryAdd(this IServiceCollection services, ServiceDescriptor descriptor)
    {
        if (services.Any(service => IsEqual(service, descriptor)))
            return false;

        services.Add(descriptor);
        return true;

        static bool IsEqual(ServiceDescriptor a, ServiceDescriptor b)
        {
            var equal = a.ServiceType == b.ServiceType; // && a.Lifetime == b.Lifetime;
            if (equal && a is NamedServiceDescriptor namedA && b is NamedServiceDescriptor namedB)
                return namedA.Name == namedB.Name;

            return equal;
        }
    }

    public static IServiceCollection AddSingletonAlias<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.AddSingleton<TService>(provider => provider.GetRequiredService<TImplementation>());

    public static IServiceCollection AddSingletonAlias<TService, TImplementation>(this IServiceCollection services,
        string name)
        where TService : class
        where TImplementation : class, TService, INamedService =>
        services.AddSingleton<TService>(provider => provider.GetRequiredService<TImplementation>(name));

    public static bool TryAddSingletonAlias<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService =>
        services.TryAddSingleton<TService>(provider => provider.GetRequiredService<TImplementation>());

    public static bool TryAddSingletonAlias<TService, TImplementation>(this IServiceCollection services,
        string name)
        where TService : class
        where TImplementation : class, TService, INamedService =>
        services.TryAddSingleton<TService>(provider => provider.GetRequiredService<TImplementation>(name));
}
