using Confluent.Kafka;
using LocalPost;
using LocalPost.KafkaConsumer;
using LocalPost.KafkaConsumer.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddScoped<MessageHandler>()
    .AddKafkaConsumers(kafka =>
    {
        // kafka.Defaults.Configure(options =>
        // {
        //     options.BootstrapServers = "localhost:9092";
        //     options.SecurityProtocol = SecurityProtocol.SaslSsl;
        //     options.SaslMechanism = SaslMechanism.Plain;
        //     options.SaslUsername = "admin";
        //     options.SaslPassword = "";
        // });
        kafka.Defaults
            .Bind(builder.Configuration.GetSection("Kafka"))
            .ValidateDataAnnotations();
        kafka.AddConsumer("weather-forecasts", HandlerStack.From<MessageHandler, WeatherForecast>()
                .UseKafkaPayload()
                .DeserializeJson()
                .Acknowledge()
                .Scoped()
                .Trace()
            )
            .Bind(builder.Configuration.GetSection("Kafka:Consumer"))
            .Configure(options =>
            {
                // options.Kafka.GroupId = "";
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                options.EnableAutoCommit = false; // TODO DryRun
            })
            .ValidateDataAnnotations();
    });

await builder.Build().RunAsync();



public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);

internal sealed class MessageHandler : IHandler<WeatherForecast>
{
    public async ValueTask InvokeAsync(WeatherForecast payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload);
    }
}
