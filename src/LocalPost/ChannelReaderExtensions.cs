using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LocalPost;

internal static class ChannelReaderExtensions
{
    // netstandard2.0 does not contain this overload, it's available only from netstandard2.1 (.NET Core 3.0)
    public static async IAsyncEnumerable<T> ReadAllAsync<T>(this ChannelReader<T> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            while (reader.TryRead(out var item))
                yield return item;
    }
}
