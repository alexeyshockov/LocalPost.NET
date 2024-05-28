using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

namespace LocalPost.BackgroundQueue;

// For the DI container, to distinguish between different queues
public sealed record Options<T> : Options;

// For the DI container, to distinguish between different queues
public sealed record BatchedOptions<T> : BatchedOptions;

public record BatchedOptions : Options
{
    [Range(1, ushort.MaxValue)] public ushort BatchMaxSize { get; set; } = 10;

    // TODO Rename to BatchTimeWindowMs
    [Range(1, ushort.MaxValue)] public int BatchTimeWindowMilliseconds { get; set; } = 1_000;

    internal TimeSpan BatchTimeWindow => TimeSpan.FromMilliseconds(BatchTimeWindowMilliseconds);
}

/// <summary>
///     Background queue configuration.
/// </summary>
public record Options
{
    /// <summary>
    ///     How to handle new messages when the queue (channel) is full. Default is to drop the oldest message (to not
    ///     block the producer).
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;

    /// <summary>
    ///     Maximum queue (channel) length, after which writes are blocked (see <see cref="FullMode" />).
    ///     Default is unlimited.
    /// </summary>
    [Range(1, ushort.MaxValue)]
    public ushort? MaxSize { get; set; } = null;

    /// <summary>
    ///     How long to wait before closing the queue (channel) on app shutdown. Default is 1 second.
    /// </summary>
    public ushort CompletionDelay { get; set; } = 1_000; // Milliseconds

    /// <summary>
    ///     How many messages to process concurrently. Default is 10.
    /// </summary>
    [Required]
    [Range(1, ushort.MaxValue)]
    public ushort MaxConcurrency { get; set; } = 10;
}
