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
        kafka.AddConsumer("one-and-the-only", HandlerStack.From<MessageHandler, WeatherForecast>()
                .UseKafkaPayload()
                .DeserializeJson()
                .Acknowledge()
                .Scoped()
                .Trace()
            )
            .Bind(builder.Configuration.GetSection("Kafka:Consumer"))
            .Configure(options =>
            {
                options.AutoOffsetReset = AutoOffsetReset.Earliest;
                // options.EnableAutoCommit = false; // TODO DryRun
            })
            .ValidateDataAnnotations();
    });

// TODO Health + Supervisor
var host = builder.Build();

// using (var producer = new ProducerBuilder<string, string>(new ProducerConfig
//        {
//            BootstrapServers = "127.0.0.1:19092"
//        }).Build())
// {
//     // Redpanda: by default, topic is created automatically on the first message
//     await producer.ProduceAsync("weather-forecasts", new Message<string, string>
//     {
//         Key = "London",
//         Value = JsonSerializer.Serialize(new WeatherForecast(25, 77, "Sunny"))
//     });
//     await producer.ProduceAsync("weather-forecasts", new Message<string, string>
//     {
//         Key = "Paris",
//         Value = JsonSerializer.Serialize(new WeatherForecast(18, 64, "Rainy"))
//     });
//     await producer.ProduceAsync("weather-forecasts", new Message<string, string>
//     {
//         Key = "Toronto",
//         Value = JsonSerializer.Serialize(new WeatherForecast(22, 72, "Cloudy"))
//     });
//     await producer.ProduceAsync("weather-forecasts", new Message<string, string>
//     {
//         Key = "Berlin",
//         Value = JsonSerializer.Serialize(new WeatherForecast(20, 68, "Sunny"))
//     });
// }

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
