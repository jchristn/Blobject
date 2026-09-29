namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Catalog of managed CIFS/SMB and NFS test targets, and a process-wide registry of started servers.
    /// Servers start lazily on first use, are shared by every test in the process, and are always stopped
    /// (containers removed, temporary directories deleted) by <see cref="DisposeAllAsync"/> or at process exit.
    /// </summary>
    public static class FileShareServers
    {
        /// <summary>
        /// Target that runs OpenCIFS.Server in-process.
        /// </summary>
        public const string CifsEphemeral = "cifs-ephemeral";

        /// <summary>
        /// Target that runs Samba in Docker.
        /// </summary>
        public const string CifsSamba = "cifs-samba";

        /// <summary>
        /// Target that runs OpenNFS.Server in-process.
        /// </summary>
        public const string NfsEphemeral = "nfs-ephemeral";

        /// <summary>
        /// Target that runs nfs-ganesha in Docker.
        /// </summary>
        public const string NfsGanesha = "nfs-ganesha";

        /// <summary>
        /// Target that runs the Linux kernel NFS server in a privileged Docker container.
        /// </summary>
        public const string NfsKnfsd = "nfs-knfsd";

        /// <summary>
        /// Target that runs unfs3 in Docker.
        /// </summary>
        public const string NfsUnfs3 = "nfs-unfs3";

        /// <summary>
        /// All managed targets.
        /// </summary>
        public static IReadOnlyList<string> AllTargets { get; } = new[] { CifsEphemeral, CifsSamba, NfsEphemeral, NfsGanesha, NfsKnfsd, NfsUnfs3 };

        /// <summary>
        /// Managed targets that run in-process and need no Docker engine.
        /// </summary>
        public static IReadOnlyList<string> InProcessTargets { get; } = new[] { CifsEphemeral, NfsEphemeral };

        private static readonly ConcurrentDictionary<string, Lazy<Task<IFileShareServer>>> _Servers =
            new ConcurrentDictionary<string, Lazy<Task<IFileShareServer>>>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _HookLock = new object();
        private static bool _HookRegistered = false;

        /// <summary>
        /// Determine whether a provider name refers to a managed target.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <returns>True if managed.</returns>
        public static bool IsManagedTarget(string provider)
        {
            return !String.IsNullOrEmpty(provider) && AllTargets.Contains(provider, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determine whether a managed target requires Docker.
        /// </summary>
        /// <param name="target">Target name.</param>
        /// <returns>True if Docker is required.</returns>
        public static bool RequiresDocker(string target)
        {
            return !InProcessTargets.Contains(target, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Protocol of a managed target, cifs or nfs.
        /// </summary>
        /// <param name="target">Target name.</param>
        /// <returns>Protocol.</returns>
        public static string ProtocolOf(string target)
        {
            return target.StartsWith("cifs", StringComparison.OrdinalIgnoreCase) ? "cifs" : "nfs";
        }

        /// <summary>
        /// Parse a comma-separated target list.  Accepts target names plus the groups all, cifs, nfs, inprocess, and docker.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Targets.</returns>
        public static List<string> ParseTargets(string value)
        {
            List<string> ret = new List<string>();
            if (String.IsNullOrWhiteSpace(value)) return ret;

            foreach (string raw in value.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string item = raw.Trim().ToLowerInvariant();
                IEnumerable<string> expanded;

                switch (item)
                {
                    case "all":
                        expanded = AllTargets;
                        break;
                    case "cifs":
                    case "smb":
                        expanded = AllTargets.Where(t => ProtocolOf(t) == "cifs");
                        break;
                    case "nfs":
                        expanded = AllTargets.Where(t => ProtocolOf(t) == "nfs");
                        break;
                    case "inprocess":
                    case "ephemeral":
                        expanded = InProcessTargets;
                        break;
                    case "docker":
                        expanded = AllTargets.Where(RequiresDocker);
                        break;
                    default:
                        if (!IsManagedTarget(item)) throw new ArgumentException("Unknown file share target '" + raw + "'.");
                        expanded = new[] { item };
                        break;
                }

                foreach (string t in expanded)
                {
                    if (!ret.Contains(t)) ret.Add(t);
                }
            }

            return ret;
        }

        /// <summary>
        /// Get, starting if necessary, the server for a managed target.
        /// </summary>
        /// <param name="target">Target name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Server.</returns>
        public static async Task<IFileShareServer> GetAsync(string target, CancellationToken token = default)
        {
            if (!IsManagedTarget(target)) throw new ArgumentException("Unknown file share target '" + target + "'.");
            EnsureHook();

            Lazy<Task<IFileShareServer>> lazy = _Servers.GetOrAdd(
                target,
                t => new Lazy<Task<IFileShareServer>>(() => StartAsync(t), LazyThreadSafetyMode.ExecutionAndPublication));

            try
            {
                return await lazy.Value.ConfigureAwait(false);
            }
            catch
            {
                // allow a later attempt to retry startup
                _Servers.TryRemove(new KeyValuePair<string, Lazy<Task<IFileShareServer>>>(target, lazy));
                throw;
            }
        }

        /// <summary>
        /// Stop every started server.  Never throws.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task DisposeAllAsync()
        {
            List<Lazy<Task<IFileShareServer>>> started = _Servers.Values.ToList();
            _Servers.Clear();

            foreach (Lazy<Task<IFileShareServer>> lazy in started)
            {
                if (!lazy.IsValueCreated) continue;

                try
                {
                    IFileShareServer server = await lazy.Value.ConfigureAwait(false);
                    await server.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }

            await DockerCli.RemoveAllStartedAsync().ConfigureAwait(false);
        }

        private static async Task<IFileShareServer> StartAsync(string target)
        {
            // Startup is shared by every test that needs the server, so it is not tied to any one test's token.
            using (CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMinutes(20)))
            {
                CancellationToken token = cts.Token;

                if (RequiresDocker(target))
                {
                    if (!DockerCli.IsAvailable())
                        throw new InvalidOperationException("Target '" + target + "' requires a Docker engine running Linux containers, and none is available.");

                    await DockerCli.RemoveOrphanedContainersAsync().ConfigureAwait(false);
                }

                switch (target.ToLowerInvariant())
                {
                    case CifsEphemeral:
                        return await OpenCifsEphemeralServer.StartAsync(token).ConfigureAwait(false);
                    case CifsSamba:
                        return await SambaDockerServer.StartAsync(token).ConfigureAwait(false);
                    case NfsEphemeral:
                        return await OpenNfsEphemeralServer.StartAsync(token).ConfigureAwait(false);
                    case NfsGanesha:
                        return await NfsDockerServer.StartAsync(NfsDockerServer.Flavor.Ganesha, token).ConfigureAwait(false);
                    case NfsKnfsd:
                        return await NfsDockerServer.StartAsync(NfsDockerServer.Flavor.Knfsd, token).ConfigureAwait(false);
                    case NfsUnfs3:
                        return await NfsDockerServer.StartAsync(NfsDockerServer.Flavor.Unfs3, token).ConfigureAwait(false);
                    default:
                        throw new ArgumentException("Unknown file share target '" + target + "'.");
                }
            }
        }

        private static void EnsureHook()
        {
            lock (_HookLock)
            {
                if (_HookRegistered) return;
                AppDomain.CurrentDomain.ProcessExit += (s, e) => DisposeAllAsync().GetAwaiter().GetResult();
                _HookRegistered = true;
            }
        }
    }
}
