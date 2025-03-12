namespace LocalPost;

[ExcludeFromCodeCoverage]
internal static class Reflection
{
    // public static string FriendlyNameOf<T>(string name) where T : INamedService =>
    public static string FriendlyNameOf<T>(string? name) => FriendlyNameOf(typeof(T), name);

    public static string FriendlyNameOf<T>() => FriendlyNameOf(typeof(T));

    public static string FriendlyNameOf(Type type, string? name) =>
        FriendlyNameOf(type) + (string.IsNullOrEmpty(name) ? "" : $" (\"{name}\")");

    public static string FriendlyNameOf(Type type) => type.IsGenericType switch
    {
        true => type.Name.Split('`')[0]
                + "<" + string.Join(", ", type.GetGenericArguments().Select(FriendlyNameOf).ToArray()) + ">",
        false => type.Name
    };
}
