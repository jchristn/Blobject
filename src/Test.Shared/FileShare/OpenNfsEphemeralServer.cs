namespace Test.Shared.FileShare
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.NFS;
    using OpenNFS.Server;
    using OpenNFS.Server.FileHandles;

    /// <summary>
    /// In-process OpenNFS server (MOUNT v3 and NFSv3) on loopback ports, serving a temporary directory.
    /// </summary>
    public sealed class OpenNfsEphemeralServer : IFileShareServer
    {
        /// <summary>
        /// Export path.
        /// </summary>
        public const string ExportPath = "/export";

        /// <inheritdoc />
        public string TargetName { get; } = "nfs-ephemeral";

        /// <inheritdoc />
        public string Protocol { get; } = "nfs";

        /// <inheritdoc />
        public string Description { get; } = "OpenNFS.Server local file system (in-process, loopback)";

        /// <inheritdoc />
        public bool CaseInsensitive
        {
            get { return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS(); }
        }

        /// <inheritdoc />
        public string ContainerId { get; } = null;

        /// <inheritdoc />
        public string ContainerSharePath { get; } = null;

        /// <inheritdoc />
        public string LocalSharePath { get; }

        /// <summary>
        /// NFSv3 port.
        /// </summary>
        public int NfsPort { get; private set; }

        /// <summary>
        /// MOUNT v3 port.
        /// </summary>
        public int MountPort { get; private set; }

        private OpenNfsServerApplication _Application = null;
        private readonly string _StateDirectory;
        private readonly string _HandleStorePath;

        private OpenNfsEphemeralServer(string root, string stateDirectory)
        {
            LocalSharePath = root;
            _StateDirectory = stateDirectory;
            _HandleStorePath = Path.Combine(stateDirectory, "handles.json");
        }

        /// <summary>
        /// Start a server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server.</returns>
        public static async Task<OpenNfsEphemeralServer> StartAsync(CancellationToken token)
        {
            string baseDirectory = Path.Combine(Path.GetTempPath(), "blobject-nfs-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(baseDirectory, "export");
            string state = Path.Combine(baseDirectory, "state");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(state);

            OpenNfsEphemeralServer server = new OpenNfsEphemeralServer(root, state);

            try
            {
                await server.StartApplicationAsync(0, 0, token).ConfigureAwait(false);
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
            ret.NfsMountPort = MountPort;
            ret.NfsShare = ExportPath;
            ret.NfsVersion = NfsVersionEnum.V3;
            return ret;
        }

        /// <inheritdoc />
        public async Task RestartAsync(CancellationToken token)
        {
            int nfsPort = NfsPort;
            int mountPort = MountPort;
            await StopApplicationAsync().ConfigureAwait(false);
            await StartApplicationAsync(nfsPort, mountPort, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await StopApplicationAsync().ConfigureAwait(false);

            try
            {
                string baseDirectory = Path.GetDirectoryName(LocalSharePath);
                if (Directory.Exists(baseDirectory)) Directory.Delete(baseDirectory, true);
            }
            catch
            {
            }
        }

        private async Task StartApplicationAsync(int nfsPort, int mountPort, CancellationToken token)
        {
            // the default handle provider embeds the full path in the handle, which exceeds the NFSv3 64-byte limit for temp paths
            OpenNfsServerApplication application = new OpenNfsServerBuilder()
                .WithServerName("blobject-test")
                .UseLocalFileSystem()
                .UseFileHandleProvider(new PersistentMappingHandleProvider(_HandleStorePath))
                .AddExport(ExportPath, LocalSharePath)
                .BuildApplication(new OpenNfsServerApplicationOptions
                {
                    ListenerAddress = "127.0.0.1",
                    EnableNfsV3 = true,
                    EnableNfs40 = false,
                    EnableNfs41 = false,
                    EnableNfs42 = false,
                    NfsPort = nfsPort,
                    MountPort = mountPort
                });

            await application.StartAsync(token).ConfigureAwait(false);
            _Application = application;
            NfsPort = application.NfsPort;
            MountPort = application.MountPort;
            await DockerCli.WaitForTcpAsync("127.0.0.1", NfsPort, TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
        }

        private async Task StopApplicationAsync()
        {
            OpenNfsServerApplication application = _Application;
            _Application = null;
            if (application == null) return;

            try { await application.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await application.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }
}
