using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.SqsConsumer;

public static partial class MessageSource
{
    public sealed class Builder
    {
        private Channel<ConsumeContext> _queue = Channel.CreateBounded<ConsumeContext>(new BoundedChannelOptions(config.BufferSize)
            {
                SingleWriter = true,
                SingleReader = true
            });

        public Builder(string name)
        {
            Name = name;
            MiddlewareStackBuilder.Append<Middleware>();
        }

        public string Name { get; }

        public MiddlewareStackBuilder<ConsumeContext> MiddlewareStackBuilder { get; } = new();

        internal HandlerFactory<ConsumeContext> BuildHandlerFactory() =>
            MiddlewareStackBuilder.Build().Resolve;

        internal BackgroundServiceSupervisor Build(IServiceProvider provider)
        {
            var client = ActivatorUtilities.CreateInstance<QueueClient>(provider, Name);
            var consumer = ActivatorUtilities.CreateInstance<Service>(provider, Name, client);

            var supervisor = ActivatorUtilities
                .CreateInstance<BackgroundServiceSupervisor<Service>>(provider, consumer);

            return supervisor;
        }
    }
}
