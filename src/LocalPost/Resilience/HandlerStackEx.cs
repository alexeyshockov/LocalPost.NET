using Polly;

namespace LocalPost.Resilience;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerManagerFactory<T> UsePollyPipeline<T>(this HandlerManagerFactory<T> hmf,
        ResiliencePipeline pipeline) => hmf.TouchHandler(next => (context, ct) =>
            pipeline.ExecuteAsync(execCt => next(context, execCt), ct));

    public static HandlerManagerFactory<T> UsePollyPipeline<T>(this HandlerManagerFactory<T> hmf,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);

        return hmf.UsePollyPipeline(builder.Build());
    }
}
