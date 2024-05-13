using JetBrains.Annotations;
using Polly;

namespace LocalPost.Polly;

[PublicAPI]
public static class PollyHandlerStack
{
    public static HandlerFactory<T> UsePollyPipeline<T>(this HandlerFactory<T> handlerStack,
        ResiliencePipeline pipeline) =>
        handlerStack.Map<T, T>(next => async (context, ct) =>
        {
            await pipeline.ExecuteAsync(ct => next(context, ct), ct);
        });

    public static HandlerFactory<T> UsePollyPipeline<T>(this HandlerFactory<T> handlerStack,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);

        return handlerStack.UsePollyPipeline(builder.Build());
    }
}
