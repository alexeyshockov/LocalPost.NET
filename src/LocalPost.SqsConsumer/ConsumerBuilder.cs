using System.Collections.Immutable;
using LocalPost.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost.SqsConsumer;

public static partial class Consumer
{
    public sealed class Builder
    {
        public Builder(string name)
        {
            Name = name;
            MiddlewareStackBuilder.Append<Middleware>();
        }

        public string Name { get; }

        // TODO Use...
        public Handler<Exception> ErrorHandler { get; set; } = (m, ct) => Task.CompletedTask;

        public MiddlewareStackBuilder<ConsumeContext> MiddlewareStackBuilder { get; } = new();

        internal HandlerFactory<ConsumeContext> BuildHandlerFactory() =>
            MiddlewareStackBuilder.Build().Resolve;

        internal IHostedService Build(IServiceProvider provider)
        {
            var client = ActivatorUtilities.CreateInstance<QueueClient>(provider, Name);
            var consumer = ActivatorUtilities.CreateInstance<Service>(provider, Name, client);

            var consumerSupervisor = ActivatorUtilities.CreateInstance<BackgroundServiceSupervisor<Service>>(provider,
                Name, consumer);

            return consumerSupervisor;
        }
    }
}
