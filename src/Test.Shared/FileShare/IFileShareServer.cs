namespace Test.Shared.FileShare
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An ephemeral CIFS/SMB or NFS server started for the duration of a test run.
    /// </summary>
    public interface IFileShareServer : IAsyncDisposable
    {
        /// <summary>
        /// Target name, e.g. cifs-samba.
        /// </summary>
        string TargetName { get; }

        /// <summary>
        /// Protocol, either cifs or nfs.
        /// </summary>
        string Protocol { get; }

        /// <summary>
        /// Human-readable description of the server implementation.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// True if the server's storage resolves names case-insensitively (for example NTFS, or Samba's default).
        /// This is independent of the provider's enumeration filter matching, which is case-insensitive for CIFS and case-sensitive for NFS.
        /// </summary>
        bool CaseInsensitive { get; }

        /// <summary>
        /// Container ID when the server runs in Docker, otherwise null.
        /// </summary>
        string ContainerId { get; }

        /// <summary>
        /// Path of the share root inside the container when the server runs in Docker, otherwise null.
        /// </summary>
        string ContainerSharePath { get; }

        /// <summary>
        /// Path of the share root on the local file system when the server runs in-process, otherwise null.
        /// </summary>
        string LocalSharePath { get; }

        /// <summary>
        /// Apply connection settings for this server to a copy of the supplied options.
        /// </summary>
        /// <param name="options">Options.</param>
        /// <returns>Options configured to connect to this server.</returns>
        BlobProviderOptions ApplyTo(BlobProviderOptions options);

        /// <summary>
        /// Restart the server, preserving its endpoint and stored data.
        /// Existing connections are dropped.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        Task RestartAsync(CancellationToken token);
    }
}
