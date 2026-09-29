namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.NFS;

    /// <summary>
    /// NFSv3 server implementation running in an ephemeral Docker container.
    /// </summary>
    public sealed class NfsDockerServer : IFileShareServer
    {
        /// <summary>
        /// Supported server implementations.
        /// </summary>
        public enum Flavor
        {
            /// <summary>
            /// nfs-ganesha userspace server.
            /// </summary>
            Ganesha,
            /// <summary>
            /// Linux kernel NFS server (requires a privileged container and nfsd support in the Docker host kernel).
            /// </summary>
            Knfsd,
            /// <summary>
            /// unfs3 userspace NFSv3 server.
            /// </summary>
            Unfs3
        }

        /// <summary>
        /// Export path.
        /// </summary>
        public const string ExportPath = "/export";

        /// <inheritdoc />
        public string TargetName { get; }

        /// <inheritdoc />
        public string Protocol { get; } = "nfs";

        /// <inheritdoc />
        public string Description { get; }

        /// <inheritdoc />
        public bool CaseInsensitive { get; } = false;

        /// <inheritdoc />
        public string ContainerId { get; private set; }

        /// <inheritdoc />
        public string ContainerSharePath { get; }

        /// <inheritdoc />
        public string LocalSharePath { get; } = null;

        /// <summary>
        /// Server flavor.
        /// </summary>
        public Flavor ServerFlavor { get; }

        /// <summary>
        /// Host port mapped to the NFS service.
        /// </summary>
        public int NfsPort { get; private set; }

        /// <summary>
        /// Host port of the MOUNT service.  For knfsd, the container uses the same port number so that
        /// portmapper discovery returns a port reachable from the host.
        /// </summary>
        public int MountPort { get; private set; }

        /// <summary>
        /// Host port mapped to the portmapper, or 0 when the portmapper is not published.
        /// </summary>
        public int PortmapperPort { get; private set; }

        private NfsDockerServer(Flavor flavor)
        {
            ServerFlavor = flavor;

            switch (flavor)
            {
                case Flavor.Ganesha:
                    TargetName = "nfs-ganesha";
                    Description = "nfs-ganesha VFS (Debian bookworm, Docker)";
                    ContainerSharePath = "/export";
                    break;
                case Flavor.Knfsd:
                    TargetName = "nfs-knfsd";
                    Description = "Linux kernel nfsd with rpcbind (Debian bookworm, privileged Docker)";
                    ContainerSharePath = "/export";
                    break;
                default:
                    TargetName = "nfs-unfs3";
                    Description = "unfs3 userspace NFSv3 (Docker)";
                    ContainerSharePath = "/export";
                    break;
            }
        }

        /// <summary>
        /// Build the image if needed and start a container.
        /// </summary>
        /// <param name="flavor">Server flavor.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server.</returns>
        public static async Task<NfsDockerServer> StartAsync(Flavor flavor, CancellationToken token)
        {
            string context;
            string image;

            switch (flavor)
            {
                case Flavor.Ganesha:
                    context = "nfs-ganesha";
                    image = "blobject-test-nfs-ganesha:1";
                    break;
                case Flavor.Knfsd:
                    context = "nfs-knfsd";
                    image = "blobject-test-nfs-knfsd:1";
                    break;
                default:
                    context = "nfs-unfs3";
                    image = "blobject-test-nfs-unfs3:1";
                    break;
            }

            await DockerCli.BuildImageAsync(Path.Combine(AppContext.BaseDirectory, "Docker", context), image, token).ConfigureAwait(false);

            NfsDockerServer server = new NfsDockerServer(flavor);
            server.NfsPort = DockerCli.GetFreeTcpPort();
            server.MountPort = DockerCli.GetFreeTcpPort();

            List<string> publish = new List<string>
            {
                "127.0.0.1:" + server.NfsPort + ":2049",
                "127.0.0.1:" + server.MountPort + ":" + server.MountPort
            };

            if (flavor == Flavor.Knfsd)
            {
                server.PortmapperPort = DockerCli.GetFreeTcpPort();
                publish.Add("127.0.0.1:" + server.PortmapperPort + ":111");
            }

            try
            {
                server.ContainerId = await DockerCli.RunContainerAsync(
                    image,
                    "blobject-" + server.TargetName,
                    publish,
                    new Dictionary<string, string> { { "MOUNT_PORT", server.MountPort.ToString() } },
                    flavor == Flavor.Knfsd,
                    new[] { "/export" },
                    flavor == Flavor.Ganesha ? new[] { "DAC_READ_SEARCH" } : null,
                    token).ConfigureAwait(false);

                await server.WaitUntilReadyAsync(token).ConfigureAwait(false);
                return server;
            }
            catch
            {
                await server.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <inheritdoc />
        public BlobProviderOptions ApplyTo(BlobProviderOptions options)
        {
            BlobProviderOptions ret = options.Clone();
            ret.Provider = "nfs";
            ret.TargetName = TargetName;
            ret.NfsHostname = "127.0.0.1";
            ret.NfsPort = NfsPort;
            ret.NfsShare = ExportPath;
            ret.NfsVersion = NfsVersionEnum.V3;

            if (ServerFlavor == Flavor.Knfsd)
            {
                // exercise portmapper discovery of the MOUNT port
                ret.NfsMountPort = 0;
                ret.NfsPortmapperPort = PortmapperPort;
            }
            else
            {
                ret.NfsMountPort = MountPort;
            }

            return ret;
        }

        /// <inheritdoc />
        public async Task RestartAsync(CancellationToken token)
        {
            await DockerCli.RunCheckedAsync(new[] { "restart", "--time", "2", ContainerId }, TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
            await WaitUntilReadyAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Return the numeric owner and group of a path within the export, e.g. 1234:5678.
        /// </summary>
        /// <param name="relativePath">Path relative to the export root.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Owner and group.</returns>
        public async Task<string> GetOwnerAsync(string relativePath, CancellationToken token)
        {
            string output = await DockerCli.ExecAsync(
                ContainerId,
                new[] { "stat", "-c", "%u:%g", ContainerSharePath + "/" + relativePath.TrimStart('/') },
                token).ConfigureAwait(false);
            return output.Trim();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DockerCli.RemoveContainerAsync(ContainerId).ConfigureAwait(false);
            ContainerId = null;
        }

        private async Task WaitUntilReadyAsync(CancellationToken token)
        {
            await DockerCli.WaitForTcpAsync("127.0.0.1", NfsPort, TimeSpan.FromSeconds(90), token).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(120);
            BlobProviderOptions options = ApplyTo(new BlobProviderOptions());

            while (true)
            {
                NfsSettings settings = new NfsSettings("127.0.0.1", 0, 0, ExportPath, NfsVersionEnum.V3)
                {
                    Port = options.NfsPort,
                    MountPort = options.NfsMountPort,
                    PortmapperPort = options.NfsPortmapperPort,
                    ConnectTimeoutMs = 5000,
                    ResponseTimeoutMs = 10000
                };

                using (NfsBlobClient client = new NfsBlobClient(settings))
                {
                    if (await client.ValidateConnectivity(token).ConfigureAwait(false)) return;
                }

                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException(TargetName + " container did not become ready:" + Environment.NewLine + await DockerCli.GetLogsAsync(ContainerId).ConfigureAwait(false));

                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
    }
}
