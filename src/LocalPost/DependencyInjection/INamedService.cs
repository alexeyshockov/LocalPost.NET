using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

internal interface INamedService
{
    string Name { get; }
}

internal sealed class NamedServiceDescriptor : ServiceDescriptor
{
    public static NamedServiceDescriptor Singleton<TService>(string name,
        Func<IServiceProvider, TService> implementationFactory) where TService : class, INamedService =>
        new(typeof(TService), name, implementationFactory, ServiceLifetime.Singleton);

    public string Name { get; init; }

    public NamedServiceDescriptor(Type serviceType, string name, Type implementationType, ServiceLifetime lifetime) : base(serviceType, implementationType, lifetime)
    {
        Name = name;
    }

    public NamedServiceDescriptor(Type serviceType, string name, object instance) : base(serviceType, instance)
    {
        Name = name;
    }

    public NamedServiceDescriptor(Type serviceType, string name, Func<IServiceProvider, object> factory, ServiceLifetime lifetime) : base(serviceType, factory, lifetime)
    {
        Name = name;
    }
}
