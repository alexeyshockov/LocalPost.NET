using System.Diagnostics.CodeAnalysis;

namespace LocalPost;

[ExcludeFromCodeCoverage]
internal static class Reflection
{
    public static string FriendlyNameOf<T>() => FriendlyNameOf(typeof(T));

    public static string FriendlyNameOf(Type type) => type.IsGenericType switch
    {
        true => type.Name.Split('`')[0]
                + "<" + string.Join(", ", type.GetGenericArguments().Select(FriendlyNameOf).ToArray()) + ">",
        false => type.Name
    };
}
