using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

namespace LocalPost;

/// <summary>
///     Background queue configuration.
/// </summary>
public record BackgroundQueueOptions
{
    // TODO Use
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.DropOldest;

    public ushort? MaxSize { get; set; } = null;

    public ushort? CompletionTimeout { get; set; } = 1_000; // Milliseconds

    /// <summary>
    ///     How many messages to process in parallel. Default is 10.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = 10;
}
