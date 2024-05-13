using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LocalPost;

internal static class ChannelReaderEx
{
    // netstandard2.0 does not contain this overload, it's available only from netstandard2.1 (.NET Core 3.0+)
    public static async IAsyncEnumerable<T> ReadAllAsync<T>(this ChannelReader<T> reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            while (reader.TryRead(out var item))
                yield return item;
    }
}

internal static class EnumerableEx
{
    // Can be removed on .NET 6+, see https://stackoverflow.com/a/6362642/322079
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, ushort size)
    {
        while (source.Any())
        {
            yield return source.Take(size);
            source = source.Skip(size);
        }
    }
}
