namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal Docker CLI wrapper used to run ephemeral file servers for tests.
    /// Every container is labeled so it can be found and removed later, containers are always started with --rm,
    /// and all containers started by this process are removed when the process exits.
    /// </summary>
    public static class DockerCli
    {
        /// <summary>
        /// Label applied to every container started by the test harness.
        /// </summary>
        public const string Label = "blobject.test";

        private static readonly ConcurrentDictionary<string, string> _Containers = new ConcurrentDictionary<string, string>();
        private static readonly object _HookLock = new object();
        private static bool _HooksRegistered = false;
        private static bool? _Available = null;

        /// <summary>
        /// Result of a Docker CLI invocation.
        /// </summary>
        public sealed class DockerResult
        {
            /// <summary>
            /// Exit code.
            /// </summary>
            public int ExitCode { get; set; }

            /// <summary>
            /// Standard output.
            /// </summary>
            public string Output { get; set; } = "";

            /// <summary>
            /// Standard error.
            /// </summary>
            public string Error { get; set; } = "";

            /// <summary>
            /// True if the exit code is zero.
            /// </summary>
            public bool Success
            {
                get { return ExitCode == 0; }
            }
        }

        /// <summary>
        /// Determine whether a Docker engine with Linux containers is reachable.
        /// </summary>
        /// <returns>True if available.</returns>
        public static bool IsAvailable()
        {
            if (_Available.HasValue) return _Available.Value;

            try
            {
                DockerResult result = RunAsync(new[] { "version", "--format", "{{.Server.Os}}" }, TimeSpan.FromSeconds(30), CancellationToken.None).GetAwaiter().GetResult();
                _Available = result.Success && result.Output.Trim().Equals("linux", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                _Available = false;
            }

            return _Available.Value;
        }

        /// <summary>
        /// Run a Docker CLI command.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Result.</returns>
        public static async Task<DockerResult> RunAsync(IEnumerable<string> args, TimeSpan timeout, CancellationToken token)
        {
            ProcessStartInfo psi = new ProcessStartInfo("docker")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            foreach (string arg in args) psi.ArgumentList.Add(arg);

            using (Process process = new Process { StartInfo = psi })
            {
                StringBuilder stdout = new StringBuilder();
                StringBuilder stderr = new StringBuilder();
                process.OutputDataReceived += (s, e) => { if (e.Data != null) lock (stdout) stdout.AppendLine(e.Data); };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (stderr) stderr.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    cts.CancelAfter(timeout);

                    try
                    {
                        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        try { process.Kill(true); } catch { }
                        if (token.IsCancellationRequested) throw;
                        throw new TimeoutException("docker " + String.Join(" ", args) + " timed out after " + timeout + ".");
                    }
                }

                process.WaitForExit();

                return new DockerResult
                {
                    ExitCode = process.ExitCode,
                    Output = stdout.ToString(),
                    Error = stderr.ToString()
                };
            }
        }

        /// <summary>
        /// Run a Docker CLI command and throw if it fails.
        /// </summary>
        /// <param name="args">Arguments.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Standard output.</returns>
        public static async Task<string> RunCheckedAsync(IEnumerable<string> args, TimeSpan timeout, CancellationToken token)
        {
            List<string> list = args.ToList();
            DockerResult result = await RunAsync(list, timeout, token).ConfigureAwait(false);
            if (!result.Success)
                throw new InvalidOperationException("docker " + String.Join(" ", list) + " failed with exit code " + result.ExitCode + ":" + Environment.NewLine + result.Error + result.Output);
            return result.Output;
        }

        /// <summary>
        /// Build an image from a context directory.  Docker's layer cache makes repeated builds fast.
        /// </summary>
        /// <param name="contextDirectory">Build context directory containing a Dockerfile.</param>
        /// <param name="tag">Image tag.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static Task BuildImageAsync(string contextDirectory, string tag, CancellationToken token)
        {
            return RunCheckedAsync(new[] { "build", "--quiet", "--tag", tag, contextDirectory }, TimeSpan.FromMinutes(15), token);
        }

        /// <summary>
        /// Start a detached, auto-removed, labeled container.
        /// </summary>
        /// <param name="image">Image.</param>
        /// <param name="namePrefix">Container name prefix.</param>
        /// <param name="publish">Port publications, e.g. 127.0.0.1::445 or 127.0.0.1:20048:20048.</param>
        /// <param name="environment">Environment variables.</param>
        /// <param name="privileged">Run privileged.</param>
        /// <param name="volumes">Container paths to back with anonymous volumes, which are removed with the container.</param>
        /// <param name="capabilities">Linux capabilities to add, e.g. DAC_READ_SEARCH.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Container ID.</returns>
        public static async Task<string> RunContainerAsync(
            string image,
            string namePrefix,
            IEnumerable<string> publish,
            IDictionary<string, string> environment,
            bool privileged,
            IEnumerable<string> volumes,
            IEnumerable<string> capabilities,
            CancellationToken token)
        {
            EnsureHooks();

            string name = namePrefix + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            List<string> args = new List<string> { "run", "--detach", "--rm", "--name", name, "--label", Label + "=true", "--label", Label + ".pid=" + Environment.ProcessId };
            if (privileged) args.Add("--privileged");

            if (capabilities != null)
            {
                foreach (string c in capabilities)
                {
                    args.Add("--cap-add");
                    args.Add(c);
                }
            }

            if (volumes != null)
            {
                foreach (string v in volumes)
                {
                    args.Add("--volume");
                    args.Add(v);
                }
            }

            if (publish != null)
            {
                foreach (string p in publish)
                {
                    args.Add("--publish");
                    args.Add(p);
                }
            }

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> kvp in environment)
                {
                    args.Add("--env");
                    args.Add(kvp.Key + "=" + kvp.Value);
                }
            }

            args.Add(image);

            // record the name before starting so it is cleaned up even if startup is interrupted
            _Containers[name] = name;

            string id = (await RunCheckedAsync(args, TimeSpan.FromMinutes(2), token).ConfigureAwait(false)).Trim();
            _Containers.TryRemove(name, out _);
            _Containers[id] = name;
            return id;
        }

        /// <summary>
        /// Get the host port published for a container port.
        /// </summary>
        /// <param name="containerId">Container ID.</param>
        /// <param name="containerPort">Container port, e.g. 445/tcp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Host port.</returns>
        public static async Task<int> GetHostPortAsync(string containerId, string containerPort, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(30);

            while (true)
            {
                DockerResult result = await RunAsync(new[] { "port", containerId, containerPort }, TimeSpan.FromSeconds(30), token).ConfigureAwait(false);

                if (result.Success)
                {
                    foreach (string line in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        int colon = line.LastIndexOf(':');
                        if (colon > 0 && Int32.TryParse(line.Substring(colon + 1).Trim(), out int port) && port > 0) return port;
                    }
                }

                if (DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Docker did not publish " + containerPort + " for container " + containerId + ": " + result.Error + result.Output);

                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Execute a command in a running container.
        /// </summary>
        /// <param name="containerId">Container ID.</param>
        /// <param name="command">Command and arguments.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Standard output.</returns>
        public static Task<string> ExecAsync(string containerId, IEnumerable<string> command, CancellationToken token)
        {
            List<string> args = new List<string> { "exec", containerId };
            args.AddRange(command);
            return RunCheckedAsync(args, TimeSpan.FromMinutes(2), token);
        }

        /// <summary>
        /// Get container logs.
        /// </summary>
        /// <param name="containerId">Container ID.</param>
        /// <returns>Logs.</returns>
        public static async Task<string> GetLogsAsync(string containerId)
        {
            try
            {
                DockerResult result = await RunAsync(new[] { "logs", "--tail", "200", containerId }, TimeSpan.FromSeconds(30), CancellationToken.None).ConfigureAwait(false);
                return result.Output + result.Error;
            }
            catch (Exception e)
            {
                return "(unable to read logs: " + e.Message + ")";
            }
        }

        /// <summary>
        /// Stop and remove a container, retrying until Docker confirms it is gone.  Never throws.
        /// A graceful stop lets entrypoints release kernel resources (for example knfsd threads), which a
        /// forced remove alone can fail on with "did not receive an exit event".
        /// </summary>
        /// <param name="containerId">Container ID or name.</param>
        /// <returns>Task.</returns>
        public static async Task RemoveContainerAsync(string containerId)
        {
            if (String.IsNullOrEmpty(containerId)) return;

            try
            {
                await RunAsync(new[] { "stop", "--time", "10", containerId }, TimeSpan.FromMinutes(1), CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
            }

            for (int attempt = 0; attempt < 12; attempt++)
            {
                try
                {
                    await RunAsync(new[] { "rm", "--force", "--volumes", containerId }, TimeSpan.FromMinutes(1), CancellationToken.None).ConfigureAwait(false);

                    DockerResult remaining = await RunAsync(
                        new[] { "ps", "--all", "--quiet", "--no-trunc", "--filter", "id=" + containerId },
                        TimeSpan.FromSeconds(30),
                        CancellationToken.None).ConfigureAwait(false);

                    DockerResult remainingByName = await RunAsync(
                        new[] { "ps", "--all", "--quiet", "--filter", "name=^/" + containerId + "$" },
                        TimeSpan.FromSeconds(30),
                        CancellationToken.None).ConfigureAwait(false);

                    if (remaining.Success && remainingByName.Success
                        && String.IsNullOrWhiteSpace(remaining.Output) && String.IsNullOrWhiteSpace(remainingByName.Output))
                    {
                        break;
                    }
                }
                catch
                {
                }

                await Task.Delay(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
            }

            _Containers.TryRemove(containerId, out _);
        }

        /// <summary>
        /// Remove every container started by this process.  Never throws.
        /// </summary>
        /// <returns>Task.</returns>
        public static async Task RemoveAllStartedAsync()
        {
            List<string> ids = _Containers.Keys.ToList();
            await Task.WhenAll(ids.Select(RemoveContainerAsync)).ConfigureAwait(false);
        }

        /// <summary>
        /// Remove labeled containers left behind by earlier test processes that are no longer running.  Never throws.
        /// </summary>
        /// <returns>Number of containers removed.</returns>
        public static async Task<int> RemoveOrphanedContainersAsync()
        {
            int removed = 0;

            try
            {
                DockerResult result = await RunAsync(
                    new[] { "ps", "--all", "--filter", "label=" + Label + "=true", "--format", "{{.ID}} {{.Label \"" + Label + ".pid\"}}" },
                    TimeSpan.FromSeconds(30),
                    CancellationToken.None).ConfigureAwait(false);

                if (!result.Success) return 0;

                foreach (string line in result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] parts = line.Trim().Split(' ');
                    if (parts.Length < 1) continue;

                    bool owned = parts.Length > 1 && Int32.TryParse(parts[1], out int pid) && pid == Environment.ProcessId;
                    bool alive = parts.Length > 1 && Int32.TryParse(parts[1], out int otherPid) && IsProcessAlive(otherPid);
                    if (owned || alive) continue;

                    await RemoveContainerAsync(parts[0]).ConfigureAwait(false);
                    removed++;
                }
            }
            catch
            {
            }

            return removed;
        }

        /// <summary>
        /// Reserve a free TCP port on the loopback interface.
        /// </summary>
        /// <returns>Port.</returns>
        public static int GetFreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();

            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// Wait until a TCP connection to the endpoint succeeds.
        /// </summary>
        /// <param name="host">Host.</param>
        /// <param name="port">Port.</param>
        /// <param name="timeout">Timeout.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task WaitForTcpAsync(string host, int port, TimeSpan timeout, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.Add(timeout);
            Exception last = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using (TcpClient client = new TcpClient())
                    {
                        Task connect = client.ConnectAsync(host, port);
                        if (await Task.WhenAny(connect, Task.Delay(2000, token)).ConfigureAwait(false) == connect)
                        {
                            await connect.ConfigureAwait(false);
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    last = e;
                }

                await Task.Delay(250, token).ConfigureAwait(false);
            }

            throw new TimeoutException("Timed out waiting for " + host + ":" + port + " to accept connections.", last);
        }

        private static bool IsProcessAlive(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid))
                {
                    return !p.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureHooks()
        {
            lock (_HookLock)
            {
                if (_HooksRegistered) return;

                AppDomain.CurrentDomain.ProcessExit += (s, e) => RemoveAllStartedAsync().GetAwaiter().GetResult();
                Console.CancelKeyPress += (s, e) => RemoveAllStartedAsync().GetAwaiter().GetResult();
                _HooksRegistered = true;
            }
        }
    }
}
