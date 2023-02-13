using System.ComponentModel.DataAnnotations;

namespace LocalPost.Azure.QueueConsumer;

/// <summary>
///     General Azure Storage Queue consumer settings
/// </summary>
public sealed record ConsumerOptions
{
    /// <summary>
    ///     How many messages to process in parallel.
    /// </summary>
    [Required] public ushort MaxConcurrency { get; set; } = 10;

    [Required] public string QueueName { get; set; } = null!;

    [Range(1, 32)] public byte BufferSize { get; set; } = 10;
}
