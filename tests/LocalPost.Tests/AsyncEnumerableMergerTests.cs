using System.Threading.Channels;
using FluentAssertions;

namespace LocalPost.Tests;

public class AsyncEnumerableMergerTests
{
    [Fact]
    internal async Task aggregates_multiple_channels()
    {
        var source1 = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var source2 = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var results = new AsyncEnumerableMerger<int>(new[]
        {
            source1.Reader.ReadAllAsync(), source2.Reader.ReadAllAsync()
        });

        async Task Produce()
        {
            await source1.Writer.WriteAsync(1);
            await source2.Writer.WriteAsync(1);
            await source1.Writer.WriteAsync(1);

            await Task.Delay(TimeSpan.FromSeconds(1));

            source1.Writer.Complete();
            await source2.Writer.WriteAsync(4);

            await Task.Delay(TimeSpan.FromSeconds(1));

            source2.Writer.Complete();
        }

        async Task Consume()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var expect = new Queue<int>();
            expect.Enqueue(1);
            expect.Enqueue(1);
            expect.Enqueue(1);
            expect.Enqueue(4);

            await foreach (var r in results.WithCancellation(cts.Token))
                r.Should().Be(expect.Dequeue());

            expect.Should().BeEmpty();
        }

        await Task.WhenAll(Produce(), Consume());
    }

    [Fact]
    internal async Task aggregates_multiple_channels_over_time()
    {
        var source1 = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var source2 = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
        var results = new AsyncEnumerableMerger<int>(true);

        async Task Produce()
        {
            await source1.Writer.WriteAsync(1);
            await source2.Writer.WriteAsync(2);
            await source1.Writer.WriteAsync(3);

            await Task.Delay(TimeSpan.FromSeconds(1)); // Does not matter

            results.Add(source1.Reader.ReadAllAsync());

            source1.Writer.Complete();

            await source2.Writer.WriteAsync(4);

            results.Add(source2.Reader.ReadAllAsync());

            await Task.Delay(TimeSpan.FromSeconds(1));

            source2.Writer.Complete();
        }

        async Task Consume()
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

            var expect = new Queue<int>();
            expect.Enqueue(1);
            expect.Enqueue(3);
            expect.Enqueue(2);
            expect.Enqueue(4);

            try
            {
                await foreach (var r in results.WithCancellation(cts.Token))
                {
//                    r.Should().Be(expect.Dequeue());
                    expect.Dequeue();
                }
            }
            catch (OperationCanceledException e) when (e.CancellationToken == cts.Token)
            {
                // Should happen
            }

            cts.IsCancellationRequested.Should().BeTrue();
            expect.Should().BeEmpty();
        }

        await Task.WhenAll(Produce(), Consume());
    }

    [Fact]
    internal async Task aggregates_multiple_channels_permanently()
    {
        var sut = new AsyncEnumerableMerger<int>(true);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        try
        {
            await foreach (var r in sut.WithCancellation(cts.Token))
            {
            }
        }
        catch (OperationCanceledException)
        {
            cts.IsCancellationRequested.Should().BeTrue();
        }
    }
}
