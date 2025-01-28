using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

public static partial class Middlewares
{
    public static HandlerManagerFactory<byte[]> DecodeString(this HandlerManagerFactory<string> hmf) =>
        DecodeString(hmf, Encoding.UTF8);

    public static HandlerManagerFactory<byte[]> DecodeString(this HandlerManagerFactory<string> hmf,
        Encoding encoding) => hmf.MapHandler<byte[], string>(next =>
        async (payload, ct) =>
        {
            var s = encoding.GetString(payload);
            await next(s, ct).ConfigureAwait(false);
        });

    /// <summary>
    ///     Handle exceptions and log them, to not break the consumer loop.
    /// </summary>
    /// <param name="hmf">Handler factory to wrap.</param>
    /// <typeparam name="T">Handler's payload type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerManagerFactory<T> LogExceptions<T>(this HandlerManagerFactory<T> hmf) => provider =>
    {
        var logger = provider.GetRequiredService<ILogger<T>>();
        return hmf(provider).Touch(next => async (context, ct) =>
            {
                try
                {
                    await next(context, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == ct)
                {
                    throw;
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Unhandled exception while processing a message");
                }
            }
        );
    };

    /// <summary>
    ///     Create a DI scope for every message and resolve the handler from it.
    /// </summary>
    /// <param name="hmf">Handler factory to wrap.</param>
    /// <typeparam name="T">Handler's payload type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerManagerFactory<T> Scoped<T>(this HandlerManagerFactory<T> hmf) => provider =>
        new ScopedHandlerManager<T>(provider.GetRequiredService<IServiceScopeFactory>(), hmf);

    /// <summary>
    ///     Shutdown the whole app on error.
    /// </summary>
    /// <param name="hmf">Handler factory to wrap.</param>
    /// <param name="exitCode">Process exit code.</param>
    /// <typeparam name="T">Handler's payload type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerManagerFactory<T> ShutdownOnError<T>(this HandlerManagerFactory<T> hmf, int exitCode = 1) =>
        provider =>
        {
            var appLifetime = provider.GetRequiredService<IHostApplicationLifetime>();
            return hmf(provider).Touch(next => async (context, ct) =>
            {
                try
                {
                    await next(context, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException e) when (e.CancellationToken == ct)
                {
                    throw;
                }
                catch
                {
                    appLifetime.StopApplication();
                    Environment.ExitCode = exitCode;
                }
            });
        };
}


internal sealed class ScopedHandlerManager<T>(IServiceScopeFactory sf, HandlerManagerFactory<T> hmf)
    : IHandlerManager<T>
{
    private async ValueTask Handle(T payload, CancellationToken ct)
    {
        // See https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/#handling-iasyncdisposable-services-with-iservicescope
        // And also https://devblogs.microsoft.com/dotnet/announcing-net-6/#microsoft-extensions-dependencyinjection-createasyncscope-apis
        await using var scope = sf.CreateAsyncScope();

        var hm = hmf(scope.ServiceProvider);
        var handler = await hm.Start(ct).ConfigureAwait(false);
        try
        {
            await handler(payload, ct).ConfigureAwait(false);
            await hm.Stop(null, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == ct)
        {
            throw;
        }
        catch (Exception e)
        {
            await hm.Stop(e, ct).ConfigureAwait(false);
        }
    }

    public ValueTask<Handler<T>> Start(CancellationToken ct)
    {
        Handler<T> handler = Handle;
        return ValueTask.FromResult(handler);
    }

    public ValueTask Stop(Exception? error, CancellationToken ct) => ValueTask.CompletedTask;
}
