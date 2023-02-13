using Azure.Storage.Queues.Models;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace LocalPost.Azure.QueueConsumer.DependencyInjection;

public static class ServiceRegistration
{
    public static OptionsBuilder<ConsumerOptions> AddAzureQueueConsumer<THandler>(this IServiceCollection services,
        string name) where THandler : IHandler<QueueMessage> =>
        services
            .AddAzureQueueConsumer(name, provider => provider.GetRequiredService<THandler>().InvokeAsync);

    public static OptionsBuilder<ConsumerOptions> AddAzureQueueConsumer(this IServiceCollection services,
        string name, Func<IServiceProvider, Handler<QueueMessage>> handlerFactory)
    {
        // Expect Azure QueueServiceClient to be registered in the DI container using the usual way,
        // see https://learn.microsoft.com/en-us/dotnet/azure/sdk/dependency-injection#register-client
        services.TryAddSingleton<IAzureQueues, AzureQueues>();

        services.TryAddSingleton<MessagePullerAccessor>();
        services.AddSingleton(provider => ActivatorUtilities.CreateInstance<MessagePuller>(provider, name));

        services
            .AddCustomBackgroundQueue($"AzureQueue/{name}",
                provider => provider.GetQueue(name),
                provider => provider.GetQueue(name).Wrap(handlerFactory(provider)))
            .Configure<IOptionsMonitor<ConsumerOptions>>(
                (options, consumerOptions) => { options.MaxConcurrency = consumerOptions.Get(name).MaxConcurrency; });

        // TODO Health check, metrics

        return services.AddOptions<ConsumerOptions>(name).Configure(options => options.QueueName = name);
    }
}
