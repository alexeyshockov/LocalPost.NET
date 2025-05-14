using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.SQS;
using LocalPost.Flow;
using LocalPost.SqsConsumer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.LocalStack;

namespace LocalPost.SqsConsumer.Tests;

public class ConsumerTests(ITestOutputHelper output) : IAsyncLifetime
{
    // Called for each test, since each test instantiates a new class instance
    private readonly LocalStackContainer _container = new LocalStackBuilder()
        .WithImage("localstack/localstack:4")
        .WithEnvironment("SERVICES", "sqs")
        .Build();

    private readonly AWSCredentials _credentials = new BasicAWSCredentials("test", "test");

    private const string QueueName = "weather-forecasts";

    private string? _queueUrl;

    private IAmazonSQS CreateClient() => new AmazonSQSClient(_credentials,
        new AmazonSQSConfig { ServiceURL = _container.GetConnectionString() });

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var sqs = CreateClient();
        var createResponse = await sqs.CreateQueueAsync(QueueName);
        _queueUrl = createResponse.QueueUrl;
    }

    public Task DisposeAsync() => _container.StopAsync();

    [Fact]
    public async Task handles_messages()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var received = new List<string>();

        var sqs = hostBuilder.Services
            .AddDefaultAWSOptions(new AWSOptions()
            {
                DefaultClientConfig = { ServiceURL = _container.GetConnectionString() },
                Credentials = _credentials,
            })
            .AddAWSService<IAmazonSQS>()
            .AddSqsConsumers();
        sqs.AddConsumer(QueueName,
            HandlerStack.For<string>(payload => received.Add(payload))
                .Scoped()
                .UseSqsPayload()
                .Trace()
                .LogExceptions()
                .Acknowledge() // Acknowledge in any case, because we caught any possible exceptions before
        );

        var host = hostBuilder.Build();

        await host.StartAsync();

        var sqsClient = CreateClient();
        await sqsClient.SendMessageAsync(_queueUrl, "It will rainy in London tomorrow");

        await Task.Delay(1_000); // "App is working"

        received.Should().HaveCount(1);
        received[0].Should().Be("It will rainy in London tomorrow");

        await host.StopAsync();
    }

    [Fact]
    public async Task handles_batches()
    {
        var hostBuilder = Host.CreateApplicationBuilder();

        var received = new List<IReadOnlyCollection<string>>();

        var sqs = hostBuilder.Services
            .AddDefaultAWSOptions(new AWSOptions()
            {
                DefaultClientConfig = { ServiceURL = _container.GetConnectionString() },
                Credentials = _credentials,
            })
            .AddAWSService<IAmazonSQS>()
            .AddSqsConsumers();
        sqs.AddConsumer(QueueName,
            HandlerStack.For<IReadOnlyCollection<string>>(payload => received.Add(payload))
                .Scoped()
                .UseSqsPayload()
                .Trace()
                .LogExceptions()
                .Acknowledge() // Will acknowledge in any case, as we already caught all the exceptions before
                .Batch(10, TimeSpan.FromSeconds(1)));

        var host = hostBuilder.Build();

        await host.StartAsync();

        var sqsClient = CreateClient();
        await sqsClient.SendMessageAsync(_queueUrl, "It will be rainy in London tomorrow");
        await sqsClient.SendMessageAsync(_queueUrl, "It will be sunny in Paris tomorrow");

        await Task.Delay(3_000); // "App is working"

        received.Should().HaveCount(1);
        received[0].Should().BeEquivalentTo(
            "It will be rainy in London tomorrow",
            "It will be sunny in Paris tomorrow");

        await host.StopAsync();
    }
}
