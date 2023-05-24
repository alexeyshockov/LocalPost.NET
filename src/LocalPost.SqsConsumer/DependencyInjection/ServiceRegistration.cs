using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

public static class ServiceRegistration
{
    // TODO Implement
//    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsJsonConsumer<THandler, T>(this IServiceCollection services,
//        string name, Action<Consumer.Builder>? configure = null) where THandler : IHandler<T> =>
//        services.AddAmazonSqsConsumer(name, builder =>
//        {
//            builder.MiddlewareStackBuilder.SetHandler<THandler>();
//            configure?.Invoke(builder);
//        });

    public static OptionsBuilder<Options> AddAmazonSqsConsumer<THandler>(this IServiceCollection services,
        string name, Action<MiddlewareStackBuilder<ConsumeContext>>? configure = null)
        where THandler : IHandler<ConsumeContext> =>
        services.AddAmazonSqsConsumer(name, builder =>
        {
            builder.SetHandler<THandler>();
            configure?.Invoke(builder);
        });

//    public static OptionsBuilder<Options> AddAmazonSqsConsumer(this IServiceCollection services,
//        string name, Handler<ConsumeContext> handler, Action<MessageSource.Builder>? configure = null) =>
//        services.AddAmazonSqsConsumer(name, builder =>
//        {
//            builder.MiddlewareStackBuilder.SetHandler(handler);
//            configure?.Invoke(builder);
//        });

//    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsMinimalConsumer<TDep1>(this IServiceCollection services,
//        string name, Func<TDep1, Message, CancellationToken, Task> handler) where TDep1 : notnull =>
//        services.AddAmazonSqsConsumer(name, provider => (context, ct) =>
//        {
//            var dep1 = provider.GetRequiredService<TDep1>();
//
//            return handler(dep1, context, ct);
//        });
//
//    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsMinimalConsumer<TDep1, TDep2>(this IServiceCollection services,
//        string name, Func<TDep1, TDep2, Message, CancellationToken, Task> handler)
//        where TDep1 : notnull
//        where TDep2 : notnull =>
//        services.AddAmazonSqsConsumer(name, provider => (context, ct) =>
//        {
//            var dep1 = provider.GetRequiredService<TDep1>();
//            var dep2 = provider.GetRequiredService<TDep2>();
//
//            return handler(dep1, dep2, context, ct);
//        });
//
//    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsMinimalConsumer<TDep1, TDep2, TDep3>(this IServiceCollection services,
//        string name, Func<TDep1, TDep2, TDep3, Message, CancellationToken, Task> handler)
//        where TDep1 : notnull
//        where TDep2 : notnull
//        where TDep3 : notnull =>
//        services.AddAmazonSqsConsumer(name, provider => (context, ct) =>
//        {
//            var dep1 = provider.GetRequiredService<TDep1>();
//            var dep2 = provider.GetRequiredService<TDep2>();
//            var dep3 = provider.GetRequiredService<TDep3>();
//
//            return handler(dep1, dep2, dep3, context, ct);
//        });

//    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsConsumer<THandler>(this IServiceCollection services,
//        string name) where THandler : IMessageHandler<Message> =>
//        services
//            .AddAmazonSqsConsumer(name, provider => provider.GetRequiredService<THandler>().Process);

    public static OptionsBuilder<Options> AddAmazonSqsConsumer(this IServiceCollection services,
        string name, Action<MiddlewareStackBuilder<ConsumeContext>> configure)
    {
        services.TryAddConcurrentHostedServices();

        var handleStackBuilder = new MiddlewareStackBuilder<ConsumeContext>();
        services.TryAddSingleton<ProcessedMessageHandler>();
        handleStackBuilder.Append<ProcessedMessageHandler>();
        configure(handleStackBuilder);
        var handlerStack = handleStackBuilder.Build();

        services.TryAddSingleton(provider => SqsConsumerService.Create(provider, name, handlerStack));

        services.AddSingleton<IConcurrentHostedService>(provider =>
            provider.GetRequiredService<SqsConsumerService>(name).Reader);
        services.AddSingleton<IConcurrentHostedService>(provider =>
            provider.GetRequiredService<SqsConsumerService>(name).ConsumerGroup);

        // Extend ServiceDescriptor for better comparison and implement custom TryAddSingleton later...

        return services.AddOptions<Options>(name).Configure(options => options.QueueName = name);
    }
}
