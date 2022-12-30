using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LocalPost.DependencyInjection;

// TODO Open to public later
internal static class ConfigurationExtensions
{
    public static OptionsBuilder<QueueOptions> AddBackgroundQueueOptions<T>(this IServiceCollection services) =>
        services.AddOptions<QueueOptions>(Reflection.FriendlyNameOf<BackgroundQueue<T>>());

    public static OptionsBuilder<QueueOptions> AddBackgroundQueueOptions(this IServiceCollection services, string name) =>
        services.AddOptions<QueueOptions>(name);
}
