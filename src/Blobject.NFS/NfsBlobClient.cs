namespace Blobject.NFS
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
    using OpenNFS.Client;

    /// <summary>
    /// BLOB client for NFSv3 exports, backed by OpenNFS.
    /// Keys map to paths within the export, using '/' as the separator.  Folders are created on demand when writing,
    /// a key ending in '/' refers to a folder, and enumeration returns files and folders, with each folder
    /// (IsFolder true, key ending in '/') returned after its contents.
    /// </summary>
    public class NfsBlobClient : BlobClientBase, IDisposable, IAsyncDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[NfsBlobClient] ";
        private NfsSettings _NfsSettings = null;
        private bool _Disposed = false;

        private readonly SemaphoreSlim _ConnectionLock = new SemaphoreSlim(1, 1);
        private OpenNfsClient _Client = null;
        private OpenNfsMountSession _Session = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsBlobClient"/> class.
        /// The connection to the server is established on first use.
        /// </summary>
        /// <param name="nfsSettings">Settings for <see cref="NfsBlobClient"/>.</param>
        public NfsBlobClient(NfsSettings nfsSettings)
        {
            if (nfsSettings == null) throw new ArgumentNullException(nameof(nfsSettings));
            if (String.IsNullOrEmpty(nfsSettings.Share)) throw new ArgumentException("A share is required.", nameof(nfsSettings));
            if (nfsSettings.Version != NfsVersionEnum.V3)
                throw new NotSupportedException("NFS version '" + nfsSettings.Version.ToString() + "' is not supported; only NFS version 3 is supported.");

            _NfsSettings = nfsSettings;
            RecordTelemetryPoolCapacity(1);
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
                Task.Run(() => CloseConnectionAsync()).GetAwaiter().GetResult();
                _ConnectionLock.Dispose();
                RecordTelemetryPoolCapacity(-1);
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
        /// Dispose asynchronously, unmounting and disconnecting from the server.
        /// </summary>
        /// <returns>Task.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;

            Log("disposing");
            await CloseConnectionAsync().ConfigureAwait(false);
            _ConnectionLock.Dispose();
            _Disposed = true;
            RecordTelemetryPoolCapacity(-1);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationValidateConnectivity, null, async () =>
            {
                try
                {
                    await ExecuteAsync(session => session.Metadata.GetAttributesAsync("/", token), token).ConfigureAwait(false);
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
            });
        }

        /// <summary>
        /// List shares (exports) available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of export paths.</returns>
        public Task<List<string>> ListShares(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationListContainers, null, async () =>
            {
                IReadOnlyList<OpenNfsExportV3Entry> exports = await ExecuteAsync(async session =>
                {
                    return await _Client.Exports.ListExportsV3Async(token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);

                List<string> ret = new List<string>();
                if (exports != null)
                {
                    foreach (OpenNfsExportV3Entry export in exports)
                    {
                        if (export != null && !String.IsNullOrEmpty(export.ExportPath)) ret.Add(export.ExportPath);
                    }
                }

                return ret;
            });
        }

        /// <inheritdoc />
        public override Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGet, key, async () =>
            {
                string path = KeyToPath(key, out _);

                byte[] data = await ExecuteAsync(async session =>
                {
                    try
                    {
                        return await session.Files.ReadAllBytesAsync(path, token).ConfigureAwait(false);
                    }
                    catch (OpenNfsV3StatusException e) when (e.Status == OpenNfsV3Status.IsDirectory || e.Status == OpenNfsV3Status.InvalidArgument)
                    {
                        // servers report reading a directory as either ISDIR or INVAL
                        OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(path, token).ConfigureAwait(false);
                        if (attributes.FileType == OpenNfsV3FileType.Directory) return Array.Empty<byte>();
                        throw;
                    }
                }, token, key).ConfigureAwait(false);

                if (data != null) SetTelemetryBytes(data.Length);
                return data;
            });
        }

        /// <inheritdoc />
        public override Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetStream, key, async () =>
            {
                string path = KeyToPath(key, out _);

                BlobData blob = await ExecuteAsync(async session =>
                {
                    OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(path, token).ConfigureAwait(false);
                    if (attributes.FileType == OpenNfsV3FileType.Directory) return new BlobData(0, new MemoryStream(Array.Empty<byte>()));

                    Stream stream = await session.Files.OpenReadAsync(path, token).ConfigureAwait(false);
                    return new BlobData(stream.Length, stream);
                }, token, key).ConfigureAwait(false);

                if (blob != null) SetTelemetryBytes(blob.ContentLength);
                return blob;
            });
        }

        /// <inheritdoc />
        /// <remarks>NFSv3 does not record a creation time, so <see cref="BlobMetadata.CreatedUtc"/> is not populated.</remarks>
        public override Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetMetadata, key, async () =>
            {
                string path = KeyToPath(key, out bool isFolderKey);

                OpenNfsV3Attributes attributes = await ExecuteAsync(
                    session => session.Metadata.GetAttributesAsync(path, token),
                    token,
                    key).ConfigureAwait(false);

                bool isFolder = attributes.FileType == OpenNfsV3FileType.Directory;
                if (isFolderKey && !isFolder) throw new KeyNotFoundException("The requested object was not found.");

                return BuildMetadata(isFolder ? path + "/" : path, attributes, isFolder);
            });
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (data == null) data = "";
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
            {
                if (data == null) data = Array.Empty<byte>();
                string path = KeyToPath(key, out bool isFolderKey);

                if (isFolderKey)
                {
                    await CreateFolderAsync(path, token).ConfigureAwait(false);
                    return;
                }

                await WriteFileAsync(path, session => session.Files.WriteAllBytesAsync(path, data, Stability, token), token).ConfigureAwait(false);
                SetTelemetryBytes(data.Length);
            });
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
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
                await WriteFileAsync(path, session => session.Files.WriteAsync(path, source, null, Stability, token), token, retryOnConnectionFailure: false).ConfigureAwait(false);
                SetTelemetryBytes(contentLength);
            });
        }

        /// <inheritdoc />
        public override async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            await base.WriteManyAsync(objects, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        /// <remarks>Deleting a key that does not exist succeeds.  Deleting a folder requires the folder to be empty.</remarks>
        public override Task DeleteAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationDelete, key, async () =>
            {
                string path = KeyToPath(key, out bool isFolderKey);

                // the not-empty failure is returned rather than thrown inside ExecuteAsync, where an IOException means a failed connection
                OpenNfsV3StatusException notEmpty = await ExecuteAsync<OpenNfsV3StatusException>(async session =>
                {
                    OpenNfsV3Attributes attributes;

                    try
                    {
                        attributes = await session.Metadata.GetAttributesAsync(path, token).ConfigureAwait(false);
                    }
                    catch (OpenNfsV3StatusException e) when (IsNotFound(e.Status))
                    {
                        return null;
                    }

                    bool isFolder = attributes.FileType == OpenNfsV3FileType.Directory;
                    if (isFolderKey && !isFolder) return null;

                    try
                    {
                        if (isFolder) await session.Directories.DeleteDirectoryAsync(path, token).ConfigureAwait(false);
                        else await session.Directories.DeleteFileAsync(path, token).ConfigureAwait(false);
                    }
                    catch (OpenNfsV3StatusException e) when (IsNotFound(e.Status))
                    {
                        // deleted concurrently
                    }
                    catch (OpenNfsV3StatusException e) when (e.Status == OpenNfsV3Status.NotEmpty || e.Status == OpenNfsV3Status.AlreadyExists)
                    {
                        return e;
                    }

                    return null;
                }, token).ConfigureAwait(false);

                if (notEmpty != null) throw new IOException("The folder '" + key + "' is not empty.", notEmpty);
            });
        }

        /// <inheritdoc />
        public override async Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default)
        {
            return await base.DeleteManyAsync(keys, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationExists, key, async () =>
            {
                string path = KeyToPath(key, out bool isFolderKey);

                return await ExecuteAsync(async session =>
                {
                    try
                    {
                        if (!isFolderKey) return await session.Metadata.ExistsAsync(path, token).ConfigureAwait(false);

                        OpenNfsV3Attributes attributes = await session.Metadata.GetAttributesAsync(path, token).ConfigureAwait(false);
                        return attributes.FileType == OpenNfsV3FileType.Directory;
                    }
                    catch (OpenNfsV3StatusException e) when (IsNotFound(e.Status))
                    {
                        // some servers (e.g. unfs3) report a lookup through a file as STALE rather than NOTDIR
                        return false;
                    }
                }, token).ConfigureAwait(false);
            });
        }

        /// <inheritdoc />
        /// <remarks>Returns an NFS URL as described in RFC 2224, e.g. nfs://server/export/key.</remarks>
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            string host = _NfsSettings.Hostname;
            if (_NfsSettings.Port != 2049) host += ":" + _NfsSettings.Port;

            string share = _NfsSettings.Share.Trim('/');
            string path = (key ?? "").Replace("\\", "/").TrimStart('/');

            string url = "nfs://" + host + "/";
            if (!String.IsNullOrEmpty(share)) url += share + "/";
            return url + path;
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            return InstrumentEnumerate(() => EnumerateInternal(filter));
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            CancellationToken token = default)
        {
            return InstrumentEnumerateAsync(t => EnumerateInternalAsync(filter, t), token);
        }

        /// <summary>
        /// Delete all files and folders in the export.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Empty result.</returns>
        public override Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationEmpty, null, async () =>
            {
                EmptyResult er = new EmptyResult();
                List<BlobMetadata> files = new List<BlobMetadata>();
                List<BlobMetadata> folders = new List<BlobMetadata>();

                await CollectTreeAsync("", files, folders, token).ConfigureAwait(false);

                object syncLock = new object();

                await ForEachConcurrentAsync(files, async md =>
                {
                    await DeleteAsync(md.Key, token).ConfigureAwait(false);
                    lock (syncLock) er.Blobs.Add(md);
                }, BlobjectTelemetryNames.OperationEmpty, token).ConfigureAwait(false);

                foreach (BlobMetadata folder in folders.OrderByDescending(f => f.Key.Length))
                {
                    token.ThrowIfCancellationRequested();
                    await DeleteAsync(folder.Key, token).ConfigureAwait(false);
                    er.Blobs.Add(folder);
                    RecordTelemetryItem(true);
                }

                return er;
            });
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override string TelemetryProvider
        {
            get
            {
                return BlobjectTelemetryNames.ProviderNfs;
            }
        }

        /// <inheritdoc />
        protected override string TelemetryContainer
        {
            get
            {
                NfsSettings settings = _NfsSettings;
                return settings != null ? settings.Share : null;
            }
        }

        /// <inheritdoc />
        protected override string TelemetryServerAddress
        {
            get
            {
                NfsSettings settings = _NfsSettings;
                return settings != null ? settings.Hostname : null;
            }
        }

        #endregion

        #region Private-Methods

        private IEnumerable<BlobMetadata> EnumerateInternal(EnumerationFilter filter)
        {
            IAsyncEnumerator<BlobMetadata> enumerator = EnumerateInternalAsync(filter, CancellationToken.None).GetAsyncEnumerator();

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

        private async IAsyncEnumerable<BlobMetadata> EnumerateInternalAsync(
            EnumerationFilter filter,
            [EnumeratorCancellation] CancellationToken token)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            string prefix = (filter.Prefix ?? "").Replace("\\", "/");
            while (prefix.StartsWith("/")) prefix = prefix.Substring(1);
            filter.Prefix = prefix;

            // walk from the root, pruning folders that cannot contain matches, so keys carry the names as stored
            IReadOnlyList<OpenNfsV3DirectoryPlusEntry> rootEntries = await ListDirectoryAsync("/", token).ConfigureAwait(false);
            if (rootEntries == null) yield break;

            await foreach (BlobMetadata md in WalkAsync("", rootEntries, filter, token).ConfigureAwait(false))
            {
                yield return md;
            }
        }

        private OpenNfsWriteStability Stability
        {
            get
            {
                switch (_NfsSettings.WriteStability)
                {
                    case NfsWriteStabilityEnum.DataSync:
                        return OpenNfsWriteStability.DataSync;
                    case NfsWriteStabilityEnum.FileSync:
                        return OpenNfsWriteStability.FileSync;
                    default:
                        return OpenNfsWriteStability.Unstable;
                }
            }
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        private void ThrowIfDisposed()
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(NfsBlobClient));
        }

        private async Task<OpenNfsMountSession> GetSessionAsync(CancellationToken token)
        {
            ThrowIfDisposed();

            OpenNfsMountSession session = _Session;
            OpenNfsClient client = _Client;
            if (session != null && client != null && client.State == OpenNfsClientState.Open) return session;

            await _ConnectionLock.WaitAsync(token).ConfigureAwait(false);

            try
            {
                ThrowIfDisposed();
                if (_Session != null && _Client != null && _Client.State == OpenNfsClientState.Open) return _Session;

                // a connection that dropped (e.g. server restart) is replaced here rather than failing an operation
                if (_Session != null || _Client != null) RecordTelemetryConnectionReset(null);
                await CloseConnectionCoreAsync().ConfigureAwait(false);

                Log("connecting to " + _NfsSettings.Hostname + ":" + _NfsSettings.Port + " export " + _NfsSettings.Share);

                OpenNfsClientBuilder builder = new OpenNfsClientBuilder()
                    .WithServer(_NfsSettings.Hostname, _NfsSettings.Port)
                    .WithAuthSysCredentials(new OpenNfsAuthSysCredentials(
                        _NfsSettings.MachineName,
                        (uint)_NfsSettings.UserId,
                        (uint)_NfsSettings.GroupId))
                    .WithConnectionTimeout(TimeSpan.FromMilliseconds(_NfsSettings.ConnectTimeoutMs))
                    .WithResponseTimeout(TimeSpan.FromMilliseconds(_NfsSettings.ResponseTimeoutMs));

                if (_NfsSettings.MountPort > 0)
                {
                    builder = builder.WithMountPort(_NfsSettings.MountPort);
                }
                else
                {
                    builder = builder
                        .WithPortmapperDiscovery(true)
                        .WithPortmapperPort(_NfsSettings.PortmapperPort);
                }

                OpenNfsClient newClient = builder.Build();

                try
                {
                    OpenNfsMountSession newSession = await InstrumentConnectAsync(async () =>
                    {
                        await newClient.ConnectAsync(token).ConfigureAwait(false);
                        return await newClient.MountAsync(_NfsSettings.Share, token).ConfigureAwait(false);
                    }).ConfigureAwait(false);
                    _Client = newClient;
                    _Session = newSession;
                    return newSession;
                }
                catch (OperationCanceledException e) when (!token.IsCancellationRequested)
                {
                    try { await newClient.DisposeAsync().ConfigureAwait(false); } catch { }
                    throw new TimeoutException("Timed out connecting to " + _NfsSettings.Hostname + ":" + _NfsSettings.Port + ".", e);
                }
                catch
                {
                    try { await newClient.DisposeAsync().ConfigureAwait(false); } catch { }
                    throw;
                }
            }
            finally
            {
                _ConnectionLock.Release();
            }
        }

        private async Task CloseConnectionAsync()
        {
            await _ConnectionLock.WaitAsync().ConfigureAwait(false);

            try
            {
                await CloseConnectionCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _ConnectionLock.Release();
            }
        }

        private async Task CloseConnectionCoreAsync()
        {
            OpenNfsMountSession session = _Session;
            OpenNfsClient client = _Client;
            _Session = null;
            _Client = null;

            if (session != null)
            {
                try { await session.DisposeAsync().ConfigureAwait(false); } catch { }
            }

            if (client != null)
            {
                try { if (client.State == OpenNfsClientState.Open) await client.DisconnectAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                try { await client.DisposeAsync().ConfigureAwait(false); } catch { }
                RecordTelemetryConnectionClosed();
            }
        }

        private async Task InvalidateConnectionAsync(OpenNfsMountSession failed)
        {
            await _ConnectionLock.WaitAsync().ConfigureAwait(false);

            try
            {
                if (ReferenceEquals(_Session, failed)) await CloseConnectionCoreAsync().ConfigureAwait(false);
            }
            finally
            {
                _ConnectionLock.Release();
            }
        }

        private Task<T> ExecuteAsync<T>(Func<OpenNfsMountSession, Task<T>> operation, CancellationToken token, string notFoundKey = null)
        {
            return ExecuteAsync(operation, token, notFoundKey, true);
        }

        private async Task<T> ExecuteAsync<T>(Func<OpenNfsMountSession, Task<T>> operation, CancellationToken token, string notFoundKey, bool retryOnConnectionFailure)
        {
            int attempt = 0;

            while (true)
            {
                attempt++;
                token.ThrowIfCancellationRequested();
                ThrowIfDisposed();
                OpenNfsMountSession session = await GetSessionAsync(token).ConfigureAwait(false);

                try
                {
                    return await operation(session).ConfigureAwait(false);
                }
                catch (OpenNfsV3StatusException e) when (notFoundKey != null && IsNotFound(e.Status))
                {
                    throw new KeyNotFoundException("The requested object '" + notFoundKey + "' was not found.", e);
                }
                catch (Exception e) when (IsConnectionFailure(e) && !token.IsCancellationRequested)
                {
                    Log("connection failure, resetting connection: " + e.Message);
                    RecordTelemetryConnectionReset(e);
                    await InvalidateConnectionAsync(session).ConfigureAwait(false);
                    if (!retryOnConnectionFailure || attempt > 1) throw;
                    RecordTelemetryRetry();
                }
            }
        }

        private async Task WriteFileAsync(string path, Func<OpenNfsMountSession, Task> write, CancellationToken token, bool retryOnConnectionFailure = true)
        {
            await ExecuteAsync<object>(async session =>
            {
                try
                {
                    await write(session).ConfigureAwait(false);
                }
                catch (OpenNfsV3StatusException e) when (e.Status == OpenNfsV3Status.NoEntry && path.Contains("/"))
                {
                    // parent folder does not exist; create it and try again
                    string parent = path.Substring(0, path.LastIndexOf('/'));
                    await session.Directories.CreateDirectoryAsync(parent, true, token).ConfigureAwait(false);
                    await write(session).ConfigureAwait(false);
                }

                return null;
            }, token, null, retryOnConnectionFailure).ConfigureAwait(false);
        }

        private async Task CreateFolderAsync(string path, CancellationToken token)
        {
            await ExecuteAsync<object>(async session =>
            {
                await session.Directories.CreateDirectoryAsync(path, true, token).ConfigureAwait(false);
                return null;
            }, token).ConfigureAwait(false);
        }

        private async Task<IReadOnlyList<OpenNfsV3DirectoryPlusEntry>> ListDirectoryAsync(string path, CancellationToken token)
        {
            return await ExecuteAsync(async session =>
            {
                try
                {
                    return await session.Directories.ListWithAttributesAsync(path, token).ConfigureAwait(false);
                }
                catch (OpenNfsV3StatusException e) when (IsNotFound(e.Status))
                {
                    return null;
                }
            }, token).ConfigureAwait(false);
        }

        private async IAsyncEnumerable<BlobMetadata> WalkAsync(
            string directoryKey,
            IReadOnlyList<OpenNfsV3DirectoryPlusEntry> entries,
            EnumerationFilter filter,
            [EnumeratorCancellation] CancellationToken token)
        {
            foreach (OpenNfsV3DirectoryPlusEntry entry in entries.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (entry == null || entry.Name == "." || entry.Name == ".." || entry.Attributes == null) continue;

                string key = directoryKey + entry.Name;

                if (entry.Attributes.FileType == OpenNfsV3FileType.Directory)
                {
                    string folderKey = key + "/";
                    if (!CanContainMatches(folderKey, filter.Prefix)) continue;

                    IReadOnlyList<OpenNfsV3DirectoryPlusEntry> children = await ListDirectoryAsync(KeyToDirectoryPath(folderKey), token).ConfigureAwait(false);
                    if (children == null) continue;

                    await foreach (BlobMetadata child in WalkAsync(folderKey, children, filter, token).ConfigureAwait(false))
                    {
                        yield return child;
                    }

                    // the folder follows its contents so that deleting in enumeration order empties it first;
                    // the folder named by the prefix itself is not returned
                    if (String.Equals(folderKey, filter.Prefix, StringComparison.Ordinal)) continue;

                    BlobMetadata folder = BuildMetadata(folderKey, entry.Attributes, true);
                    if (MatchesFilter(folder, filter, StringComparison.Ordinal)) yield return folder;
                }
                else
                {
                    BlobMetadata md = BuildMetadata(key, entry.Attributes, false);
                    if (MatchesFilter(md, filter, StringComparison.Ordinal)) yield return md;
                }
            }
        }

        private async Task CollectTreeAsync(string directoryKey, List<BlobMetadata> files, List<BlobMetadata> folders, CancellationToken token)
        {
            IReadOnlyList<OpenNfsV3DirectoryPlusEntry> entries = await ListDirectoryAsync(KeyToDirectoryPath(directoryKey), token).ConfigureAwait(false);
            if (entries == null) return;

            foreach (OpenNfsV3DirectoryPlusEntry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                if (entry == null || entry.Name == "." || entry.Name == ".." || entry.Attributes == null) continue;

                string key = directoryKey + entry.Name;

                if (entry.Attributes.FileType == OpenNfsV3FileType.Directory)
                {
                    await CollectTreeAsync(key + "/", files, folders, token).ConfigureAwait(false);
                    folders.Add(BuildMetadata(key + "/", entry.Attributes, true));
                }
                else
                {
                    files.Add(BuildMetadata(key, entry.Attributes, false));
                }
            }
        }

        private static BlobMetadata BuildMetadata(string key, OpenNfsV3Attributes attributes, bool isFolder)
        {
            return new BlobMetadata
            {
                Key = key,
                IsFolder = isFolder,
                ContentType = "application/octet-stream",
                ContentLength = isFolder ? 0 : (attributes.SizeBytes > (ulong)Int64.MaxValue ? Int64.MaxValue : (long)attributes.SizeBytes),
                CreatedUtc = null,
                LastAccessUtc = attributes.AccessTime.ToDateTimeUtc(),
                LastUpdateUtc = attributes.ModifyTime.ToDateTimeUtc()
            };
        }

        private static bool CanContainMatches(string folderKey, string prefix)
        {
            if (String.IsNullOrEmpty(prefix)) return true;
            return folderKey.StartsWith(prefix, StringComparison.Ordinal)
                || prefix.StartsWith(folderKey, StringComparison.Ordinal);
        }

        private static bool IsNotFound(OpenNfsV3Status status)
        {
            return status == OpenNfsV3Status.NoEntry
                || status == OpenNfsV3Status.NotDirectory
                || status == OpenNfsV3Status.Stale;
        }

        private static bool IsConnectionFailure(Exception e)
        {
            // a session disposed by a concurrent reconnect reports the library's client as disposed
            if (e is ObjectDisposedException disposed) return disposed.ObjectName != nameof(NfsBlobClient);
            if (e.InnerException is ObjectDisposedException innerDisposed) return innerDisposed.ObjectName != nameof(NfsBlobClient);

            return e is IOException
                || e is SocketException
                || e is TimeoutException
                || e is OpenNfsClientIoException
                || e is OpenNfsClientStateException
                || (e.InnerException != null && (e.InnerException is IOException || e.InnerException is SocketException));
        }

        /// <summary>
        /// Convert a BLOB key into an export-relative NFS path.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="isFolderKey">True if the key ends with a separator.</param>
        /// <returns>Path using '/' separators.</returns>
        internal static string KeyToPath(string key, out bool isFolderKey)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            string normalized = key.Replace("\\", "/");
            isFolderKey = normalized.EndsWith("/");

            string[] segments = normalized.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 1) throw new ArgumentException("The key must identify an object beneath the export root.", nameof(key));

            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                    throw new ArgumentException("Relative path segments '.' and '..' are not permitted in keys.", nameof(key));
            }

            return String.Join("/", segments);
        }

        private static string KeyToDirectoryPath(string directoryKey)
        {
            if (String.IsNullOrEmpty(directoryKey)) return "/";
            string path = directoryKey.Trim('/');
            return String.IsNullOrEmpty(path) ? "/" : path;
        }

        #endregion
    }
}
