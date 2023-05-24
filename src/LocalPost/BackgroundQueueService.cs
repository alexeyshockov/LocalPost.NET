using LocalPost.AsyncEnumerable;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

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

        var consumers = Enumerable.Range(1, options.MaxConcurrency)
            .Select(_ =>
            {
                var consumer = new BackgroundQueue<T>.Consumer(queue, handler);
                var supervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider,
                    Name, consumer);

                return supervisor;
            });

        return new BackgroundQueueService<T>(options, queue, consumers);
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

        HandlerFactory<TOut> handlerFactory = handlerStack.Resolve;
        Handler<TOut> handler = ActivatorUtilities.CreateInstance<ScopedHandler<TOut>>(provider,
            Name, handlerFactory).InvokeAsync;

        // Just a single consumer, to do the batching properly
        var batchSupervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider,
            // A different name for the batching consumer?..
            Name, new BackgroundQueue<T>.BatchConsumer<TOut>(queue, batchQueue, batchFactory));

        // And the actual consumers, to process the batches
        var consumers = Enumerable.Range(1, options.MaxConcurrency)
            .Select(_ =>
            {
                var consumer = new BackgroundQueue<TOut>.Consumer(batchQueue, handler);
                var supervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor>(provider,
                    Name, consumer);

                return supervisor;
            }).Prepend(batchSupervisor);

        return new BackgroundQueueService<T>(options, queue, consumers);
    }

    public BackgroundQueueService(BackgroundQueueOptions<T> options, BackgroundQueue<T> queue,
        IEnumerable<IBackgroundServiceSupervisor> consumers)
    {
        Options = options;

        Queue = queue;
        var queueSupervisor = new BackgroundQueue<T>.Supervisor(queue);

        var consumerGroup = new IBackgroundServiceSupervisor.Combined(consumers);

        _consumerGroupReadinessCheck = new IBackgroundServiceSupervisor.ReadinessCheck(consumerGroup);
        _consumerGroupLivenessCheck = new IBackgroundServiceSupervisor.LivenessCheck(consumerGroup);

        Supervisor = new CombinedHostedService(queueSupervisor, consumerGroup);
    }

    public BackgroundQueueOptions<T> Options { get; }

    public IBackgroundQueue<T> Queue { get; }

    // Expose only the root supervisor to the host, to avoid deadlocks (.NET runtime handles background services
    // synchronously by default, so if consumers are stopped first, they will block the reader from completing the
    // channel).
    public IHostedService Supervisor { get; }

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
