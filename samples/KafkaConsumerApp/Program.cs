using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using LocalPost;
using LocalPost.KafkaConsumer;
using LocalPost.KafkaConsumer.DependencyInjection;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddScoped<MessageHandler>();

        services.AddKafkaConsumers(kafka =>
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
                .Bind(context.Configuration.GetSection("Kafka"))
                .ValidateDataAnnotations();
            kafka.AddConsumer("weather-forecasts", HandlerStack.From<MessageHandler, WeatherForecast>()
                    .UseKafkaPayload()
                    .DeserializeJson()
                    .Acknowledge()
                    .Scoped()
                    .Trace()
                )
                .Bind(context.Configuration.GetSection("Kafka:Consumer"))
                .Configure(options =>
                {
                    options.AutoOffsetReset = AutoOffsetReset.Earliest;
                    options.EnableAutoCommit = false;
                })
                .ValidateDataAnnotations();
        });


        // services.AddKafkaConsumer<string>("orders",
        //     builder => { builder.SetHandler<KafkaTopicHandler>(); },
        //     builder => { builder.SetValueDeserializer(new StringDeserializer()); }).Configure<KafkaOptions>(
        //     (options, kafkaOptions) =>
        //     {
        //         options.Kafka.GroupId = "";
        //         options.Kafka.AutoOffsetReset = AutoOffsetReset.Earliest;
        //         options.Kafka.EnableAutoCommit = false; // TODO DryRun
        //
        //         options.Kafka.BootstrapServers = "localhost:9092";
        //         options.Kafka.SecurityProtocol = SecurityProtocol.SaslSsl;
        //         options.Kafka.SaslMechanism = SaslMechanism.Plain;
        //         options.Kafka.SaslUsername = "admin";
        //         options.Kafka.SaslPassword = "";
        //     });


        // Only one consumer per name (topic) is allowed?..
        // services.AddBatchKafkaConsumer<string>("orders",
        //     builder =>
        //     {
        //     },
        //     builder =>
        //     {
        //     });
    })
    .Build()
    .RunAsync();

public record WeatherForecast(int TemperatureC, int TemperatureF, string Summary);

internal sealed class MessageHandler : IHandler<WeatherForecast>
{
    public async ValueTask InvokeAsync(WeatherForecast payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload);
    }
}
