using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace LocalPost.DependencyInjection;

internal static class ConsumerGroupRegistration
{
    internal static bool TryAddConsumerGroup<T, TQ>(this IServiceCollection services, string name,
        HandlerFactory<T> configure) where TQ : IAsyncEnumerable<T>, INamedService
    {
//        services.TryAddConsumerGroup(name, provider => BackgroundQueue.ConsumerGroupFor(
//            provider.GetRequiredService<BatchMessageSource>(name), configure(provider), 1));
        if (!services.TryAddNamedSingleton(name, provider => BackgroundQueue.ConsumerGroupForNamed(
                provider.GetRequiredService<TQ>(name), configure(provider), 1))) // FIXME Config
            return false;

        services.AddBackgroundServiceForNamed<BackgroundQueue.NamedConsumerGroup<TQ, T>>(name);

        return true;
    }

    internal static bool TryAddConsumerGroup<T, TQ>(this IServiceCollection services,
        HandlerFactory<T> configure) where TQ : IAsyncEnumerable<T>
    {
        if (!services.TryAddSingleton(provider => BackgroundQueue.ConsumerGroupFor(
                provider.GetRequiredService<TQ>(), configure(provider), 1))) // FIXME Config
            return false;

        services.AddBackgroundServiceFor<BackgroundQueue.ConsumerGroup<TQ, T>>();

        return true;
    }
}

internal static class HealthChecksRegistration
{
    public static IHealthChecksBuilder AddConsumerGroupLivenessCheck<TQ, T>(this IHealthChecksBuilder builder,
        string? name = default, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default)
        where TQ : IAsyncEnumerable<T>
    {
        var check = HealthChecks.LivenessCheckFor<BackgroundQueue.ConsumerGroup<TQ, T>>(failureStatus, tags);
        if (name is not null)
            check.Name = name;

        return builder.Add(check);
    }

    public static IHealthChecksBuilder AddNamedConsumerGroupLivenessCheck<TQ, T>(this IHealthChecksBuilder builder,
        string name, HealthStatus? failureStatus = default, IEnumerable<string>? tags = default)
        where TQ : IAsyncEnumerable<T>, INamedService
    {
        var check = HealthChecks.LivenessCheckForNamed<BackgroundQueue.NamedConsumerGroup<TQ, T>>(name, failureStatus, tags);

        return builder.Add(check);
    }
}

internal static class Registration
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
}
