namespace LocalPost.SnsPublisher;

public sealed record PublisherOptions
{
    // Same for Publish and PublishBatch
    public const int RequestMaxSize = 262_144;

    public const int BatchMaxSize = 10;

    public ConsumerOptions Sender { get; init; } = new();

    public QueueOptions PerTopic { get; init; } = new();
}
