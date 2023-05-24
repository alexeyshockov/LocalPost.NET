using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LocalPost;

internal sealed class BackgroundQueueService<T>
{
    public static readonly string Name = Reflection.FriendlyNameOf<T>();

    public static BackgroundQueueService<T> Create(IServiceProvider provider, MiddlewareStack<T> handlerStack)
    {
        var options = provider.GetOptions<BackgroundQueueOptions<T>>();

        var queue = new BackgroundQueue<T>(options);

        HandlerFactory<T> handlerFactory = handlerStack.Resolve;
        Handler<T> handler = ActivatorUtilities.CreateInstance<ScopedHandler<T>>(provider,
            Name, handlerFactory).InvokeAsync;

        var consumer = new BackgroundQueue<T>.Consumer(queue, handler);
        var consumerGroup = new ConsumerGroup(consumer.Run, options.MaxConcurrency);

        return new BackgroundQueueService<T>(queue, consumerGroup);
    }

    public static BackgroundQueueService<T> CreateBatched(IServiceProvider provider,
        MiddlewareStack<IReadOnlyList<T>> handlerStack,
        int maxBatchSize = 10, int batchCompletionTimeWindow = 1_000) => CreateBatched(provider, handlerStack,
        BoundedBatchBuilder<T>.Factory(maxBatchSize, batchCompletionTimeWindow));

    public static BackgroundQueueService<T> CreateBatched<TOut>(IServiceProvider provider,
        MiddlewareStack<TOut> handlerStack, BatchBuilderFactory<T, TOut> batchFactory)
    {
        var options = provider.GetOptions<BackgroundQueueOptions<T>>();

        var queue = new BackgroundQueue<T>(options);
        var batchQueue = new BackgroundQueue<TOut>(options);

        // Just a single consumer, to do the batching properly
        var consumer = new BackgroundQueue<T>.BatchConsumer<TOut>(queue, batchQueue, batchFactory);
        var consumerSupervisor = new ConsumerSupervisor(consumer.Run);

        HandlerFactory<TOut> handlerFactory = handlerStack.Resolve;
        Handler<TOut> handler = ActivatorUtilities.CreateInstance<ScopedHandler<TOut>>(provider,
            Name, handlerFactory).InvokeAsync;
        var batchConsumer = new BackgroundQueue<TOut>.Consumer(batchQueue, handler);
        var batchConsumerGroup = new ConsumerGroup(batchConsumer.Run, options.MaxConcurrency);

        return new BackgroundQueueService<T>(queue,
            new IBackgroundServiceSupervisor.Combined(consumerSupervisor, batchConsumerGroup));
    }

    public BackgroundQueueService(BackgroundQueue<T> queue, IBackgroundServiceSupervisor consumerGroup)
    {
        Queue = queue;
        QueueSupervisor = new BackgroundQueue<T>.Supervisor(queue);

        ConsumerGroup = consumerGroup;
        _consumerGroupReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(consumerGroup);
        _consumerGroupLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(consumerGroup);
    }

    public IBackgroundQueue<T> Queue { get; }

    public IConcurrentHostedService QueueSupervisor { get; }

    public IConcurrentHostedService ConsumerGroup { get; }
    private readonly IHealthCheck _consumerGroupReadinessCheck;
    private readonly IHealthCheck _consumerGroupLivenessCheck;

    public static HealthCheckRegistration ConsumerGroupReadinessCheck(HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(Name,
        provider => provider.GetRequiredService<BackgroundQueueService<T>>()._consumerGroupReadinessCheck,
        failureStatus,
        tags);

    public static HealthCheckRegistration ConsumerGroupLivenessCheck(HealthStatus? failureStatus = default,
        IEnumerable<string>? tags = default) => new(Name,
        provider => provider.GetRequiredService<BackgroundQueueService<T>>()._consumerGroupLivenessCheck,
        failureStatus,
        tags);

}
