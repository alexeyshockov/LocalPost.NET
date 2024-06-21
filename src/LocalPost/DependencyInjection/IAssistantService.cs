namespace LocalPost.DependencyInjection;

public readonly record struct AssistedService
{
    private readonly Type _type;

    private readonly string? _name;

    private AssistedService(Type type, string? name = null)
    {
        _type = type;
        _name = name;
    }

    internal static AssistedService From<T>() => new(typeof(T));

    internal static AssistedService From<T>(string name) where T : INamedService => new(typeof(T), name);

    public static implicit operator string(AssistedService service) => service.ToString();

    public override string ToString() => Reflection.FriendlyNameOf(_type, _name);
}

internal interface IAssistantService
{
    // string Target { get; }
    AssistedService Target { get; }
}
