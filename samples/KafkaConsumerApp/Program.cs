using System.ComponentModel.DataAnnotations;
using Confluent.Kafka;
using LocalPost;
using LocalPost.KafkaConsumer;
using LocalPost.KafkaConsumer.DependencyInjection;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<KafkaOptions>()
            .Bind(context.Configuration.GetSection(KafkaOptions.ConfigSection))
            .ValidateDataAnnotations();

        services.AddScoped<KafkaTopicHandler>();
        services.AddKafkaConsumer<string>("orders",
            builder =>
            {
                builder.SetHandler<KafkaTopicHandler>();
            },
            builder =>
            {
                builder.SetValueDeserializer(new StringDeserializer());
            }).Configure<KafkaOptions>((options, kafkaOptions) =>
            {
                options.Kafka.GroupId = "";
                options.Kafka.AutoOffsetReset = AutoOffsetReset.Earliest;
                options.Kafka.EnableAutoCommit = false; // TODO DryRun

                options.Kafka.BootstrapServers = "localhost:9092";
                options.Kafka.SecurityProtocol = SecurityProtocol.SaslSsl;
                options.Kafka.SaslMechanism = SaslMechanism.Plain;
                options.Kafka.SaslUsername = "admin";
                options.Kafka.SaslPassword = "";
            });
        // Only one consumer per name (topic) is allowed
        services.AddBatchKafkaConsumer<string>("orders",
            builder =>
            {
            },
            builder =>
            {
            });
    })
    .Build();

host.Run();

public sealed record KafkaOptions
{
    public const string ConfigSection = "Kafka";

    [Required]
    public string BootstrapServers { get; init; } = null!;

    public Dictionary<string, Options> Consumers { get; init; } = new();
}

internal class KafkaTopicHandler : IHandler<ConsumeContext<string>>
{
    public async Task InvokeAsync(ConsumeContext<string> payload, CancellationToken ct)
    {
        await Task.Delay(1_000, ct);
        Console.WriteLine(payload.Payload);
    }
}
