using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

public sealed partial class BackgroundQueue<T>
{
    public sealed class ConsumerBuilder
    {
        private Func<IServiceProvider, IAsyncEnumerable<T>>? _readerFactory;

        public ConsumerBuilder(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public MiddlewareStackBuilder<T> MiddlewareStackBuilder { get; } = new();

        public ConsumerBuilder SetReaderFactory(Func<IServiceProvider, IAsyncEnumerable<T>> factory)
        {
            _readerFactory = factory;

            return this;
        }

        private HandlerFactory<T> BuildHandlerFactory() =>
            MiddlewareStackBuilder.Build().Resolve;

        internal BackgroundServiceSupervisor Build(IServiceProvider provider)
        {
            // TODO Custom exception
            var readerFactory = _readerFactory ?? throw new Exception($"Reader factory is required");

            var executor = ActivatorUtilities.CreateInstance<BoundedExecutor>(provider, Name);
            var consumer = ActivatorUtilities.CreateInstance<BackgroundQueueConsumer<T>>(provider, Name,
                executor, readerFactory(provider), BuildHandlerFactory());
            var supervisor = ActivatorUtilities
                .CreateInstance<BackgroundServiceSupervisor<BackgroundQueueConsumer<T>>>(provider, consumer);

            return supervisor;
        }
    }
}
