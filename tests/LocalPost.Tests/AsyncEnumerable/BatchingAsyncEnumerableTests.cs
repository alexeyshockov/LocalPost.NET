using System.Threading.Channels;
using FluentAssertions;
using LocalPost.AsyncEnumerable;
using Nito.AsyncEx;

namespace LocalPost.Tests.AsyncEnumerable;

public class BatchingAsyncEnumerableTests
{
    [Fact]
    internal async Task batches()
    {
        var source = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var results = source.Reader.ReadAllAsync().Batch(
            (ct) => new BoundedBatchBuilder<int>(10, TimeSpan.FromSeconds(2)));

        async Task Produce()
        {
            await source.Writer.WriteAsync(1);
            await source.Writer.WriteAsync(2);
            await source.Writer.WriteAsync(3);

            await Task.Delay(TimeSpan.FromSeconds(3));

            await source.Writer.WriteAsync(4);
            await source.Writer.WriteAsync(5);

            source.Writer.Complete();
        }

        async Task Consume()
        {
            var expect = new Queue<int[]>();
            expect.Enqueue(new[] { 1, 2, 3 });
            expect.Enqueue(new[] { 4, 5 });
            await foreach (var batch in results)
            {
                batch.Should().ContainInOrder(expect.Dequeue());
            }

            expect.Should().BeEmpty();
        }

        await Task.WhenAll(Produce(), Consume());
    }
}
