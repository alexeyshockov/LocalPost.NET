using System.ComponentModel.DataAnnotations;

namespace LocalPost;

/// <summary>
///     Background queue configuration.
/// </summary>
public sealed record BackgroundQueueOptions<T>
{
    public QueueOptions Queue { get; set; } = new();

    public ConsumerOptions Consumer { get; set; } = new();
}

/// <summary>
///     Consumer configuration.
/// </summary>
public sealed record ConsumerOptions
{
    /// <summary>
    ///     How many messages to process in parallel.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = ushort.MaxValue;
}

/// <summary>
///     Queue configuration.
/// </summary>
public sealed record QueueOptions
{
    // TODO Drop strategy
    public ushort? MaxSize { get; set; } = ushort.MaxValue;

    public ushort? CompletionTimeout { get; set; } = 1_000; // Milliseconds
}
