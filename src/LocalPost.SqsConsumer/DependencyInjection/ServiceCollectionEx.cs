using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.SqsConsumer.DependencyInjection;

[PublicAPI]
public static class ServiceCollectionEx
{
    public static SqsBuilder AddSqsConsumers(this IServiceCollection services) => new(services);
}
