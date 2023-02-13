using Amazon.SQS.Model;
using LocalPost;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

public static class ServiceRegistration
{
//    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsMinimalConsumer(this IServiceCollection services,
//        string name, Handler<Message> handler) =>
//        services.AddAmazonSqsConsumer(name, _ => handler);
//
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

    public static OptionsBuilder<ConsumerOptions> AddAmazonSqsConsumer(this IServiceCollection services,
        string name, Action<Consumer.Builder> configure)
    {
        var builder = new Consumer.Builder(name);
        configure(builder);
        services.AddHostedService(builder.Build);

        services.TryAddSingleton<Consumer.Middleware>();

        services
            .AddBackgroundQueueConsumer<ConsumeContext>(name, b => b
                .SetReaderFactory(provider => provider.GetRequiredService<Consumer.Service>(name).Messages)
                .MiddlewareStackBuilder.SetHandler(builder.BuildHandlerFactory()))
            .Configure<IOptionsMonitor<ConsumerOptions>>(
                (options, consumerOptions) => { options.MaxConcurrency = consumerOptions.Get(name).MaxConcurrency; });

        return services.AddOptions<ConsumerOptions>(name).Configure(options => options.QueueName = name);
    }

//    public static IHealthChecksBuilder AddAmazonSqsConsumerHealthCheck(this IHealthChecksBuilder builder)
//    {
//        // TODO Add a global one...
//    }
}
