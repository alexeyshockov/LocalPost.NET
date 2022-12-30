using System.Text;
using Amazon.SimpleNotificationService.Model;

namespace LocalPost.SnsPublisher;

internal static class PublishBatchRequestEntryExtensions
{
    // Include attributes in the calculation later?..
    public static int CalculateSize(this PublishBatchRequestEntry entry) => Encoding.UTF8.GetByteCount(entry.Message);
}
