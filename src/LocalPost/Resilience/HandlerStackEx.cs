using Polly;

namespace LocalPost.Resilience;

[PublicAPI]
public static class HandlerStackEx
{
    public static HandlerFactory<T> UsePollyPipeline<T>(this HandlerFactory<T> hf,
        ResiliencePipeline pipeline) => hf.Touch(next =>
        async (context, ct) =>
        {
            await pipeline.ExecuteAsync(execCt => next(context, execCt), ct);
        });

    public static HandlerFactory<T> UsePollyPipeline<T>(this HandlerFactory<T> hf,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);

        return hf.UsePollyPipeline(builder.Build());
    }
}
