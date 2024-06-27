using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

internal static class ServiceCollectionTools
{
//     public static void AddBackgroundServiceFor<T>(this IServiceCollection services, string name)
//         where T : class, IBackgroundService, IServiceFor
//     {
//         services.AddConcurrentHostedServices();
//
//         var added = services.TryAddNamedSingleton<BackgroundServiceRunner<T>>(name, provider =>
//             new BackgroundServiceRunner<T>(provider.GetRequiredService<T>(name),
//                 provider.GetRequiredService<IHostApplicationLifetime>()));
//         if (!added)
//             return;
//
//         services.AddSingletonAlias<IConcurrentHostedService, NamedBackgroundServiceRunner<T>>(name);
//     }
//
//     public static void AddBackgroundService<T>(this IServiceCollection services, string name)
//         where T : class, IBackgroundService, INamedService
//     {
//         services.AddConcurrentHostedServices();
//
//         var added = services.TryAddNamedSingleton<BackgroundServiceRunner<T>>(name, provider =>
//             new BackgroundServiceRunner<T>(provider.GetRequiredService<T>(name),
//                 provider.GetRequiredService<IHostApplicationLifetime>()));
//         if (!added)
//             return;
//
//         services.AddSingletonAlias<IConcurrentHostedService, NamedBackgroundServiceRunner<T>>(name);
//     }
//
//     public static void AddBackgroundService<T>(this IServiceCollection services)
//         where T : class, IBackgroundService
//     {
//         services.AddConcurrentHostedServices();
//
//         // We DO expect that this service is registered by the user...
// //        services.AddSingleton<T>();
// //        services.AddSingleton<IBackgroundService, T>();
//
//         var added = services.TryAddSingleton<BackgroundServiceRunner<T>>(provider =>
//             new BackgroundServiceRunner<T>(provider.GetRequiredService<T>(),
//                 provider.GetRequiredService<IHostApplicationLifetime>()));
//         if (!added)
//             return;
//
//         services.AddSingletonAlias<IConcurrentHostedService, BackgroundServiceRunner<T>>();
//
//
//         // FIXME Remove and check
//         // services.AddSingleton<IBackgroundServiceMonitor>(provider =>
//         //     provider.GetRequiredService<BackgroundServiceRunner<T>>());
//     }

    public static void AddBackgroundService(this IServiceCollection services,
        Func<IServiceProvider, IBackgroundService> factory) =>
        services.AddConcurrentHostedServices().AddSingleton(factory);

    public static void AddBackgroundService<T>(this IServiceCollection services)
        where T : class, IBackgroundService =>
        services.AddConcurrentHostedServices().AddSingletonAlias<IBackgroundService, T>();

    public static void AddBackgroundService<T>(this IServiceCollection services, string name)
        where T : class, IBackgroundService, INamedService =>
        services.AddConcurrentHostedServices().AddSingletonAlias<IBackgroundService, T>(name);

    public static IServiceCollection AddConcurrentHostedServices(this IServiceCollection services)
    {
        if (!services.TryAddSingleton<BackgroundServices>())
            return services;

        return services
            .AddHostedService<ConcurrentHostedServices>()
            .AddSingletonAlias<IConcurrentHostedService, BackgroundServices>();
    }

    public static bool TryAddNamedSingleton<TService>(this IServiceCollection services, string name,
        Func<IServiceProvider, TService> factory)
        where TService : class, INamedService =>
        services.TryAdd(ServiceDescriptor.KeyedSingleton(name, factory));

    public static bool TryAddSingleton<TService>(this IServiceCollection services) where TService : class =>
        services.TryAdd(ServiceDescriptor.Singleton<TService, TService>());

    public static bool TryAddSingleton<TService>(this IServiceCollection services,
        Func<IServiceProvider, TService> factory)
        where TService : class =>
        services.TryAdd(ServiceDescriptor.Singleton(factory));

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
            if (equal && a is { IsKeyedService: true } && b is { IsKeyedService: true })
                return a.ServiceKey == b.ServiceKey;

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
