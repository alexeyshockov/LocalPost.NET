using Amazon.SQS.Model;
using LocalPost;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.SqsConsumer.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static OptionsBuilder<SqsConsumerOptions> AddAmazonSqsMinimalConsumer(this IServiceCollection services,
        string name, MessageHandler<Message> handler) =>
        services.AddAmazonSqsConsumer(name, _ => handler);

    public static OptionsBuilder<SqsConsumerOptions> AddAmazonSqsMinimalConsumer<TDep1>(this IServiceCollection services,
        string name, Func<TDep1, Message, CancellationToken, Task> handler) where TDep1 : notnull =>
        services.AddAmazonSqsConsumer(name, provider => (context, ct) =>
        {
            var dep1 = provider.GetRequiredService<TDep1>();

            return handler(dep1, context, ct);
        });

    public static OptionsBuilder<SqsConsumerOptions> AddAmazonSqsMinimalConsumer<TDep1, TDep2>(this IServiceCollection services,
        string name, Func<TDep1, TDep2, Message, CancellationToken, Task> handler)
        where TDep1 : notnull
        where TDep2 : notnull =>
        services.AddAmazonSqsConsumer(name, provider => (context, ct) =>
        {
            var dep1 = provider.GetRequiredService<TDep1>();
            var dep2 = provider.GetRequiredService<TDep2>();

            return handler(dep1, dep2, context, ct);
        });

    public static OptionsBuilder<SqsConsumerOptions> AddAmazonSqsMinimalConsumer<TDep1, TDep2, TDep3>(this IServiceCollection services,
        string name, Func<TDep1, TDep2, TDep3, Message, CancellationToken, Task> handler)
        where TDep1 : notnull
        where TDep2 : notnull
        where TDep3 : notnull =>
        services.AddAmazonSqsConsumer(name, provider => (context, ct) =>
        {
            var dep1 = provider.GetRequiredService<TDep1>();
            var dep2 = provider.GetRequiredService<TDep2>();
            var dep3 = provider.GetRequiredService<TDep3>();

            return handler(dep1, dep2, dep3, context, ct);
        });

    public static OptionsBuilder<SqsConsumerOptions> AddAmazonSqsConsumer<THandler>(this IServiceCollection services,
        string name) where THandler : IMessageHandler<Message> =>
        services
            .AddAmazonSqsConsumer(name, provider => provider.GetRequiredService<THandler>().Process);

    public static OptionsBuilder<SqsConsumerOptions> AddAmazonSqsConsumer(this IServiceCollection services,
        string name, Func<IServiceProvider, MessageHandler<Message>> handlerFactory)
    {
        services.TryAddSingleton<IPostConfigureOptions<SqsConsumerOptions>, SqsConsumerOptionsResolver>();

        services.TryAddSingleton<SqsAccessor>();
        services.AddSingleton(provider => ActivatorUtilities.CreateInstance<SqsPuller>(provider, name));

        services
            .AddCustomBackgroundQueue($"SQS/{name}",
                provider => provider.GetSqs(name),
                provider => provider.GetSqs(name).Handler(handlerFactory(provider)))
            .Configure<IOptionsMonitor<SqsConsumerOptions>>(
                (options, sqsOptions) => { options.MaxConcurrency = sqsOptions.Get(name).MaxConcurrency; });

        services.TryAddSingleton<ProcessedMessagesHandler>();
        services
            .AddCustomBackgroundQueue($"SQS/{name}/ProcessedMessages",
                provider => provider.GetSqs(name).ProcessedMessages,
                provider => provider.GetRequiredService<ProcessedMessagesHandler>().Process)
            .Configure<IOptionsMonitor<SqsConsumerOptions>>(
                (options, sqsOptions) => { options.MaxConcurrency = sqsOptions.Get(name).MaxConcurrency; });

        // TODO Health check, metrics

        return services.AddOptions<SqsConsumerOptions>(name).Configure(options => options.QueueName = name);
    }
}
