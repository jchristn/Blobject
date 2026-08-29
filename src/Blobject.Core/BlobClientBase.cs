namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An interface for interacting with different BLOB storage providers.
    /// </summary>
    public abstract class BlobClientBase
    {
#pragma warning disable CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable        #region Public-Members

        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        /// <summary>
        /// Buffer size to use when reading from a stream.  Default is 65536.
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                _StreamBufferSize = value;
            }
        }

        /// <summary>
        /// Maximum number of concurrent operations used by common bulk APIs.  Default is 4.
        /// </summary>
        public int MaxConcurrency
        {
            get
            {
                return _MaxConcurrency;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxConcurrency));
                _MaxConcurrency = value;
            }
        }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;
        private int _MaxConcurrency = 4;

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate connectivity to the repository.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if connectivity can be established.</returns>
        public abstract Task<bool> ValidateConnectivity(CancellationToken token = default);

        /// <summary>
        /// Gets the content of the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the BLOB to get.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A byte array containing the content of the BLOB.</returns>
        public abstract Task<byte[]> GetAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets the stream of the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the BLOB to get.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobData"/> object containing the stream of the BLOB.</returns>
        public abstract Task<BlobData> GetStreamAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets the metadata of the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the BLOB to get metadata for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobMetadata"/> object containing the metadata of the BLOB.</returns>
        public abstract Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Writes the specified data to the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="data">The data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public abstract Task WriteAsync(string key, string contentType, string data, CancellationToken token = default);

        /// <summary>
        /// Writes the specified data to the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="data">The data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public abstract Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default);

        /// <summary>
        /// Writes the data from the specified stream to the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="contentLength">The length of the content in the stream.</param>
        /// <param name="stream">The stream containing the data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public abstract Task WriteAsync(string key, string contentType, long contentLength, Stream stream,
            CancellationToken token = default);

        /// <summary>
        /// Writes many objects to the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="objects">The list of objects to write to the BLOB storage.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));

            await ForEachAsync(objects, MaxConcurrency, async obj =>
            {
                if (obj == null) return;

                if (obj.Data != null)
                {
                    await WriteAsync(obj.Key, obj.ContentType, obj.Data, token).ConfigureAwait(false);
                }
                else
                {
                    await WriteAsync(obj.Key, obj.ContentType, obj.ContentLength, obj.DataStream, token).ConfigureAwait(false);
                }
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an object from the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// For file storage platforms, when deleting a folder, use / at the end of the key.
        /// </summary>
        /// <param name="key">The key of the object to delete from the BLOB storage.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task DeleteAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Deletes multiple objects from the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// For file storage platforms, when deleting a folder, use / at the end of the key.
        /// Providers with a native bulk-delete API override this method to use it; otherwise deletions are fanned out over
        /// <see cref="DeleteAsync(string, CancellationToken)"/> using <see cref="MaxConcurrency"/>.
        /// Deleting a key that does not exist is treated as a successful deletion.
        /// </summary>
        /// <param name="keys">The keys of the objects to delete from the BLOB storage.  Null or empty keys are ignored.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A <see cref="DeleteManyResult"/> describing the outcome for each key.</returns>
        public virtual async Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));

            DeleteManyResult result = new DeleteManyResult();
            List<string> keyList = keys.Where(k => !String.IsNullOrEmpty(k)).Distinct().ToList();
            if (keyList.Count < 1) return result;

            object syncLock = new object();

            await ForEachAsync(keyList, MaxConcurrency, async key =>
            {
                DeleteResult dr = new DeleteResult { Key = key };

                try
                {
                    await DeleteAsync(key, token).ConfigureAwait(false);
                    dr.Success = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    dr.Success = false;
                    dr.Error = e.Message;
                }

                lock (syncLock)
                {
                    result.Results.Add(dr);
                }
            }, token).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Checks if an object with the specified key exists in the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the object to check.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation. The task result is true if the object exists; otherwise, false.</returns>
        public abstract Task<bool> ExistsAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Generates a URL to access the object with the specified key in the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the object to generate the URL for.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A string representing the URL to access the object.</returns>
        public abstract string GenerateUrl(string key, CancellationToken token = default);

        /// <summary>
        /// Enumerate all BLOBs within the repository.
        /// To enumerate only a specific prefix or contents of a specific folder, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="filter">Enumeration filter.</param>
        /// <returns>Enumerable of BlobMetadata.</returns>
        public abstract IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null);

        /// <summary>
        /// Enumerate all BLOBs within the repository asynchronously.
        /// To enumerate only a specific prefix or contents of a specific folder, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="filter">Enumeration filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Enumerable of BlobMetadata.</returns>
        public abstract IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            [EnumeratorCancellation] CancellationToken token = default);

        /// <summary>
        /// WARNING: This API deletes all objects in the BLOB storage asynchronously recursively.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public virtual async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();
            List<BlobMetadata> files = new List<BlobMetadata>();
            List<BlobMetadata> folders = new List<BlobMetadata>();

            await foreach (BlobMetadata md in EnumerateAsync(null, token).ConfigureAwait(false))
            {
                if (md == null) continue;
                if (md.IsFolder) folders.Add(md);
                else files.Add(md);
            }

            object syncLock = new object();

            await ForEachAsync(files, MaxConcurrency, async md =>
            {
                await DeleteAsync(md.Key, token).ConfigureAwait(false);
                lock (syncLock)
                {
                    er.Blobs.Add(md);
                }
            }, token).ConfigureAwait(false);

            foreach (BlobMetadata folder in folders.OrderByDescending(f => f.Key != null ? f.Key.Length : 0))
            {
                if (token.IsCancellationRequested) break;
                await DeleteAsync(folder.Key, token).ConfigureAwait(false);
                er.Blobs.Add(folder);
            }

            return er;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Clone an enumeration filter or create a default filter.
        /// </summary>
        /// <param name="filter">Input filter.</param>
        /// <returns>Cloned filter.</returns>
        protected static EnumerationFilter CloneFilter(EnumerationFilter filter)
        {
            if (filter == null) return new EnumerationFilter();
            return filter.Clone();
        }

        /// <summary>
        /// Determine if metadata matches a filter.
        /// </summary>
        /// <param name="metadata">Metadata.</param>
        /// <param name="filter">Filter.</param>
        /// <param name="comparison">String comparison.</param>
        /// <returns>True if the metadata matches the filter.</returns>
        protected static bool MatchesFilter(
            BlobMetadata metadata,
            EnumerationFilter filter,
            StringComparison comparison = StringComparison.Ordinal)
        {
            if (metadata == null) return false;
            if (filter == null) filter = new EnumerationFilter();

            if (metadata.ContentLength < filter.MinimumSize || metadata.ContentLength > filter.MaximumSize) return false;

            if (!String.IsNullOrEmpty(filter.Prefix))
            {
                if (String.IsNullOrEmpty(metadata.Key)) return false;
                if (!metadata.Key.StartsWith(filter.Prefix, comparison)) return false;
            }

            if (!String.IsNullOrEmpty(filter.Suffix))
            {
                if (String.IsNullOrEmpty(metadata.Key)) return false;
                if (!metadata.Key.EndsWith(filter.Suffix, comparison)) return false;
            }

            return true;
        }

        /// <summary>
        /// Copy exactly the specified number of bytes from one stream to another.
        /// </summary>
        /// <param name="source">Source stream.</param>
        /// <param name="destination">Destination stream.</param>
        /// <param name="contentLength">Content length.</param>
        /// <param name="token">Cancellation token.</param>
        protected async Task CopyStreamAsync(Stream source, Stream destination, long contentLength, CancellationToken token = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));

            byte[] buffer = new byte[StreamBufferSize];
            long bytesRemaining = contentLength;

            while (bytesRemaining > 0)
            {
                int toRead = bytesRemaining > buffer.Length ? buffer.Length : (int)bytesRemaining;
                int read = await source.ReadAsync(buffer, 0, toRead, token).ConfigureAwait(false);
                if (read < 1) break;

                await destination.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                bytesRemaining -= read;
            }
        }

        /// <summary>
        /// Read a stream fully into a byte array.
        /// </summary>
        /// <param name="source">Source stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Byte array.</returns>
        protected async Task<byte[]> ReadStreamFullyAsync(Stream source, CancellationToken token = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            using (MemoryStream ms = new MemoryStream())
            {
                await source.CopyToAsync(ms, StreamBufferSize, token).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        #endregion

        #region Private-Methods

        private static async Task ForEachAsync<T>(
            IEnumerable<T> source,
            int maxConcurrency,
            Func<T, Task> action,
            CancellationToken token)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));

            using (SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrency))
            {
                List<Task> tasks = new List<Task>();

                foreach (T item in source)
                {
                    token.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(token).ConfigureAwait(false);

                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await action(item).ConfigureAwait(false);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, token));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        #endregion

#pragma warning restore CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
    }
}
