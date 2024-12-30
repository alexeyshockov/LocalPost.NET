using Confluent.Kafka;
using LocalPost;
using LocalPost.KafkaConsumer;
using LocalPost.KafkaConsumer.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddScoped<MessageHandler>()
    .AddKafkaConsumers(kafka =>
    {
        kafka.Defaults
            .Bind(builder.Configuration.GetSection("Kafka"))
            .ValidateDataAnnotations();
        kafka.AddConsumer("example-consumer-group", HandlerStack.From<MessageHandler, WeatherForecast>()
                .UseKafkaPayload()
                .DeserializeJson()
                .Acknowledge()
                .Scoped()
                .Trace()
            )
            .Bind(builder.Configuration.GetSection("Kafka:Consumer"))
            .ConfigureConsumer(options =>
            {
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                // options.EnableAutoCommit = false; // TODO DryRun
            })
            .ValidateDataAnnotations();
    });

// TODO Health + Supervisor
var host = builder.Build();

await host.RunAsync();



public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);

internal sealed class MessageHandler : IHandler<WeatherForecast>
{
    public ValueTask InvokeAsync(WeatherForecast payload, CancellationToken ct)
    {
        Console.WriteLine(payload);
        return ValueTask.CompletedTask;
    }
}
