using Amazon.Runtime;
using Polly;
using Polly.CircuitBreaker;
using Polly.Contrib.WaitAndRetry;

namespace LocalPost.AmazonSns;

internal static class ResiliencePolicy
{
    public static IAsyncPolicy Create()
    {
        var circuitBreaker = Policy
            .Handle<Exception>(IsTransient)
            .CircuitBreakerAsync(3, TimeSpan.FromSeconds(1));

        var retryPolicy = Policy
            .Handle<Exception>(IsTransient)
            .Or<BrokenCircuitException>()
            // See https://github.com/App-vNext/Polly/wiki/Retry-with-jitter
            .WaitAndRetryAsync(Backoff.DecorrelatedJitterBackoffV2(
                medianFirstRetryDelay: TimeSpan.FromSeconds(1),
                retryCount: 3));

        var resilientPolicy = Policy.WrapAsync(retryPolicy, circuitBreaker);

        return resilientPolicy;
    }

    public static bool IsTransient(Exception exception) => exception switch
    {
        AmazonServiceException { Retryable: not null } awsException => true,
        // TODO Handle Amazon.Runtime.Internal.HttpErrorResponseException
        _ => false,
    };
}
