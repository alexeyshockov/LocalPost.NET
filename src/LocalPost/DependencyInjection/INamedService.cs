using Microsoft.Extensions.DependencyInjection;

namespace LocalPost.DependencyInjection;

internal interface INamedService
{
    string Name { get; }
}

// internal sealed class NamedServiceDescriptor : ServiceDescriptor
// {
//     public static NamedServiceDescriptor Singleton<TService>(string name, Func<IServiceProvider, TService> iFactory)
//         where TService : class, INamedService =>
//         new(typeof(TService), name, iFactory, ServiceLifetime.Singleton);
//
//     public string Name { get; init; }
//
//     public NamedServiceDescriptor(Type sType, string name, Type iType, ServiceLifetime lifetime) :
//         base(sType, iType, lifetime)
//     {
//         Name = name;
//     }
//
//     public NamedServiceDescriptor(Type sType, string name, object instance) : base(sType, instance)
//     {
//         Name = name;
//     }
//
//     public NamedServiceDescriptor(Type sType, string name, Func<IServiceProvider, object> factory,
//         ServiceLifetime lifetime) : base(sType, factory, lifetime)
//     {
//         Name = name;
//     }
// }
