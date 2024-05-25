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

        services.AddBackgroundServiceForNamed<BackgroundQueue.NamedConsumer<TQ, T>>(name);

        return true;

        BackgroundQueue.NamedConsumer<TQ, T> CreateConsumer(IServiceProvider provider)
        {
            var options = of(provider);
            var handler = hf(provider);

            return new BackgroundQueue.NamedConsumer<TQ, T>(
                provider.GetRequiredService<ILogger<BackgroundQueue.ConsumerBase<T>>>(),
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

        services.AddBackgroundServiceFor<BackgroundQueue.Consumer<TQ, T>>();

        return true;

        BackgroundQueue.Consumer<TQ, T> CreateConsumer(IServiceProvider provider)
        {
            var options = of(provider);
            var handler = hf(provider);

            return new BackgroundQueue.Consumer<TQ, T>(
                provider.GetRequiredService<ILogger<BackgroundQueue.ConsumerBase<T>>>(),
                provider.GetRequiredService<TQ>(), handler, options.MaxConcurrency)
            {
                BreakOnException = options.BreakOnException
            };
        }
    }
}
