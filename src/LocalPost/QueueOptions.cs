using System.ComponentModel.DataAnnotations;

namespace LocalPost;

/// <summary>
///     Background queue configuration.
/// </summary>
public sealed record QueueOptions
{
    /// <summary>
    ///     How many messages to process in parallel.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = ushort.MaxValue;
}
