using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LocalPost;

public static partial class HandlerStackEx
{
    /// <summary>
    ///     Handle exceptions and log them, to not break the consumer loop.
    /// </summary>
    /// <param name="hf">Handler factory to wrap.</param>
    /// <typeparam name="T">Handler's payload type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerFactory<T> LogExceptions<T>(this HandlerFactory<T> hf) => provider =>
    {
        var logger = provider.GetRequiredService<ILogger<T>>();
        var next = hf(provider);

        return async (context, ct) =>
        {
            try
            {
                await next(context, ct);
            }
            catch (OperationCanceledException e) when (e.CancellationToken == ct)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unhandled exception while processing a message");
            }
        };
    };

    /// <summary>
    ///     Create a DI scope for every message and resolve the handler from it.
    /// </summary>
    /// <param name="hf">Handler factory to wrap.</param>
    /// <typeparam name="T">Handler's payload type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerFactory<T> Scoped<T>(this HandlerFactory<T> hf) => provider =>
        new ScopedHandler<T>(provider.GetRequiredService<IServiceScopeFactory>(), hf).InvokeAsync;

    /// <summary>
    ///     Shutdown the whole app on error.
    /// </summary>
    /// <param name="hf">Handler factory to wrap.</param>
    /// <param name="exitCode">Process exit code.</param>
    /// <typeparam name="T">Handler's payload type.</typeparam>
    /// <returns>Wrapped handler factory.</returns>
    public static HandlerFactory<T> ShutdownOnError<T>(this HandlerFactory<T> hf, int exitCode = 1) => provider =>
    {
        var appLifetime = provider.GetRequiredService<IHostApplicationLifetime>();
        var next = hf(provider);

        return async (context, ct) =>
        {
            try
            {
                await next(context, ct);
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
        };
    };
}

internal sealed class ScopedHandler<T>(IServiceScopeFactory sf, HandlerFactory<T> hf) : IHandler<T>
{
    public async ValueTask InvokeAsync(T payload, CancellationToken ct)
    {
        // See https://andrewlock.net/exploring-dotnet-6-part-10-new-dependency-injection-features-in-dotnet-6/#handling-iasyncdisposable-services-with-iservicescope
        // And also https://devblogs.microsoft.com/dotnet/announcing-net-6/#microsoft-extensions-dependencyinjection-createasyncscope-apis
        await using var scope = sf.CreateAsyncScope();

        var handler = hf(scope.ServiceProvider);

        await handler(payload, ct);
    }
}
