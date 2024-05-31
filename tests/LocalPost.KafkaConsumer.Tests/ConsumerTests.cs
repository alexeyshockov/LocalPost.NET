using System.Text;
using Confluent.Kafka;
using LocalPost.KafkaConsumer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit.Abstractions;

namespace LocalPost.KafkaConsumer.Tests;

public class ConsumerTests(ITestOutputHelper output) : IAsyncLifetime
{
    // Called for each test, since each test instantiates a new class instance
    private readonly RpContainer _container = new RpBuilder()
        .Build();

    private const string Topic = "weather-forecasts";

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Dirty fix, but otherwise the client fails
        await Task.Delay(3_000);

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _container.GetBootstrapAddress()
        }).Build();

        // Redpanda: by default, topic is created automatically on the first message
        await producer.ProduceAsync(Topic, new Message<string, string>
        {
            Key = "London",
            Value = "It will rainy in London tomorrow"
        });
        await producer.ProduceAsync(Topic, new Message<string, string>
        {
            Key = "Paris",
            Value = "It will rainy in London tomorrow"
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
            .AddConsumer("test-one", HandlerStack.For<string>(async (payload, _) =>
                {
                    received.Add(payload);
                })
                .Map<byte[], string>(next => async (payload, ct) =>
                {
                    // TODO Support string payload out of the box?..
                    await next(Encoding.UTF8.GetString(payload), ct);
                })
                .UseKafkaPayload()
                .Acknowledge()
                .Scoped()
                .Trace()
            )
            .Configure(options =>
            {
                options.BootstrapServers = _container.GetBootstrapAddress();
                options.Topic = Topic;
                options.GroupId = "test-app";
                // Otherwise the client attaches to the end of the topic, skipping all the published messages
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
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
