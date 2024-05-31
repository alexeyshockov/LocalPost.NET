using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.SQS;
using LocalPost.SqsConsumer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.LocalStack;
using Xunit.Abstractions;

namespace LocalPost.SqsConsumer.Tests;

public class ConsumerTests(ITestOutputHelper output) : IAsyncLifetime
{
    // Called for each test, since each test instantiates a new class instance
    private readonly LocalStackContainer _container = new LocalStackBuilder()
        .WithImage("localstack/localstack:3.4")
        .WithEnvironment("SERVICES", "sqs")
        .Build();


    private const string QueueName = "weather-forecasts";

    private IAmazonSQS _sqs;

    private string? _queueUrl;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _sqs = new AmazonSQSClient(new BasicAWSCredentials("LSIAQAAAAAAVNCBMPNSG", "any"),
            new AmazonSQSConfig { ServiceURL = _container.GetConnectionString() });

        var createResponse = await _sqs.CreateQueueAsync(QueueName);
        _queueUrl = createResponse.QueueUrl;
    }

    public Task DisposeAsync() => _container.StopAsync();

    [Fact]
    public async Task handles_messages()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var received = new List<string>();

        hostBuilder.Services
            .AddDefaultAWSOptions(new AWSOptions()
            {
                DefaultClientConfig = { ServiceURL = _container.GetConnectionString() },
                Credentials = new BasicAWSCredentials("LSIAQAAAAAAVNCBMPNSG", "any")
            })
            .AddAWSService<IAmazonSQS>()
            .AddSqsConsumers(sqs => sqs.AddConsumer(QueueName, HandlerStack.For<string>(async (payload, _) =>
                {
                    received.Add(payload);
                })
                .UseSqsPayload()
                .Acknowledge()
                .Scoped()
                .Trace()));

        var host = hostBuilder.Build();

        await host.StartAsync();

        await _sqs.SendMessageAsync(_queueUrl, "It will rainy in London tomorrow");

        await Task.Delay(1_000);

        received.Should().HaveCount(1);
        received[0].Should().Be("It will rainy in London tomorrow");

        await host.StopAsync();
    }
}
