namespace Test.Shared.FileShare
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Direct server-side file operations for managed servers.
    /// </summary>
    public static class ServerFiles
    {
        /// <summary>
        /// Remove a share-relative file or directory tree on the server.  Never throws.
        /// </summary>
        /// <param name="server">Server.</param>
        /// <param name="sharePath">Path relative to the share root, using '/' separators.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task DeleteTreeAsync(IFileShareServer server, string sharePath, CancellationToken token)
        {
            string relative = (sharePath ?? "").Replace('\\', '/').Trim('/');
            if (String.IsNullOrEmpty(relative) || relative.Contains("..")) return;

            try
            {
                if (server.LocalSharePath != null)
                {
                    string path = Path.Combine(server.LocalSharePath, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (Directory.Exists(path)) Directory.Delete(path, true);
                    else if (File.Exists(path)) File.Delete(path);
                    return;
                }

                if (server.ContainerId != null)
                {
                    await DockerCli.ExecAsync(server.ContainerId, new[] { "rm", "-rf", server.ContainerSharePath + "/" + relative }, token).ConfigureAwait(false);
                }
            }
            catch
            {
            }
        }
    }
}
