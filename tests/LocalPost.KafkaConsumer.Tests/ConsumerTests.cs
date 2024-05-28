using System.Text;
using Confluent.Kafka;
using LocalPost.KafkaConsumer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Redpanda;
using Xunit.Abstractions;

namespace LocalPost.KafkaConsumer.Tests;

public class ConsumerTests(ITestOutputHelper output) : IAsyncLifetime
{
    // Called for each test, since each test instantiates a new class instance
    private readonly RedpandaContainer _container = new RedpandaBuilder().Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        output.WriteLine(_container.Id);
    }

    public Task DisposeAsync() => _container.StopAsync();

    [Fact]
    public async Task handles_messages()
    {
        var hostBuilder = Host.CreateApplicationBuilder();
        hostBuilder.Services
            .AddKafkaConsumers(kafka =>
            {
                kafka.Defaults.Configure(options =>
                {
                    options.BootstrapServers = _container.GetBootstrapAddress();
                    // options.SecurityProtocol = SecurityProtocol.SaslSsl;
                    // options.SaslMechanism = SaslMechanism.Plain;
                    // options.SaslUsername = "admin";
                    // options.SaslPassword = "";
                });
                // kafka.Defaults
                //     .Bind(builder.Configuration.GetSection("Kafka"))
                //     .ValidateDataAnnotations();
                kafka.AddConsumer("weather-forecasts", HandlerStack.For<string>(async (payload, _) =>
                        {
                            output.WriteLine(payload);
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
                        options.Topic = "weather-forecasts";
                        options.GroupId = "test-consumer";
                        // options.AutoOffsetReset = AutoOffsetReset.Earliest;
                        // options.EnableAutoCommit = false; // TODO DryRun
                    })
                    .ValidateDataAnnotations();
            });

        var host = hostBuilder.Build();

        await host.StartAsync();

        using var producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _container.GetBootstrapAddress()
        }).Build();

        await producer.ProduceAsync("weather-forecasts", new Message<string, string>
        {
            Key = "London",
            Value = "It will rainy in London tomorrow"
        });

        await Task.Delay(1_000);

        Assert.True(true);

        await host.StopAsync();
    }
}


// public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);
