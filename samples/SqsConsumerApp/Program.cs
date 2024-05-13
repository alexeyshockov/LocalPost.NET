using Amazon.SQS;
using LocalPost;
using LocalPost.SqsConsumer;
using LocalPost.SqsConsumer.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddAWSService<IAmazonSQS>();
        services.AddScoped<SqsHandler>();
        services.AddAmazonSqsConsumer<SqsHandler>("test");
    })
    .Build();

host.Run();


// FIXME System.Text.Json deserializer...
internal class SqsHandler : IHandler<ConsumeContext>
{
    public async Task InvokeAsync(ConsumeContext payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload.Message.Body);
    }
}
