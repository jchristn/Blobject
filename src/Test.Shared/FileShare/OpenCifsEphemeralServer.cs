namespace Test.Shared.FileShare
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenCIFS.Server;

    /// <summary>
    /// In-process OpenCIFS server on a loopback port, serving a temporary directory.
    /// </summary>
    public sealed class OpenCifsEphemeralServer : IFileShareServer
    {
        /// <summary>
        /// Username accepted by the server.
        /// </summary>
        public const string UserName = "blobject";

        /// <summary>
        /// Password accepted by the server.
        /// </summary>
        public const string Password = "Blobject-Test-123!";

        /// <summary>
        /// Share name.
        /// </summary>
        public const string ShareName = "share";

        /// <inheritdoc />
        public string TargetName { get; } = "cifs-ephemeral";

        /// <inheritdoc />
        public string Protocol { get; } = "cifs";

        /// <inheritdoc />
        public string Description { get; } = "OpenCIFS.Server (in-process, loopback)";

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
        /// Loopback port.
        /// </summary>
        public int Port { get; }

        private OpenCifsServerApplication _Application = null;

        private OpenCifsEphemeralServer(string root, int port)
        {
            LocalSharePath = root;
            Port = port;
        }

        /// <summary>
        /// Start a server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server.</returns>
        public static async Task<OpenCifsEphemeralServer> StartAsync(CancellationToken token)
        {
            string root = Path.Combine(Path.GetTempPath(), "blobject-cifs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            OpenCifsEphemeralServer server = new OpenCifsEphemeralServer(root, DockerCli.GetFreeTcpPort());

            try
            {
                await server.StartApplicationAsync(token).ConfigureAwait(false);
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
            ret.Provider = "cifs";
            ret.TargetName = TargetName;
            ret.CifsHostname = "127.0.0.1";
            ret.CifsPort = Port;
            ret.CifsUsername = UserName;
            ret.CifsPassword = Password;
            ret.CifsDomain = "WORKGROUP";
            ret.CifsShare = ShareName;

            // OpenCIFS negotiates at most SMB 3.0.2, whose only cipher is AES-128-CCM; platforms without it (macOS) cannot encrypt.
            ret.CifsPreferEncryption = ret.CifsPreferEncryption && AesCcm.IsSupported;
            return ret;
        }

        /// <inheritdoc />
        public async Task RestartAsync(CancellationToken token)
        {
            await StopApplicationAsync().ConfigureAwait(false);
            await StartApplicationAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await StopApplicationAsync().ConfigureAwait(false);

            try
            {
                if (Directory.Exists(LocalSharePath)) Directory.Delete(LocalSharePath, true);
            }
            catch
            {
            }
        }

        private async Task StartApplicationAsync(CancellationToken token)
        {
            OpenCifsServerApplication application = new OpenCifsServerBuilder()
                .WithServerName("blobject-test")
                .WithBindAddress("127.0.0.1")
                .WithBindPort(Port)
                .AddAccount(new OpenCifsServerAccount
                {
                    UserName = UserName,
                    UserDomain = "WORKGROUP",
                    Password = Password
                })
                .AddSrvsvcShareEnumerationEndpoint()
                .AddFileSystemShare(ShareName, LocalSharePath)
                .BuildApplication(e => { });

            await application.StartAsync(token).ConfigureAwait(false);
            _Application = application;
            await DockerCli.WaitForTcpAsync("127.0.0.1", Port, TimeSpan.FromSeconds(30), token).ConfigureAwait(false);
        }

        private async Task StopApplicationAsync()
        {
            OpenCifsServerApplication application = _Application;
            _Application = null;
            if (application == null) return;

            try { await application.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            try { await application.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }
}
