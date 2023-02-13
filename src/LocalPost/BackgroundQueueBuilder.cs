using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

public sealed partial class BackgroundQueue<T>
{
    public sealed class Builder
    {
        private Func<IServiceProvider, IAsyncEnumerable<T>>? _readerFactory;

        public Builder(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public MiddlewareStackBuilder<T> MiddlewareStackBuilder { get; } = new();

        public Builder SetReaderFactory(Func<IServiceProvider, IAsyncEnumerable<T>> factory)
        {
            _readerFactory = factory;

            return this;
        }

        private HandlerFactory<T> BuildHandlerFactory() =>
            MiddlewareStackBuilder.Build().Resolve;

        internal IHostedService Build(IServiceProvider provider)
        {
            // TODO Custom exception
            var readerFactory = _readerFactory ?? throw new Exception($"Reader factory is required");

            var executor = ActivatorUtilities.CreateInstance<BoundedExecutor>(provider, Name);
            var consumer = ActivatorUtilities.CreateInstance<BackgroundQueueConsumer<T>>(provider, Name,
                readerFactory(provider), executor, BuildHandlerFactory());
            var consumerSupervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor<BackgroundQueueConsumer<T>>>(provider, Name,
                consumer);

            return consumerSupervisor;
        }
    }
}
