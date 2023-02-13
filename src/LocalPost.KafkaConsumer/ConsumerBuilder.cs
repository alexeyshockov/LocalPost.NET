using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer;

public static partial class Consumer<TKey, TValue>
{
    public sealed class Builder
    {
        private Action<ConsumerBuilder<TKey, TValue>> _configure = (cb) => { };

        private Handler<ConsumeException> _errorHandler = (e, ct) => Task.CompletedTask;

        private Handler<ConsumeContext<TKey, TValue>> _handler = (c, ct) => Task.CompletedTask;

        private readonly List<MiddlewareFactory<ConsumeContext<TKey, TValue>>> _middlewares = new();

        private readonly Channel<ConsumeContext<TKey, TValue>> _queue =
            // Kafka client (librdkafka) is optimised to prefetch messages, so there is no need to maintain our own
            // buffer
            Channel.CreateBounded<ConsumeContext<TKey, TValue>>(new BoundedChannelOptions(1)
            {
                SingleWriter = true,
                SingleReader = true
                // TODO AllowSynchronousContinuations?..
            });

        public required string Name { get; init; }

        public Builder ConfigureKafkaClient(Action<ConsumerBuilder<TKey, TValue>> configure)
        {
            _configure = configure;

            return this;
        }

        // TODO Remove
        public Builder SetErrorHandler(Handler<ConsumeException> handler)
        {
            _errorHandler = handler;

            return this;
        }

        public Builder SetMessageHandler(Handler<Message<TKey, TValue>> handler)
        {
            _handler = (c, ct) => handler(c.Result.Message, ct);

            return this;
        }

        // FIXME Take from the container...
//        public Builder SetMessageHandler<THandler>(THandler handler)
//            where THandler : IMessageHandler<Message<TKey, TValue>> =>
//            SetMessageHandler(handler.Process);

        public Builder SetMessageHandler(Handler<TValue> handler) =>
            SetMessageHandler((m, ct) => handler(m.Value, ct));

        public Builder AddMiddleware(MiddlewareFactory<ConsumeContext<TKey, TValue>> factory)
        {
            _middlewares.Add(factory);

            return this;
        }

        public Builder AddMiddleware(Middleware<ConsumeContext<TKey, TValue>> middleware) =>
            AddMiddleware(_ => middleware);

        internal HandlerFactory<ConsumeContext<TKey, TValue>> BuildHandlerFactory() =>
            new MiddlewareStack<ConsumeContext<TKey, TValue>>(_handler, _middlewares).Resolve;

        internal IAsyncEnumerable<ConsumeContext<TKey, TValue>> Messages => _queue.Reader.ReadAllAsync();

        internal Service Build(IServiceProvider provider)
        {
            var clientConfig = provider.GetRequiredService<IOptionsMonitor<ConsumerOptions>>().Get(Name);

            var clientBuilder = new ConsumerBuilder<TKey, TValue>(clientConfig);
            _configure(clientBuilder);

            var kafkaClient = clientBuilder.Build();

            return ActivatorUtilities.CreateInstance<Service>(provider, clientConfig, Name, kafkaClient, _errorHandler);
        }
    }
}
