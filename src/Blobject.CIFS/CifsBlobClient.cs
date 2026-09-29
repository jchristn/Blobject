namespace Blobject.CIFS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Sockets;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using OpenCIFS.Client;
    using OpenCIFS.Protocol;

    /// <summary>
    /// BLOB client for CIFS/SMB file shares, backed by OpenCIFS.
    /// Keys map to paths within the share, using '/' as the separator.  Folders are created on demand when writing,
    /// a key ending in '/' refers to a folder, and enumeration returns files and folders, with each folder
    /// (IsFolder true, key ending in '/') returned after its contents.  Characters that SMB reserves
    /// (&lt; &gt; : " | ? * and control characters) are not permitted in keys.
    /// </summary>
    public class CifsBlobClient : BlobClientBase, IDisposable, IAsyncDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[CifsBlobClient] ";
        private CifsSettings _CifsSettings = null;
        private bool _Disposed = false;

        private const string _ReservedCharacters = "<>:\"|?*";
        private readonly Connection[] _Connections;
        private int _NextConnection = -1;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="CifsBlobClient"/> class.
        /// The connection to the server is established on first use.
        /// </summary>
        /// <param name="cifsSettings">Settings for <see cref="CifsBlobClient"/>.</param>
        public CifsBlobClient(CifsSettings cifsSettings)
        {
            if (cifsSettings == null) throw new ArgumentNullException(nameof(cifsSettings));
            if (String.IsNullOrEmpty(cifsSettings.Username)) throw new ArgumentException("A username is required.", nameof(cifsSettings));
            if (String.IsNullOrEmpty(cifsSettings.Password)) throw new ArgumentException("A password is required.", nameof(cifsSettings));
            if (String.IsNullOrEmpty(cifsSettings.Share)) throw new ArgumentException("A share is required.", nameof(cifsSettings));

            _CifsSettings = cifsSettings;
            _Connections = new Connection[cifsSettings.MaxConnections];
            for (int i = 0; i < _Connections.Length; i++) _Connections[i] = new Connection(this);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_Disposed) return;

            if (disposing)
            {
                Log("disposing");
                _Disposed = true;
                Task.Run(() => CloseAllConnectionsAsync()).GetAwaiter().GetResult();
            }

            _Disposed = true;
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose asynchronously, disconnecting from the server.
        /// </summary>
        /// <returns>Task.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;

            Log("disposing");
            _Disposed = true;
            await CloseAllConnectionsAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override async Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            try
            {
                await ExecuteAsync(share => share.Metadata.GetAttributesAsync("", token), token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log("connectivity validation failed: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// List shares available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of share names.</returns>
        public async Task<List<string>> ListShares(CancellationToken token = default)
        {
            Log("retrieving shares for " + _CifsSettings.Hostname + " using username " + _CifsSettings.Username);

            OpenCifsRemoteShareInfo[] shares = await ExecuteAsync(
                (connection, share) => connection.Client.EnumerateSharesAsync(token),
                token,
                null,
                true).ConfigureAwait(false);

            List<string> ret = new List<string>();
            if (shares != null)
            {
                foreach (OpenCifsRemoteShareInfo info in shares)
                {
                    if (info != null && !String.IsNullOrEmpty(info.Name)) ret.Add(info.Name);
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public override async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            string path = KeyToPath(key, out _);

            return await ExecuteAsync(async share =>
            {
                try
                {
                    return await share.Files.ReadAllBytesAsync(path, token).ConfigureAwait(false);
                }
                catch (OpenCifsStatusException e) when (e.Status == NtStatus.FileIsADirectory)
                {
                    return Array.Empty<byte>();
                }
            }, token, key).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            string path = KeyToPath(key, out _);

            return await ExecuteAsync(async share =>
            {
                try
                {
                    Stream stream = await share.Files.OpenReadAsync(path, token).ConfigureAwait(false);
                    return new BlobData(stream.Length, stream);
                }
                catch (OpenCifsStatusException e) when (e.Status == NtStatus.FileIsADirectory)
                {
                    return new BlobData(0, new MemoryStream(Array.Empty<byte>()));
                }
            }, token, key).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            string path = KeyToPath(key, out bool isFolderKey);

            OpenCifsClientFileMetadata md = await ExecuteAsync(
                share => share.Metadata.GetAttributesAsync(path, token),
                token,
                key).ConfigureAwait(false);

            if (isFolderKey && !md.IsDirectory) throw new KeyNotFoundException("The requested object was not found.");

            return new BlobMetadata
            {
                Key = PathToKey(path, md.IsDirectory),
                IsFolder = md.IsDirectory,
                ContentType = "application/octet-stream",
                ContentLength = md.IsDirectory ? 0 : ToLength(md.EndOfFile),
                CreatedUtc = md.CreationTimeUtc,
                LastAccessUtc = md.LastAccessTimeUtc,
                LastUpdateUtc = md.LastWriteTimeUtc
            };
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (data == null) data = "";
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            if (data == null) data = Array.Empty<byte>();
            string path = KeyToPath(key, out bool isFolderKey);

            if (isFolderKey)
            {
                await CreateFolderAsync(path, token).ConfigureAwait(false);
                return;
            }

            await WriteFileAsync(path, share => share.Files.WriteAllBytesAsync(path, data, token), token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
            if (stream == null)
            {
                if (contentLength > 0) throw new ArgumentNullException(nameof(stream));
                stream = new MemoryStream(Array.Empty<byte>());
            }

            if (!stream.CanRead) throw new ArgumentException("The supplied stream is not readable.", nameof(stream));

            string path = KeyToPath(key, out bool isFolderKey);

            if (isFolderKey)
            {
                await CreateFolderAsync(path, token).ConfigureAwait(false);
                return;
            }

            // The stream can only be consumed once, so this write is not retried after a connection failure.
            Stream source = new LengthLimitedReadStream(stream, contentLength);
            await WriteFileAsync(path, share => share.Files.WriteAsync(path, source, null, token), token, retryOnConnectionFailure: false).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            await base.WriteManyAsync(objects, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <remarks>Deleting a key that does not exist succeeds.  Deleting a folder requires the folder to be empty.</remarks>
        public override async Task DeleteAsync(string key, CancellationToken token = default)
        {
            string path = KeyToPath(key, out bool isFolderKey);

            // the not-empty failure is returned rather than thrown inside ExecuteAsync, where an IOException means a failed connection
            OpenCifsStatusException notEmpty = await ExecuteAsync<OpenCifsStatusException>(async share =>
            {
                OpenCifsClientFileMetadata md;

                try
                {
                    md = await share.Metadata.GetAttributesAsync(path, token).ConfigureAwait(false);
                }
                catch (OpenCifsStatusException e) when (IsNotFound(e.Status))
                {
                    return null;
                }

                if (isFolderKey && !md.IsDirectory) return null;

                try
                {
                    if (md.IsDirectory) await share.Directories.DeleteAsync(path, token).ConfigureAwait(false);
                    else await share.Files.DeleteAsync(path, token).ConfigureAwait(false);
                }
                catch (OpenCifsStatusException e) when (IsNotFound(e.Status))
                {
                    // deleted concurrently
                }
                catch (OpenCifsStatusException e) when (e.Status == NtStatus.DirectoryNotEmpty)
                {
                    return e;
                }

                return null;
            }, token).ConfigureAwait(false);

            if (notEmpty != null) throw new IOException("The folder '" + key + "' is not empty.", notEmpty);
        }

        /// <inheritdoc />
        public override async Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default)
        {
            return await base.DeleteManyAsync(keys, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            string path = KeyToPath(key, out bool isFolderKey);

            return await ExecuteAsync(async share =>
            {
                if (!isFolderKey) return await share.Metadata.ExistsAsync(path, token).ConfigureAwait(false);

                try
                {
                    OpenCifsClientFileMetadata md = await share.Metadata.GetAttributesAsync(path, token).ConfigureAwait(false);
                    return md.IsDirectory;
                }
                catch (OpenCifsStatusException e) when (IsNotFound(e.Status))
                {
                    return false;
                }
            }, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            if (!String.IsNullOrEmpty(key)) key = key.Replace("/", "\\");
            return "\\\\" + _CifsSettings.Ip.ToString() + "\\" + _CifsSettings.Share + "\\" + key;
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            IAsyncEnumerator<BlobMetadata> enumerator = EnumerateAsync(filter).GetAsyncEnumerator();

            try
            {
                while (Task.Run(() => enumerator.MoveNextAsync().AsTask()).GetAwaiter().GetResult())
                {
                    yield return enumerator.Current;
                }
            }
            finally
            {
                Task.Run(() => enumerator.DisposeAsync().AsTask()).GetAwaiter().GetResult();
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            string prefix = (filter.Prefix ?? "").Replace("\\", "/");
            while (prefix.StartsWith("/")) prefix = prefix.Substring(1);
            filter.Prefix = prefix;

            // walk from the root, pruning folders that cannot contain matches, so keys carry the names as stored
            OpenCifsClientDirectoryEntry[] rootEntries = await ListDirectoryAsync("", token).ConfigureAwait(false);
            if (rootEntries == null) yield break;

            await foreach (BlobMetadata md in WalkAsync("", rootEntries, filter, token).ConfigureAwait(false))
            {
                yield return md;
            }
        }

        /// <summary>
        /// Delete all files and folders in the share.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Empty result.</returns>
        public override async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();
            List<BlobMetadata> files = new List<BlobMetadata>();
            List<BlobMetadata> folders = new List<BlobMetadata>();

            await CollectTreeAsync("", files, folders, token).ConfigureAwait(false);

            object syncLock = new object();

            using (SemaphoreSlim semaphore = new SemaphoreSlim(MaxConcurrency))
            {
                List<Task> tasks = new List<Task>();

                foreach (BlobMetadata md in files)
                {
                    await semaphore.WaitAsync(token).ConfigureAwait(false);

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await DeleteAsync(md.Key, token).ConfigureAwait(false);
                            lock (syncLock) er.Blobs.Add(md);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, token));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            foreach (BlobMetadata folder in folders.OrderByDescending(f => f.Key.Length))
            {
                token.ThrowIfCancellationRequested();
                await DeleteAsync(folder.Key, token).ConfigureAwait(false);
                er.Blobs.Add(folder);
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        private void ThrowIfDisposed()
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(CifsBlobClient));
        }

        private OpenCifsClientCredential BuildCredential()
        {
            string user = _CifsSettings.Username;
            string domain = _CifsSettings.Domain;

            while (user.StartsWith("\\")) user = user.Substring(1);
            int separator = user.IndexOf('\\');
            if (separator > 0)
            {
                if (String.IsNullOrEmpty(domain)) domain = user.Substring(0, separator);
                user = user.Substring(separator + 1);
            }

            return new OpenCifsClientCredential
            {
                UserName = user,
                UserDomain = domain,
                Password = _CifsSettings.Password
            };
        }

        private Connection NextConnection()
        {
            int index = (int)((uint)Interlocked.Increment(ref _NextConnection) % (uint)_Connections.Length);
            return _Connections[index];
        }

        private async Task CloseAllConnectionsAsync()
        {
            foreach (Connection connection in _Connections)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
        }

        private Task<T> ExecuteAsync<T>(Func<OpenCifsShareSession, Task<T>> operation, CancellationToken token, string notFoundKey = null)
        {
            return ExecuteAsync((connection, share) => operation(share), token, notFoundKey, true);
        }

        private async Task<T> ExecuteAsync<T>(
            Func<Connection, OpenCifsShareSession, Task<T>> operation,
            CancellationToken token,
            string notFoundKey,
            bool retryOnConnectionFailure)
        {
            int attempt = 0;

            while (true)
            {
                attempt++;
                token.ThrowIfCancellationRequested();
                ThrowIfDisposed();

                Connection connection = NextConnection();
                OpenCifsShareSession share = await connection.GetShareAsync(token).ConfigureAwait(false);

                try
                {
                    return await operation(connection, share).ConfigureAwait(false);
                }
                catch (OpenCifsStatusException e) when (notFoundKey != null && IsNotFound(e.Status))
                {
                    throw new KeyNotFoundException("The requested object '" + notFoundKey + "' was not found.", e);
                }
                catch (Exception e) when (IsConnectionFailure(e) && !token.IsCancellationRequested)
                {
                    Log("connection failure, resetting connection: " + e.Message);
                    await connection.InvalidateAsync(share).ConfigureAwait(false);

                    // after a server restart every pooled connection may be dead, so allow one attempt per connection
                    if (!retryOnConnectionFailure || attempt > _Connections.Length) throw;
                }
            }
        }

        private async Task WriteFileAsync(string path, Func<OpenCifsShareSession, Task> write, CancellationToken token, bool retryOnConnectionFailure = true)
        {
            await ExecuteAsync<object>(async (connection, share) =>
            {
                try
                {
                    await write(share).ConfigureAwait(false);
                }
                catch (OpenCifsStatusException e) when ((e.Status == NtStatus.ObjectPathNotFound || e.Status == NtStatus.ObjectNameNotFound) && path.Contains("\\"))
                {
                    // parent folder does not exist; create it and try again
                    string parent = path.Substring(0, path.LastIndexOf('\\'));
                    await share.Directories.CreateAsync(parent, true, token).ConfigureAwait(false);
                    await write(share).ConfigureAwait(false);
                }

                return null;
            }, token, null, retryOnConnectionFailure).ConfigureAwait(false);
        }

        private async Task CreateFolderAsync(string path, CancellationToken token)
        {
            await ExecuteAsync<object>(async share =>
            {
                await share.Directories.CreateAsync(path, true, token).ConfigureAwait(false);
                return null;
            }, token).ConfigureAwait(false);
        }

        private async Task<OpenCifsClientDirectoryEntry[]> ListDirectoryAsync(string path, CancellationToken token)
        {
            return await ExecuteAsync(async share =>
            {
                try
                {
                    return await share.Directories.EnumerateAsync(path, null, token).ConfigureAwait(false);
                }
                catch (OpenCifsStatusException e) when (IsNotFound(e.Status) || e.Status == NtStatus.NotADirectory)
                {
                    return null;
                }
            }, token).ConfigureAwait(false);
        }

        private async IAsyncEnumerable<BlobMetadata> WalkAsync(
            string directoryKey,
            OpenCifsClientDirectoryEntry[] entries,
            EnumerationFilter filter,
            [EnumeratorCancellation] CancellationToken token)
        {
            foreach (OpenCifsClientDirectoryEntry entry in entries.OrderBy(e => e.FileName, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (entry == null || entry.FileName == "." || entry.FileName == "..") continue;

                string key = directoryKey + entry.FileName;

                if (entry.IsDirectory)
                {
                    string folderKey = key + "/";
                    if (!CanContainMatches(folderKey, filter.Prefix)) continue;

                    OpenCifsClientDirectoryEntry[] children = await ListDirectoryAsync(KeyToDirectoryPath(folderKey), token).ConfigureAwait(false);
                    if (children == null) continue;

                    await foreach (BlobMetadata child in WalkAsync(folderKey, children, filter, token).ConfigureAwait(false))
                    {
                        yield return child;
                    }

                    // the folder follows its contents so that deleting in enumeration order empties it first;
                    // the folder named by the prefix itself is not returned
                    if (String.Equals(folderKey, filter.Prefix, StringComparison.OrdinalIgnoreCase)) continue;

                    BlobMetadata folder = BuildMetadata(folderKey, entry, true);
                    if (MatchesFilter(folder, filter, StringComparison.OrdinalIgnoreCase)) yield return folder;
                }
                else
                {
                    BlobMetadata md = BuildMetadata(key, entry, false);
                    if (MatchesFilter(md, filter, StringComparison.OrdinalIgnoreCase)) yield return md;
                }
            }
        }

        private async Task CollectTreeAsync(string directoryKey, List<BlobMetadata> files, List<BlobMetadata> folders, CancellationToken token)
        {
            OpenCifsClientDirectoryEntry[] entries = await ListDirectoryAsync(KeyToDirectoryPath(directoryKey), token).ConfigureAwait(false);
            if (entries == null) return;

            foreach (OpenCifsClientDirectoryEntry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry == null || entry.FileName == "." || entry.FileName == "..") continue;

                string key = directoryKey + entry.FileName;

                if (entry.IsDirectory)
                {
                    await CollectTreeAsync(key + "/", files, folders, token).ConfigureAwait(false);
                    folders.Add(BuildMetadata(key + "/", entry, true));
                }
                else
                {
                    files.Add(BuildMetadata(key, entry, false));
                }
            }
        }

        private static BlobMetadata BuildMetadata(string key, OpenCifsClientDirectoryEntry entry, bool isFolder)
        {
            return new BlobMetadata
            {
                Key = key,
                IsFolder = isFolder,
                ContentType = "application/octet-stream",
                ContentLength = isFolder ? 0 : ToLength(entry.EndOfFile),
                CreatedUtc = entry.CreationTimeUtc,
                LastAccessUtc = entry.LastAccessTimeUtc,
                LastUpdateUtc = entry.LastWriteTimeUtc
            };
        }

        private static bool CanContainMatches(string folderKey, string prefix)
        {
            if (String.IsNullOrEmpty(prefix)) return true;
            return folderKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || prefix.StartsWith(folderKey, StringComparison.OrdinalIgnoreCase);
        }

        private static long ToLength(ulong value)
        {
            return value > (ulong)Int64.MaxValue ? Int64.MaxValue : (long)value;
        }

        private static bool IsNotFound(NtStatus status)
        {
            return status == NtStatus.ObjectNameNotFound
                || status == NtStatus.ObjectPathNotFound
                || status == NtStatus.NoSuchFile
                || status == NtStatus.NotADirectory
                || status == NtStatus.DeletePending;
        }

        private static bool IsConnectionFailure(Exception e)
        {
            if (e is OpenCifsStatusException status)
            {
                return status.Status == NtStatus.NetworkNameDeleted
                    || status.Status == NtStatus.UserSessionDeleted;
            }

            // a session disposed by a concurrent reconnect reports the library's client as disposed
            if (e is ObjectDisposedException disposed) return disposed.ObjectName != nameof(CifsBlobClient);
            if (e.InnerException is ObjectDisposedException innerDisposed) return innerDisposed.ObjectName != nameof(CifsBlobClient);

            return e is IOException
                || e is SocketException
                || e is TimeoutException
                || e is OpenCifsClientTransportException
                || e is OpenCifsClientStateException
                || e is OpenCifsClientProtocolException
                || (e.InnerException != null && (e.InnerException is IOException || e.InnerException is SocketException));
        }

        /// <summary>
        /// Convert a BLOB key into a share-relative SMB path.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="isFolderKey">True if the key ends with a separator.</param>
        /// <returns>Path using '\' separators.</returns>
        internal static string KeyToPath(string key, out bool isFolderKey)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            string normalized = key.Replace("\\", "/");
            isFolderKey = normalized.EndsWith("/");

            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 1) throw new ArgumentException("The key must identify an object beneath the share root.", nameof(key));

            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                    throw new ArgumentException("Relative path segments '.' and '..' are not permitted in keys.", nameof(key));

                foreach (char c in segment)
                {
                    if (c < 0x20 || _ReservedCharacters.IndexOf(c) >= 0)
                        throw new ArgumentException("The key contains the character '" + (c < 0x20 ? "0x" + ((int)c).ToString("X2") : c.ToString()) + "', which SMB does not permit in names.", nameof(key));
                }
            }

            return String.Join("\\", segments);
        }

        private static string KeyToDirectoryPath(string directoryKey)
        {
            if (String.IsNullOrEmpty(directoryKey)) return "";
            return directoryKey.Replace("/", "\\").Trim('\\');
        }

        private static string PathToKey(string path, bool isFolder)
        {
            string key = path.Replace("\\", "/");
            return isFolder ? key + "/" : key;
        }

        #endregion

        #region Connection

        /// <summary>
        /// One pooled SMB connection and its share session.  OpenCIFS serializes the requests on a connection,
        /// so operations are spread across several connections to run in parallel.
        /// </summary>
        private sealed class Connection
        {
            private readonly CifsBlobClient _Parent;
            private readonly SemaphoreSlim _Lock = new SemaphoreSlim(1, 1);
            private OpenCifsShareSession _Share = null;

            internal OpenCifsClient Client { get; private set; } = null;

            internal Connection(CifsBlobClient parent)
            {
                _Parent = parent;
            }

            internal async Task<OpenCifsShareSession> GetShareAsync(CancellationToken token)
            {
                OpenCifsShareSession share = _Share;
                OpenCifsClient client = Client;
                if (share != null && client != null && client.IsConnected && client.IsAuthenticated) return share;

                await _Lock.WaitAsync(token).ConfigureAwait(false);

                try
                {
                    _Parent.ThrowIfDisposed();
                    if (_Share != null && Client != null && Client.IsConnected && Client.IsAuthenticated) return _Share;

                    await CloseCoreAsync().ConfigureAwait(false);

                    CifsSettings settings = _Parent._CifsSettings;
                    _Parent.Log("connecting to " + settings.Hostname + ":" + settings.Port + " share " + settings.Share);

                    OpenCifsClient newClient = new OpenCifsClientBuilder()
                        .WithServer(settings.Hostname, settings.Port)
                        .WithSigningRequired(settings.RequireSigning)
                        .WithPreferredEncryption(settings.PreferEncryption)
                        .WithConnectTimeoutMs(settings.ConnectTimeoutMs)
                        .Build();

                    try
                    {
                        await newClient.ConnectAsync(_Parent.BuildCredential(), token).ConfigureAwait(false);
                        OpenCifsShareSession newShare = await newClient.OpenShareAsync(settings.Share, token).ConfigureAwait(false);
                        Client = newClient;
                        _Share = newShare;
                        return newShare;
                    }
                    catch (OperationCanceledException e) when (!token.IsCancellationRequested)
                    {
                        try { await newClient.DisposeAsync().ConfigureAwait(false); } catch { }
                        throw new TimeoutException("Timed out connecting to " + settings.Hostname + ":" + settings.Port + ".", e);
                    }
                    catch
                    {
                        try { await newClient.DisposeAsync().ConfigureAwait(false); } catch { }
                        throw;
                    }
                }
                finally
                {
                    _Lock.Release();
                }
            }

            internal async Task InvalidateAsync(OpenCifsShareSession failed)
            {
                await _Lock.WaitAsync().ConfigureAwait(false);

                try
                {
                    if (ReferenceEquals(_Share, failed)) await CloseCoreAsync().ConfigureAwait(false);
                }
                finally
                {
                    _Lock.Release();
                }
            }

            internal async Task CloseAsync()
            {
                await _Lock.WaitAsync().ConfigureAwait(false);

                try
                {
                    await CloseCoreAsync().ConfigureAwait(false);
                }
                finally
                {
                    _Lock.Release();
                }
            }

            private async Task CloseCoreAsync()
            {
                OpenCifsShareSession share = _Share;
                OpenCifsClient client = Client;
                _Share = null;
                Client = null;

                if (share != null)
                {
                    try { await share.DisposeAsync().ConfigureAwait(false); } catch { }
                }

                if (client != null)
                {
                    try { if (client.IsConnected) await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                    try { await client.DisposeAsync().ConfigureAwait(false); } catch { }
                }
            }
        }

        #endregion
    }
}
