using Microsoft.Extensions.DependencyInjection;

namespace LocalPost;

public static partial class HandlerStackEx
{
    // public static HandlerFactory<T> LogErrors<T>(this HandlerFactory<T> hf) => hf.Map(provider =>
    //     ActivatorUtilities.CreateInstance<ErrorLoggingHandler<T>>(provider));

    public static HandlerFactory<T> Scoped<T>(this HandlerFactory<T> hf) => provider =>
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        return new ScopedHandler<T>(scopeFactory, hf).InvokeAsync;
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

// Too narrow use case in the first place, also easier to implement using a lambda
// internal class ErrorLoggingHandler<T>(ILogger<T> logger) : IHandlerMiddleware<T, T>
// {
//     public Handler<T> Invoke(Handler<T> next) => async (context, ct) =>
//     {
//         try
//         {
//             await next(context, ct);
//         }
//         catch (OperationCanceledException e) when (e.CancellationToken == ct)
//         {
//             throw;
//         }
//         catch (Exception e)
//         {
//             logger.LogError(e, "Unhandled exception while processing a message");
//         }
//     };
// }

// TODO Just add it as an example, also using Polly
//[PublicAPI]
//public static class Middlewares
//{
//    public static Middleware<T> Timeout<T>(TimeSpan timeout) => next => async (context, ct) =>
//    {
//        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
//        cts.CancelAfter(timeout);
//
//        await next(context, cts.Token);
//    };
//}
