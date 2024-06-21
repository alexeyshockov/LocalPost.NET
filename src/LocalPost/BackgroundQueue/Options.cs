using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

namespace LocalPost.BackgroundQueue;

// // For the DI container, to distinguish between different queues
// public sealed record QueueOptions<T> : QueueOptions;

// // For the DI container, to distinguish between different queues
// public sealed record BatchedOptions<T> : BatchedOptions;
//
// public record BatchedOptions : Options
// {
//     [Range(1, ushort.MaxValue)] public ushort BatchMaxSize { get; set; } = 10;
//
//     // TODO Rename to BatchTimeWindowMs
//     [Range(1, ushort.MaxValue)] public int BatchTimeWindowMilliseconds { get; set; } = 1_000;
//
//     internal TimeSpan BatchTimeWindow => TimeSpan.FromMilliseconds(BatchTimeWindowMilliseconds);
// }

/// <summary>
///     Background queue configuration.
/// </summary>
public sealed class QueueOptions<T>
{
    // /// <summary>
    // ///     How to handle new messages when the queue (channel) is full. Default is to drop the oldest message (to not
    // ///     block the producer).
    // /// </summary>
    // public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;
    //
    // /// <summary>
    // ///     Maximum queue (channel) length, after which writes are blocked (see <see cref="FullMode" />).
    // ///     Default is unlimited.
    // /// </summary>
    // [Range(1, ushort.MaxValue)]
    // public ushort? MaxSize { get; set; } = null;

    public ChannelOptions Channel { get; set; } = new UnboundedChannelOptions();

    /// <summary>
    ///     How long to wait before closing the queue (channel) on app shutdown. Default is 1 second.
    /// </summary>
    public ushort CompletionDelay { get; set; } = 1_000; // Milliseconds

    // /// <summary>
    // ///     How many messages to process concurrently. Default is 10.
    // /// </summary>
    // [Required]
    // [Range(1, ushort.MaxValue)]
    // public ushort MaxConcurrency { get; set; } = 10;

    internal void UpdateFrom(QueueOptions<T> options)
    {
        // FullMode = options.FullMode;
        // MaxSize = options.MaxSize;
        Channel = options.Channel;
        CompletionDelay = options.CompletionDelay;
        // MaxConcurrency = options.MaxConcurrency;
    }
}

public sealed class DefaultPipelineOptions<T>
{
    public QueueOptions<T> Queue { get; } = new();

    [Range(1, ushort.MaxValue)]
    public ushort MaxConcurrency { get; set; } = 10;

    public static implicit operator Pipeline.ConsumerOptions(DefaultPipelineOptions<T> options) => new()
    {
        MaxConcurrency = options.MaxConcurrency,
        BreakOnException = false,
    };
}

public sealed class DefaultBatchPipelineOptions<T>
{
    // public QueueOptions<IEnumerable<T>> Queue { get; } = new();
    public QueueOptions<T> Queue { get; } = new();

    [Range(1, ushort.MaxValue)]
    public ushort MaxConcurrency { get; set; } = 10;

    [Range(1, ushort.MaxValue)]
    public ushort BatchMaxSize { get; set; } = 10;

    [Range(1, ushort.MaxValue)]
    public int TimeWindowMs { get; set; } = 1_000;

    public static implicit operator Pipeline.ConsumerOptions(DefaultBatchPipelineOptions<T> options) => new()
    {
        MaxConcurrency = options.MaxConcurrency,
        BreakOnException = false,
    };

    public static implicit operator BatchOptions(DefaultBatchPipelineOptions<T> options) => new()
    {
        MaxSize = options.BatchMaxSize,
        TimeWindowDuration = options.TimeWindowMs,
    };
}
