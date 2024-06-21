using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

public static partial class ServiceCollectionEx
{
    internal static RegistrationContext RegistrationContextFor<T>(this IServiceCollection services) =>
        new(services, AssistedService.From<T>());

    internal static RegistrationContext RegistrationContextFor<T>(this IServiceCollection services, string name)
        where T : INamedService =>
        new(services, AssistedService.From<T>(name));

    // internal static bool TryAddBackgroundConsumer<T, TQ>(this IServiceCollection services, string name,
    //     HandlerFactory<T> hf, Func<IServiceProvider, ConsumerOptions> of)
    //     where TQ : IAsyncEnumerable<T>, INamedService
    // {
    //     if (!services.TryAddNamedSingleton(name, CreateConsumer))
    //         return false;
    //
    //     services.AddBackgroundService<Queue.NamedConsumer<TQ, T>>(name);
    //
    //     return true;
    //
    //     Queue.NamedConsumer<TQ, T> CreateConsumer(IServiceProvider provider)
    //     {
    //         var options = of(provider);
    //         var handler = hf(provider);
    //
    //         return new Queue.NamedConsumer<TQ, T>(
    //             provider.GetRequiredService<ILogger<Queue.ConsumerBase<T>>>(),
    //             provider.GetRequiredService<TQ>(name), handler, options.MaxConcurrency)
    //         {
    //             BreakOnException = options.BreakOnException
    //         };
    //     }
    // }
    //
    // internal static bool TryAddBackgroundConsumer<T, TQ>(this IServiceCollection services,
    //     HandlerFactory<T> hf, Func<IServiceProvider, ConsumerOptions> of)
    //     where TQ : IAsyncEnumerable<T>
    // {
    //     if (!services.TryAddSingleton(CreateConsumer))
    //         return false;
    //
    //     services.AddBackgroundService<Queue.Consumer<TQ, T>>();
    //
    //     return true;
    //
    //     Queue.Consumer<TQ, T> CreateConsumer(IServiceProvider provider)
    //     {
    //         var options = of(provider);
    //         var handler = hf(provider);
    //
    //         return new Queue.Consumer<TQ, T>(
    //             provider.GetRequiredService<ILogger<Queue.ConsumerBase<T>>>(),
    //             provider.GetRequiredService<TQ>(), handler, options.MaxConcurrency)
    //         {
    //             BreakOnException = options.BreakOnException
    //         };
    //     }
    // }

    // Just register a background service directly
    // internal static IServiceCollection AddBackgroundPipeline<T>(this IServiceCollection services, string target,
    //     IAsyncEnumerable<T> stream, PipelineConsumer<T> consume) => ...
}
