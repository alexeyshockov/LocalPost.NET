using System.Text;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace LocalPost.KafkaConsumer.Tests;

// See also https://github.com/testcontainers/testcontainers-dotnet/blob/develop/src/Testcontainers.Kafka/KafkaBuilder.cs
public sealed class RedpandaBuilder : ContainerBuilder<RedpandaBuilder, RedpandaContainer, ContainerConfiguration>
{
    public const string RedpandaImage = "docker.redpanda.com/redpandadata/redpanda:v24.3.3";

    public const ushort KafkaPort = 9092;
    public const ushort KafkaAdminPort = 9644;
    public const ushort PandaProxyPort = 8082;
    public const ushort SchemaRegistryPort = 8081;

    public const string StartupScriptFilePath = "/testcontainers.sh";

    public RedpandaBuilder() : this(new ContainerConfiguration())
    {
        DockerResourceConfiguration = Init().DockerResourceConfiguration;
    }

    private RedpandaBuilder(ContainerConfiguration resourceConfiguration) : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

    protected override RedpandaBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration) =>
        Merge(DockerResourceConfiguration, new ContainerConfiguration(resourceConfiguration));

    protected override RedpandaBuilder Clone(IContainerConfiguration resourceConfiguration) =>
        Merge(DockerResourceConfiguration, new ContainerConfiguration(resourceConfiguration));

    protected override RedpandaBuilder Merge(ContainerConfiguration oldValue, ContainerConfiguration newValue) =>
        new(new ContainerConfiguration(oldValue, newValue));

    protected override ContainerConfiguration DockerResourceConfiguration { get; }

    public override RedpandaContainer Build()
    {
        Validate();
        return new RedpandaContainer(DockerResourceConfiguration);
    }

    protected override RedpandaBuilder Init()
    {
        return base.Init()
            .WithImage(RedpandaImage)
            .WithPortBinding(KafkaPort, true)
            .WithPortBinding(KafkaAdminPort, true)
            .WithPortBinding(PandaProxyPort, true)
            .WithPortBinding(SchemaRegistryPort, true)
            .WithEntrypoint("/bin/sh", "-c")
            .WithCommand("while [ ! -f " + StartupScriptFilePath + " ]; do sleep 0.1; done; " + StartupScriptFilePath)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Started Kafka API server"))
            .WithStartupCallback((container, ct) =>
            {
                string[] cmd =
                [
                    "rpk", "redpanda", "start",
                    "--smp 1",
                    "--mode dev-container",
                    $"--kafka-addr internal://0.0.0.0:29092,external://0.0.0.0:{KafkaPort}",
                    $"--advertise-kafka-addr internal://{container.IpAddress}:29092,external://{container.Hostname}:{container.GetMappedPublicPort(KafkaPort)}",
                    $"--pandaproxy-addr internal://0.0.0.0:28082,external://0.0.0.0:{PandaProxyPort}",
                    $"--advertise-pandaproxy-addr internal://{container.IpAddress}:28082,external://{container.Hostname}:{container.GetMappedPublicPort(PandaProxyPort)}",
                    $"--schema-registry-addr internal://0.0.0.0:28081,external://0.0.0.0:{SchemaRegistryPort}",
                ];
                var startupScript = "#!/bin/sh" + '\n' + '\n' + string.Join(' ', cmd) + '\n';

                return container.CopyAsync(Encoding.Default.GetBytes(startupScript), StartupScriptFilePath, Unix.FileMode755, ct);
            });
    }
}

public sealed class RedpandaContainer(IContainerConfiguration configuration) : DockerContainer(configuration)
{
    public string GetSchemaRegistryAddress() =>
        new UriBuilder(Uri.UriSchemeHttp, Hostname, GetMappedPublicPort(RedpandaBuilder.SchemaRegistryPort)).ToString();

    public string GetBootstrapAddress() =>
        // new UriBuilder("PLAINTEXT", Hostname, GetMappedPublicPort(RpBuilder.KafkaPort)).ToString();
        $"{Hostname}:{GetMappedPublicPort(RedpandaBuilder.KafkaPort)}";
}
