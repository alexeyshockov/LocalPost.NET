using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

namespace LocalPost.BackgroundQueue;

public sealed record QueueOptions<T>
{
    /// <summary>
    ///     How many messages to process concurrently. Default is 10.
    /// </summary>
    [Required]
    [Range(1, ushort.MaxValue)]
    public ushort MaxConcurrency { get; set; } = 50;

    [Range(1, int.MaxValue)]
    public int? BufferSize { get; set; } = 1000;

    /// <summary>
    ///     How to handle new messages when the underlying channel is full. Default is to drop the oldest message
    ///     (to not block the producer).
    /// </summary>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;

    public bool SingleProducer { get; set; } = false;

    /// <summary>
    ///     How long to wait before closing the queue (channel) on app shutdown. Default is 1 second.
    /// </summary>
    public Func<CancellationToken, Task> CompletionTrigger { get; set; } = ct => Task.Delay(1000, ct);
    // public ushort CompletionDelay { get; set; } = 1_000; // Milliseconds
}
