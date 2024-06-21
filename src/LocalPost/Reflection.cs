using System.Diagnostics.CodeAnalysis;
using LocalPost.DependencyInjection;

namespace LocalPost;

[ExcludeFromCodeCoverage]
internal static class Reflection
{
    public static string FriendlyNameOf<T>(string name) where T : INamedService =>
        FriendlyNameOf(typeof(T)) + ":" + name;

    public static string FriendlyNameOf<T>() => FriendlyNameOf(typeof(T));

    public static string FriendlyNameOf(Type type, string? instanceName)
    {
        var name = FriendlyNameOf(type);
        return instanceName is null ? name : $"{name}:{instanceName}";
    }

    public static string FriendlyNameOf(Type type) => type.IsGenericType switch
    {
        true => type.Name.Split('`')[0]
                + "<" + string.Join(", ", type.GetGenericArguments().Select(FriendlyNameOf).ToArray()) + ">",
        false => type.Name
    };
}
