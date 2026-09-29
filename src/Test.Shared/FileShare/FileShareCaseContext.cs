namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.CIFS;
    using Blobject.Core;
    using Blobject.NFS;

    /// <summary>
    /// Per-case context for file share suites: a prefix-scoped client, factories for unscoped clients,
    /// and helpers that inspect or modify the share directly on the server.
    /// </summary>
    public sealed class FileShareCaseContext : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Resolved options connecting to the managed server.
        /// </summary>
        public BlobProviderOptions Options { get; private set; }

        /// <summary>
        /// Managed server.
        /// </summary>
        public IFileShareServer Server { get; private set; }

        /// <summary>
        /// Client scoped to a unique prefix for this case.
        /// </summary>
        public BlobClientBase Blobs { get; private set; }

        /// <summary>
        /// Share-relative prefix used by <see cref="Blobs"/>, ending in '/'.
        /// </summary>
        public string Prefix { get; private set; }

        #endregion

        #region Private-Members

        private BlobProviderContext _Context = null;

        #endregion

        #region Constructors-and-Factories

        private FileShareCaseContext()
        {
        }

        /// <summary>
        /// Start (or reuse) the target server and create a scoped client.
        /// </summary>
        /// <param name="targetOptions">Options whose provider is a managed target.</param>
        /// <param name="caseId">Case ID.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Context.</returns>
        public static async Task<FileShareCaseContext> CreateAsync(BlobProviderOptions targetOptions, string caseId, CancellationToken token)
        {
            IFileShareServer server = await FileShareServers.GetAsync(targetOptions.Provider, token).ConfigureAwait(false);
            BlobProviderOptions resolved = server.ApplyTo(targetOptions);
            BlobProviderContext context = BlobProviderFactory.Create(resolved, caseId);

            return new FileShareCaseContext
            {
                Options = resolved,
                Server = server,
                Blobs = context.Client,
                Prefix = ((PrefixingBlobClient)context.Client).Prefix,
                _Context = context
            };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a new, unscoped client connected to the server.  The caller disposes it.
        /// </summary>
        /// <param name="configureCifs">Optional CIFS settings customization.</param>
        /// <param name="configureNfs">Optional NFS settings customization.</param>
        /// <returns>Client.</returns>
        public RawFileShareClient CreateRawClient(Action<CifsSettings> configureCifs = null, Action<NfsSettings> configureNfs = null)
        {
            if (Server.Protocol == "cifs")
            {
                CifsSettings settings = BlobProviderFactory.CreateCifsSettings(Options);
                configureCifs?.Invoke(settings);
                return new RawFileShareClient(new CifsBlobClient(settings));
            }
            else
            {
                NfsSettings settings = BlobProviderFactory.CreateNfsSettings(Options);
                configureNfs?.Invoke(settings);
                return new RawFileShareClient(new NfsBlobClient(settings));
            }
        }

        /// <summary>
        /// Remove data written by the case.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task CleanupAsync(CancellationToken token)
        {
            if (_Context?.CleanupAsync == null) return;

            try
            {
                await _Context.CleanupAsync(token).ConfigureAwait(false);
            }
            catch (Exception) when (Server.ContainerId != null || Server.LocalSharePath != null)
            {
                // cleanup failures must not mask the case result; remove the prefix directly on the server
                await ServerDeleteTreeAsync("", CancellationToken.None).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Size in bytes of a prefix-relative file as seen by the server.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Size.</returns>
        public async Task<long> ServerSizeAsync(string relativePath, CancellationToken token)
        {
            if (Server.LocalSharePath != null) return new FileInfo(LocalPath(relativePath)).Length;
            string output = await ExecAsync(new[] { "stat", "-c", "%s", ContainerPath(relativePath) }, token).ConfigureAwait(false);
            return Int64.Parse(output.Trim());
        }

        /// <summary>
        /// Lower-case hexadecimal SHA-256 of a prefix-relative file as seen by the server.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Hash.</returns>
        public async Task<string> ServerSha256Async(string relativePath, CancellationToken token)
        {
            if (Server.LocalSharePath != null) return TestData.Sha256(File.ReadAllBytes(LocalPath(relativePath)));
            string output = await ExecAsync(new[] { "sha256sum", ContainerPath(relativePath) }, token).ConfigureAwait(false);
            return output.Trim().Split(' ')[0].ToLowerInvariant();
        }

        /// <summary>
        /// Determine whether a prefix-relative path exists on the server.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if it exists.</returns>
        public Task<bool> ServerExistsAsync(string relativePath, CancellationToken token)
        {
            return ExistsAsync(Prefix + relativePath, token);
        }

        /// <summary>
        /// Determine whether a share-relative path exists on the server.
        /// </summary>
        /// <param name="sharePath">Path relative to the share root.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if it exists.</returns>
        public Task<bool> ServerExistsAtRootAsync(string sharePath, CancellationToken token)
        {
            return ExistsAsync(sharePath, token);
        }

        /// <summary>
        /// Determine whether a prefix-relative path is a directory on the server.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if it is a directory.</returns>
        public async Task<bool> ServerIsDirectoryAsync(string relativePath, CancellationToken token)
        {
            if (Server.LocalSharePath != null) return Directory.Exists(LocalPath(relativePath));
            DockerCli.DockerResult result = await DockerCli.RunAsync(new[] { "exec", Server.ContainerId, "test", "-d", ContainerPath(relativePath) }, TimeSpan.FromMinutes(1), token).ConfigureAwait(false);
            return result.Success;
        }

        /// <summary>
        /// Write a text file on the server, creating parent directories.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>.</param>
        /// <param name="text">ASCII text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ServerWriteTextAsync(string relativePath, string text, CancellationToken token)
        {
            if (Server.LocalSharePath != null)
            {
                string path = LocalPath(relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, text, new UTF8Encoding(false));
                return;
            }

            string target = ContainerPath(relativePath);
            string dir = target.Substring(0, target.LastIndexOf('/'));
            await ExecAsync(new[] { "mkdir", "-p", dir }, token).ConfigureAwait(false);
            await ExecAsync(new[] { "sh", "-c", "printf '%s' \"$1\" > \"$2\"", "sh", text, target }, token).ConfigureAwait(false);
            await OpenPermissionsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a directory on the server, creating parent directories.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task ServerCreateDirectoryAsync(string relativePath, CancellationToken token)
        {
            if (Server.LocalSharePath != null)
            {
                Directory.CreateDirectory(LocalPath(relativePath));
                return;
            }

            await ExecAsync(new[] { "mkdir", "-p", ContainerPath(relativePath) }, token).ConfigureAwait(false);
            await OpenPermissionsAsync(token).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove a prefix-relative tree directly on the server.  Never throws.
        /// </summary>
        /// <param name="relativePath">Path relative to <see cref="Prefix"/>; empty for the whole prefix.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task ServerDeleteTreeAsync(string relativePath, CancellationToken token)
        {
            return ServerFiles.DeleteTreeAsync(Server, Prefix + relativePath, token);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _Context?.Dispose();
            _Context = null;
        }

        #endregion

        #region Private-Methods

        private async Task<bool> ExistsAsync(string sharePath, CancellationToken token)
        {
            if (Server.LocalSharePath != null)
            {
                string path = Path.Combine(Server.LocalSharePath, sharePath.Replace('/', Path.DirectorySeparatorChar));
                return File.Exists(path) || Directory.Exists(path);
            }

            DockerCli.DockerResult result = await DockerCli.RunAsync(
                new[] { "exec", Server.ContainerId, "test", "-e", Server.ContainerSharePath + "/" + sharePath.TrimStart('/') },
                TimeSpan.FromMinutes(1),
                token).ConfigureAwait(false);
            return result.Success;
        }

        private string LocalPath(string relativePath)
        {
            return Path.Combine(Server.LocalSharePath, (Prefix + relativePath).TrimEnd('/').Replace('/', Path.DirectorySeparatorChar));
        }

        private string ContainerPath(string relativePath)
        {
            return (Server.ContainerSharePath + "/" + Prefix + relativePath).TrimEnd('/');
        }

        private Task<string> ExecAsync(IEnumerable<string> command, CancellationToken token)
        {
            return DockerCli.ExecAsync(Server.ContainerId, command, token);
        }

        private Task OpenPermissionsAsync(CancellationToken token)
        {
            // files created as root in the container must remain writable by the share's user
            return ExecAsync(new[] { "chmod", "-R", "a+rwX", ContainerPath("") }, token);
        }

        #endregion
    }
}
