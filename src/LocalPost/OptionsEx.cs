using Microsoft.Extensions.Options;

namespace LocalPost;

public static class OptionsEx
{
    public static TOptions Get<TOptions, T>(this IOptionsMonitor<TOptions> optionsMonitor) =>
        optionsMonitor.Get(Reflection.FriendlyNameOf<T>());
}
