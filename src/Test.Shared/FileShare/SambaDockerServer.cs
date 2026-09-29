namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.CIFS;

    /// <summary>
    /// Samba smbd running in an ephemeral Docker container.
    /// </summary>
    public sealed class SambaDockerServer : IFileShareServer
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

        private const string Image = "blobject-test-samba:1";

        /// <inheritdoc />
        public string TargetName { get; } = "cifs-samba";

        /// <inheritdoc />
        public string Protocol { get; } = "cifs";

        /// <inheritdoc />
        public string Description { get; } = "Samba smbd (Debian bookworm, Docker)";

        /// <inheritdoc />
        public bool CaseInsensitive { get; } = true;

        /// <inheritdoc />
        public string ContainerId { get; private set; }

        /// <inheritdoc />
        public string ContainerSharePath { get; } = "/srv/share";

        /// <inheritdoc />
        public string LocalSharePath { get; } = null;

        /// <summary>
        /// Host port mapped to 445.
        /// </summary>
        public int Port { get; private set; }

        private SambaDockerServer()
        {
        }

        /// <summary>
        /// Build the image if needed and start a container.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server.</returns>
        public static async Task<SambaDockerServer> StartAsync(CancellationToken token)
        {
            await DockerCli.BuildImageAsync(Path.Combine(AppContext.BaseDirectory, "Docker", "samba"), Image, token).ConfigureAwait(false);

            SambaDockerServer server = new SambaDockerServer();
            server.Port = DockerCli.GetFreeTcpPort();

            try
            {
                server.ContainerId = await DockerCli.RunContainerAsync(
                    Image,
                    "blobject-samba",
                    new[] { "127.0.0.1:" + server.Port + ":445" },
                    new Dictionary<string, string>
                    {
                        { "SMB_USER", UserName },
                        { "SMB_PASSWORD", Password },
                        { "SMB_SHARE", ShareName }
                    },
                    false,
                    new[] { "/srv/share" },
                    null,
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
            ret.Provider = "cifs";
            ret.TargetName = TargetName;
            ret.CifsHostname = "127.0.0.1";
            ret.CifsPort = Port;
            ret.CifsUsername = UserName;
            ret.CifsPassword = Password;
            ret.CifsDomain = "WORKGROUP";
            ret.CifsShare = ShareName;
            return ret;
        }

        /// <inheritdoc />
        public async Task RestartAsync(CancellationToken token)
        {
            await DockerCli.RunCheckedAsync(new[] { "restart", "--time", "1", ContainerId }, TimeSpan.FromMinutes(2), token).ConfigureAwait(false);
            await WaitUntilReadyAsync(token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            await DockerCli.RemoveContainerAsync(ContainerId).ConfigureAwait(false);
            ContainerId = null;
        }

        private async Task WaitUntilReadyAsync(CancellationToken token)
        {
            await DockerCli.WaitForTcpAsync("127.0.0.1", Port, TimeSpan.FromSeconds(60), token).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(60);

            while (true)
            {
                using (CifsBlobClient client = new CifsBlobClient(new CifsSettings("127.0.0.1", Port, UserName, Password, ShareName)))
                {
                    if (await client.ValidateConnectivity(token).ConfigureAwait(false)) return;
                }

                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Samba container did not become ready:" + Environment.NewLine + await DockerCli.GetLogsAsync(ContainerId).ConfigureAwait(false));

                await Task.Delay(500, token).ConfigureAwait(false);
            }
        }
    }
}
