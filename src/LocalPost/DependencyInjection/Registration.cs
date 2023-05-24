using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

public static class Registration
{
    public static void TryAddConcurrentHostedServices(this IServiceCollection services) =>
        services.AddHostedService<HostedServices>();
}
