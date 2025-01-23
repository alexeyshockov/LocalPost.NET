using System.Text;
using Confluent.Kafka;
using LocalPost.KafkaConsumer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost.KafkaConsumer.Tests;

public class ConsumerTests(ITestOutputHelper output) : IAsyncLifetime
{
    // Called for each test, since each test instantiates a new class instance
    private readonly RedpandaContainer _container = new RedpandaBuilder()
        .Build();

    private const string Topic = "weather-forecasts";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _container.GetBootstrapAddress()
        }).Build();

        // Redpanda: by default, topic is created automatically on the first message
        await producer.ProduceAsync(Topic, new Message<string, string>
        {
            Key = "London",
            Value = "It will be rainy in London tomorrow"
        });
        await producer.ProduceAsync(Topic, new Message<string, string>
        {
            Key = "Paris",
            Value = "It will be sunny in Paris tomorrow"
        });
    }

    public async Task DisposeAsync()
    {
        await _container.StopAsync();
    }

    [Fact]
    public async Task handles_messages()
    {
        var received = new List<string>();

        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services.AddKafkaConsumers(kafka => kafka
            .AddConsumer("test-consumer",
                HandlerStack.For<string>(payload => received.Add(payload))
                    .Scoped()
                    .DecodeString()
                    .UseKafkaPayload()
                    .Acknowledge()
                    .Trace()
            )
            .Configure(co =>
            {
                co.ClientConfig.BootstrapServers = _container.GetBootstrapAddress();
                // Already set, see above
                // co.ClientConfig.GroupId = "test-consumer";
                co.Topics.Add(Topic);
                co.ClientConfig.EnableAutoOffsetStore = false; // Manually acknowledge every message
                // Otherwise the client attaches to the end of the topic, skipping all the published messages
                co.ClientConfig.AutoOffsetReset = AutoOffsetReset.Earliest;
            })
            .ValidateDataAnnotations());

        var host = hostBuilder.Build();

        try
        {
            await host.StartAsync();

            await Task.Delay(1_000); // "App is working"

            received.Should().HaveCount(2);
        }
        finally
        {
            await host.StopAsync();
        }
    }
}
