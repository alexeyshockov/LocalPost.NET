using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.KafkaConsumer;

public static partial class MessageSource<TKey, TValue>
{
    public sealed class Builder
    {
        private Action<ConsumerBuilder<TKey, TValue>> _configure = (cb) => { };

        private readonly Channel<ConsumeContext<TKey, TValue>> _queue =
            // Kafka client (librdkafka) is optimised to prefetch messages, so there is no need to maintain our own
            // buffer
            Channel.CreateBounded<ConsumeContext<TKey, TValue>>(new BoundedChannelOptions(1)
            {
                SingleWriter = true,
                SingleReader = true
            });

        public Builder(string name)
        {
            Name = name;
        }

        public string Name { get; }

        internal IAsyncEnumerable<ConsumeContext<TKey, TValue>> Messages => _queue.Reader.ReadAllAsync();

        public MiddlewareStackBuilder<ConsumeContext<TKey, TValue>> MiddlewareStackBuilder { get; } = new();

        // TODO Implement
//        public Builder SetMessageHandler(Handler<Message<TKey, TValue>> handler)
//        {
//            _handler = (c, ct) => handler(c.Result.Message, ct);
//
//            return this;
//        }

        // Allows to configure message format (Avro/JSON/Protobuf/etc.) and other things
        public Builder ConfigureKafkaClient(Action<ConsumerBuilder<TKey, TValue>> configure)
        {
            _configure = configure;

            return this;
        }

        internal HandlerFactory<ConsumeContext<TKey, TValue>> BuildHandlerFactory() =>
            MiddlewareStackBuilder.Build().Resolve;

        internal BackgroundServiceSupervisor Build(IServiceProvider provider)
        {
            var clientConfig = provider.GetRequiredService<IOptionsMonitor<Options>>().Get(Name);

            var clientBuilder = new ConsumerBuilder<TKey, TValue>(clientConfig.Kafka);
            _configure(clientBuilder);

            var kafkaClient = clientBuilder.Build();
            var consumer = ActivatorUtilities.CreateInstance<Service>(provider, Name, clientConfig, kafkaClient);

            var supervisor = ActivatorUtilities
                .CreateInstance<BackgroundServiceSupervisor<Service>>(provider, consumer);

            return supervisor;
        }
    }
}
