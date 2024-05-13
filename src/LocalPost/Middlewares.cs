using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LocalPost;

public static partial class HandlerStack
{
    public static HandlerFactory<T> LogErrors<T>(this HandlerFactory<T> handlerStack) =>
        handlerStack.Map<T, T>(provider =>
            ActivatorUtilities.CreateInstance<LoggingErrorHandler<T>>(provider).Invoke);
}

internal class LoggingErrorHandler<T> : IHandlerMiddleware<T, T>
{
    private readonly ILogger<LoggingErrorHandler<T>> _logger;

    public LoggingErrorHandler(ILogger<LoggingErrorHandler<T>> logger)
    {
        _logger = logger;
    }

    public Handler<T> Invoke(Handler<T> next) => async (context, ct) =>
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
            _logger.LogError(e, "Unhandled exception while processing a message");
        }
    };
}

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
