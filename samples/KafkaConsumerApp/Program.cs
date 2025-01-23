using Confluent.Kafka;
using JetBrains.Annotations;
using LocalPost;
using LocalPost.KafkaConsumer;
using LocalPost.KafkaConsumer.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<HostOptions>(options =>
{
    options.ServicesStartConcurrently = true;
    options.ServicesStopConcurrently = true;
});

builder.Services
    .AddScoped<MessageHandler>()
    .AddKafkaConsumers(kafka =>
    {
        kafka.Defaults
            .Bind(builder.Configuration.GetSection("Kafka"))
            .ValidateDataAnnotations();
        kafka.AddConsumer("example-consumer-group",
                HandlerStack.From<MessageHandler, WeatherForecast>()
                    .UseKafkaPayload()
                    .Scoped()
                    .DeserializeJson()
                    .Trace()
                    // .Acknowledge()
                    .LogExceptions()
            )
            .Bind(builder.Configuration.GetSection("Kafka:Consumer"))
            .Configure(options =>
            {
                options.ClientConfig.AutoOffsetReset = AutoOffsetReset.Earliest;
                // options.ClientConfig.EnableAutoCommit = false; // DryRun
                // options.ClientConfig.EnableAutoOffsetStore = false; // Manually acknowledge every message
            })
            .ValidateDataAnnotations();
    });

await builder.Build().RunAsync();


[UsedImplicitly]
public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);

internal sealed class MessageHandler : IHandler<WeatherForecast>
{
    public ValueTask InvokeAsync(WeatherForecast payload, CancellationToken ct)
    {
        Console.WriteLine(payload);
        return ValueTask.CompletedTask;
    }
}
