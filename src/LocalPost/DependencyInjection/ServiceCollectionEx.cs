using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost.DependencyInjection;

public static partial class ServiceCollectionEx
{
    internal static bool TryAddBackgroundConsumer<T, TQ>(this IServiceCollection services, string name,
        HandlerFactory<T> hf, Func<IServiceProvider, ConsumerOptions> of)
        where TQ : IAsyncEnumerable<T>, INamedService
    {
        if (!services.TryAddNamedSingleton(name, CreateConsumer))
            return false;

        services.AddBackgroundServiceForNamed<Queue.NamedConsumer<TQ, T>>(name);

        return true;

        Queue.NamedConsumer<TQ, T> CreateConsumer(IServiceProvider provider)
        {
            var options = of(provider);
            var handler = hf(provider);

            return new Queue.NamedConsumer<TQ, T>(
                provider.GetRequiredService<ILogger<Queue.ConsumerBase<T>>>(),
                provider.GetRequiredService<TQ>(name), handler, options.MaxConcurrency)
            {
                BreakOnException = options.BreakOnException
            };
        }
    }

    internal static bool TryAddBackgroundConsumer<T, TQ>(this IServiceCollection services,
        HandlerFactory<T> hf, Func<IServiceProvider, ConsumerOptions> of)
        where TQ : IAsyncEnumerable<T>
    {
        if (!services.TryAddSingleton(CreateConsumer))
            return false;

        services.AddBackgroundServiceFor<Queue.Consumer<TQ, T>>();

        return true;

        Queue.Consumer<TQ, T> CreateConsumer(IServiceProvider provider)
        {
            var options = of(provider);
            var handler = hf(provider);

            return new Queue.Consumer<TQ, T>(
                provider.GetRequiredService<ILogger<Queue.ConsumerBase<T>>>(),
                provider.GetRequiredService<TQ>(), handler, options.MaxConcurrency)
            {
                BreakOnException = options.BreakOnException
            };
        }
    }
}
