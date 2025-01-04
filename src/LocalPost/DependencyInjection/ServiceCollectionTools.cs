using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

internal static class ServiceCollectionTools
{
    public static IEnumerable<object?> GetKeysFor<T>(this IServiceCollection services) => services
        // See https://github.com/dotnet/runtime/issues/95789#issuecomment-2274223124
        .Where(service => service.IsKeyedService && service.ServiceType == typeof(T))
        .Select(service => service.ServiceKey);

    public static bool TryAddKeyedSingleton<TService>(this IServiceCollection services, object key,
        Func<IServiceProvider, object?, TService> factory)
        // where TService : class, INamedService =>
        where TService : class =>
        services.TryAdd(ServiceDescriptor.KeyedSingleton(key, factory));

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

    public static IServiceCollection AddSingletonAlias<TAlias, TService>(this IServiceCollection services, object key)
        where TAlias : class
        where TService : class, TAlias =>
        services.AddKeyedSingleton<TAlias>(key, (provider, _) => provider.GetRequiredKeyedService<TService>(key));

    public static bool TryAddSingletonAlias<TAlias, TService>(this IServiceCollection services)
        where TAlias : class
        where TService : class, TAlias =>
        services.TryAddSingleton<TAlias>(provider => provider.GetRequiredService<TService>());

    public static bool TryAddSingletonAlias<TAlias, TService>(this IServiceCollection services, object key)
        where TAlias : class
        where TService : class, TAlias =>
        services.TryAddKeyedSingleton<TAlias>(key, (provider, _) => provider.GetRequiredKeyedService<TService>(key));
}
