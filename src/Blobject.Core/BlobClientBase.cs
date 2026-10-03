namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
        public virtual Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWriteMany, null, async () =>
            {
                if (objects == null) throw new ArgumentNullException(nameof(objects));

                await ForEachConcurrentAsync(objects, async obj =>
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
                }, BlobjectTelemetryNames.OperationWriteMany, token).ConfigureAwait(false);
            });
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
        public virtual Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationDeleteMany, null, async () =>
            {
                if (keys == null) throw new ArgumentNullException(nameof(keys));

                DeleteManyResult result = new DeleteManyResult();
                List<string> keyList = keys.Where(k => !String.IsNullOrEmpty(k)).Distinct().ToList();
                if (keyList.Count < 1) return result;

                object syncLock = new object();

                await ForEachConcurrentAsync(keyList, async key =>
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
                }, BlobjectTelemetryNames.OperationDeleteMany, token).ConfigureAwait(false);

                SetTelemetryItems(result.Results.Count(r => r.Success), result.Results.Count(r => !r.Success));
                return result;
            });
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
        public virtual Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationEmpty, null, async () =>
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

                await ForEachConcurrentAsync(files, async md =>
                {
                    await DeleteAsync(md.Key, token).ConfigureAwait(false);
                    lock (syncLock)
                    {
                        er.Blobs.Add(md);
                    }
                }, BlobjectTelemetryNames.OperationEmpty, token).ConfigureAwait(false);

                foreach (BlobMetadata folder in folders.OrderByDescending(f => f.Key != null ? f.Key.Length : 0))
                {
                    if (token.IsCancellationRequested) break;
                    await DeleteAsync(folder.Key, token).ConfigureAwait(false);
                    er.Blobs.Add(folder);
                    RecordTelemetryItem(true);
                }

                return er;
            });
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

        #region Telemetry

        /// <summary>
        /// Provider name reported on Blobject metrics and spans (blobject.provider).  Must be a small, fixed value such as
        /// one of the BlobjectTelemetryNames.Provider* constants.  Default is "custom".
        /// </summary>
        protected internal virtual string TelemetryProvider
        {
            get
            {
                return BlobjectTelemetryNames.ProviderCustom;
            }
        }

        /// <summary>
        /// Bucket, container, share, export, or directory reported on spans (blobject.container).  Never used on metrics.
        /// Default is null (not reported).
        /// </summary>
        protected virtual string TelemetryContainer
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Server host name or endpoint reported on spans (server.address).  Never used on metrics.  Default is null (not reported).
        /// </summary>
        protected virtual string TelemetryServerAddress
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Determine whether an exception means the requested object does not exist, which telemetry reports with the
        /// not_found outcome.  The default recognizes <see cref="KeyNotFoundException"/>, <see cref="FileNotFoundException"/>,
        /// and <see cref="DirectoryNotFoundException"/>; providers override it to add their SDK's not-found errors.
        /// </summary>
        /// <param name="e">Exception, never null.</param>
        /// <returns>True if the exception means the object does not exist.</returns>
        protected virtual bool IsTelemetryNotFound(Exception e)
        {
            return e is KeyNotFoundException || e is FileNotFoundException || e is DirectoryNotFoundException;
        }

        /// <summary>
        /// Run a storage operation inside a Blobject span and record its duration, outcome, and errors.
        /// When the same client is already inside the same operation (one overload delegating to another),
        /// the inner call is not recorded again.  Telemetry failures never affect the operation.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="operation">Operation name, one of the BlobjectTelemetryNames.Operation* constants.</param>
        /// <param name="key">Object key, recorded on the span only when <see cref="BlobjectTelemetry.RecordKeys"/> is true.  May be null.</param>
        /// <param name="body">Operation.</param>
        /// <returns>The result of the operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when body is null.</exception>
        protected Task<T> InstrumentAsync<T>(string operation, string key, Func<Task<T>> body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            TelemetryOperationScope scope = BeginTelemetry(operation, key);
            if (scope == null) return body();
            return RunInstrumentedAsync(scope, body);
        }

        /// <summary>
        /// Run a storage operation inside a Blobject span and record its duration, outcome, and errors.
        /// When the same client is already inside the same operation (one overload delegating to another),
        /// the inner call is not recorded again.  Telemetry failures never affect the operation.
        /// </summary>
        /// <param name="operation">Operation name, one of the BlobjectTelemetryNames.Operation* constants.</param>
        /// <param name="key">Object key, recorded on the span only when <see cref="BlobjectTelemetry.RecordKeys"/> is true.  May be null.</param>
        /// <param name="body">Operation.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when body is null.</exception>
        protected Task InstrumentAsync(string operation, string key, Func<Task> body)
        {
            if (body == null) throw new ArgumentNullException(nameof(body));

            TelemetryOperationScope scope = BeginTelemetry(operation, key);
            if (scope == null) return body();
            return RunInstrumentedAsync(scope, body);
        }

        /// <summary>
        /// Wrap a synchronous enumeration in an enumerate span that ends when enumeration completes, fails, or is abandoned,
        /// and count the objects returned.
        /// </summary>
        /// <param name="source">Factory for the underlying enumeration, invoked when enumeration starts.</param>
        /// <returns>Instrumented enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        protected IEnumerable<BlobMetadata> InstrumentEnumerate(Func<IEnumerable<BlobMetadata>> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return InstrumentedEnumerate(source);
        }

        /// <summary>
        /// Wrap an asynchronous enumeration in an enumerate span that ends when enumeration completes, fails, or is abandoned,
        /// and count the objects returned.  The cancellation token passed to the factory combines the supplied token with
        /// any token supplied through WithCancellation.
        /// </summary>
        /// <param name="source">Factory for the underlying enumeration, invoked when enumeration starts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Instrumented enumeration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
        protected IAsyncEnumerable<BlobMetadata> InstrumentEnumerateAsync(
            Func<CancellationToken, IAsyncEnumerable<BlobMetadata>> source,
            CancellationToken token = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return InstrumentedEnumerateAsync(source, token);
        }

        /// <summary>
        /// Report the object bytes read or written by the current operation of this client.  Has no effect outside an
        /// instrumented operation.
        /// </summary>
        /// <param name="bytes">Bytes; negative values are ignored.</param>
        protected void SetTelemetryBytes(long bytes)
        {
            TelemetryOperationScope scope = CurrentTelemetryScope();
            if (scope != null) scope.SetBytes(bytes);
        }

        /// <summary>
        /// Report the per-item results of the current bulk operation of this client, replacing any counted so far.
        /// Has no effect outside an instrumented operation.
        /// </summary>
        /// <param name="succeeded">Items that succeeded.</param>
        /// <param name="failed">Items that failed.</param>
        protected void SetTelemetryItems(long succeeded, long failed)
        {
            TelemetryOperationScope scope = CurrentTelemetryScope();
            if (scope != null) scope.SetItemResults(succeeded, failed);
        }

        /// <summary>
        /// Count one item of the current bulk operation of this client.  Has no effect outside an instrumented operation.
        /// </summary>
        /// <param name="success">True if the item succeeded.</param>
        protected void RecordTelemetryItem(bool success)
        {
            TelemetryOperationScope scope = CurrentTelemetryScope();
            if (scope != null) scope.AddItemResult(success);
        }

        /// <summary>
        /// Establish a connection inside a connect span, recording its duration and outcome, and count it as open on success.
        /// Call <see cref="RecordTelemetryConnectionClosed"/> when the connection is closed.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="connect">Connect operation.</param>
        /// <returns>The result of the connect operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when connect is null.</exception>
        protected async Task<T> InstrumentConnectAsync<T>(Func<Task<T>> connect)
        {
            if (connect == null) throw new ArgumentNullException(nameof(connect));

            string provider = SafeTelemetryProvider();
            Activity activity = null;
            Activity previous = Activity.Current;
            long start = Stopwatch.GetTimestamp();
            bool enabled = BlobjectTelemetry.Enabled;

            if (enabled)
            {
                try
                {
                    activity = BlobjectInstrumentation.Source.StartActivity(provider + " " + BlobjectTelemetryNames.OperationConnect, ActivityKind.Client);
                    if (activity != null && activity.IsAllDataRequested)
                    {
                        activity.SetTag(BlobjectTelemetryNames.AttributeProvider, provider);
                        activity.SetTag(BlobjectTelemetryNames.AttributeOperation, BlobjectTelemetryNames.OperationConnect);
                        string container = SafeTelemetryValue(false);
                        string server = SafeTelemetryValue(true);
                        if (!String.IsNullOrEmpty(container)) activity.SetTag(BlobjectTelemetryNames.AttributeContainer, container);
                        if (!String.IsNullOrEmpty(server)) activity.SetTag(BlobjectTelemetryNames.AttributeServerAddress, server);
                    }
                }
                catch
                {
                    activity = null;
                }
            }

            Exception failure = null;

            try
            {
                return await connect().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                failure = e;
                throw;
            }
            finally
            {
                if (enabled)
                {
                    try
                    {
                        string outcome = BlobjectInstrumentation.ClassifyOutcome(failure, false);
                        bool failed = failure != null && !(failure is OperationCanceledException);

                        if (BlobjectInstrumentation.ConnectionDuration.Enabled)
                        {
                            TagList tags = new TagList();
                            tags.Add(BlobjectTelemetryNames.AttributeProvider, provider);
                            tags.Add(BlobjectTelemetryNames.AttributeOutcome, outcome);
                            if (failed) tags.Add(BlobjectTelemetryNames.AttributeErrorType, BlobjectInstrumentation.ErrorType(failure));
                            BlobjectInstrumentation.ConnectionDuration.Record(BlobjectInstrumentation.ElapsedSeconds(start), tags);
                        }

                        if (failure == null) BlobjectInstrumentation.AddProviderCount(BlobjectInstrumentation.ConnectionOpen, provider, 1);

                        if (activity != null)
                        {
                            if (activity.IsAllDataRequested)
                            {
                                activity.SetTag(BlobjectTelemetryNames.AttributeOutcome, outcome);
                                if (failed)
                                {
                                    activity.SetTag(BlobjectTelemetryNames.AttributeErrorType, BlobjectInstrumentation.ErrorType(failure));
                                    activity.SetStatus(ActivityStatusCode.Error, failure.Message);
                                    BlobjectInstrumentation.RecordException(activity, failure);
                                }
                                else if (failure == null)
                                {
                                    activity.SetStatus(ActivityStatusCode.Ok);
                                }
                            }

                            activity.Dispose();
                        }

                        Activity.Current = previous;
                    }
                    catch
                    {
                        // best-effort
                    }
                }
            }
        }

        /// <summary>
        /// Count a connection established through <see cref="InstrumentConnectAsync{T}"/> as closed.
        /// </summary>
        protected void RecordTelemetryConnectionClosed()
        {
            BlobjectInstrumentation.AddProviderCount(BlobjectInstrumentation.ConnectionOpen, SafeTelemetryProvider(), -1);
        }

        /// <summary>
        /// Record that a connection was discarded after a transport failure, and add an event to the current span.
        /// </summary>
        /// <param name="e">Failure that caused the reset.  Null when a pooled connection was found disconnected before use,
        /// which is reported with error.type connection_lost.</param>
        protected void RecordTelemetryConnectionReset(Exception e)
        {
            BlobjectInstrumentation.RecordConnectionReset(SafeTelemetryProvider(), e);
        }

        /// <summary>
        /// Record that the current operation is being retried, and add an event to the current span.
        /// </summary>
        protected void RecordTelemetryRetry()
        {
            TelemetryOperationScope scope = CurrentTelemetryScope();
            BlobjectInstrumentation.RecordRetry(SafeTelemetryProvider(), scope != null ? scope.Operation : null);
        }

        /// <summary>
        /// Adjust the reported connection pool capacity (blobject.connection.pool.capacity).  Add the pool size when a client
        /// is created and subtract it when the client is disposed.
        /// </summary>
        /// <param name="delta">Change in capacity.</param>
        protected void RecordTelemetryPoolCapacity(int delta)
        {
            BlobjectInstrumentation.AddProviderCount(BlobjectInstrumentation.ConnectionPoolCapacity, SafeTelemetryProvider(), delta);
        }

        /// <summary>
        /// Run an action for each item with at most <see cref="MaxConcurrency"/> actions in flight, recording the time each
        /// item waits for a slot (blobject.bulk.queue.duration), slots in use and capacity, and per-item outcomes for the
        /// current bulk operation.  The first failure is rethrown after all started actions finish.
        /// </summary>
        /// <typeparam name="T">Item type.</typeparam>
        /// <param name="source">Items.</param>
        /// <param name="action">Action to run for each item.</param>
        /// <param name="operation">Bulk operation name used on slot metrics, one of the BlobjectTelemetryNames.Operation* constants.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when source or action is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
        protected async Task ForEachConcurrentAsync<T>(
            IEnumerable<T> source,
            Func<T, Task> action,
            string operation,
            CancellationToken token = default)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (action == null) throw new ArgumentNullException(nameof(action));

            int maxConcurrency = MaxConcurrency;
            string provider = SafeTelemetryProvider();
            if (String.IsNullOrEmpty(operation)) operation = BlobjectTelemetryNames.OperationWriteMany;
            bool observed = BlobjectInstrumentation.IsObserved;
            TelemetryOperationScope scope = CurrentTelemetryScope();
            if (scope != null) scope.SetMaxConcurrency(maxConcurrency);

            if (observed) BlobjectInstrumentation.AddOperationCount(BlobjectInstrumentation.BulkSlotsCapacity, provider, operation, maxConcurrency);

            try
            {
                using (SemaphoreSlim semaphore = new SemaphoreSlim(maxConcurrency))
                {
                    List<Task> tasks = new List<Task>();

                    foreach (T item in source)
                    {
                        token.ThrowIfCancellationRequested();

                        long waitStart = Stopwatch.GetTimestamp();
                        await semaphore.WaitAsync(token).ConfigureAwait(false);
                        if (observed) BlobjectInstrumentation.RecordQueueWait(provider, operation, BlobjectInstrumentation.ElapsedSeconds(waitStart));

                        tasks.Add(Task.Run(async () =>
                        {
                            if (observed) BlobjectInstrumentation.AddOperationCount(BlobjectInstrumentation.BulkSlotsInUse, provider, operation, 1);

                            try
                            {
                                await action(item).ConfigureAwait(false);
                                if (scope != null) scope.AddItemResult(true);
                            }
                            catch
                            {
                                if (scope != null) scope.AddItemResult(false);
                                throw;
                            }
                            finally
                            {
                                if (observed) BlobjectInstrumentation.AddOperationCount(BlobjectInstrumentation.BulkSlotsInUse, provider, operation, -1);
                                semaphore.Release();
                            }
                        }, token));
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            finally
            {
                if (observed) BlobjectInstrumentation.AddOperationCount(BlobjectInstrumentation.BulkSlotsCapacity, provider, operation, -maxConcurrency);
            }
        }

        #endregion

        #region Private-Methods

        internal string SafeTelemetryProvider()
        {
            try
            {
                string provider = TelemetryProvider;
                return String.IsNullOrEmpty(provider) ? BlobjectTelemetryNames.ProviderCustom : provider;
            }
            catch
            {
                return BlobjectTelemetryNames.ProviderCustom;
            }
        }

        private string SafeTelemetryValue(bool serverAddress)
        {
            try
            {
                return serverAddress ? TelemetryServerAddress : TelemetryContainer;
            }
            catch
            {
                return null;
            }
        }

        private bool SafeIsNotFound(Exception e)
        {
            try
            {
                return e != null && IsTelemetryNotFound(e);
            }
            catch
            {
                return false;
            }
        }

        private TelemetryOperationScope CurrentTelemetryScope()
        {
            try
            {
                TelemetryOperationScope scope = BlobjectInstrumentation.CurrentScope.Value;
                if (scope != null && ReferenceEquals(scope.Client, this)) return scope;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private TelemetryOperationScope BeginTelemetry(string operation, string key)
        {
            if (!BlobjectTelemetry.Enabled) return null;

            return TelemetryOperationScope.Begin(
                this,
                SafeTelemetryProvider(),
                operation,
                SafeTelemetryValue(false),
                SafeTelemetryValue(true),
                key);
        }

        private async Task<T> RunInstrumentedAsync<T>(TelemetryOperationScope scope, Func<Task<T>> body)
        {
            scope.Enter();

            try
            {
                T result = await body().ConfigureAwait(false);
                scope.Complete(null, false);
                return result;
            }
            catch (Exception e)
            {
                scope.Complete(e, SafeIsNotFound(e));
                throw;
            }
            finally
            {
                scope.Exit();
            }
        }

        private async Task RunInstrumentedAsync(TelemetryOperationScope scope, Func<Task> body)
        {
            scope.Enter();

            try
            {
                await body().ConfigureAwait(false);
                scope.Complete(null, false);
            }
            catch (Exception e)
            {
                scope.Complete(e, SafeIsNotFound(e));
                throw;
            }
            finally
            {
                scope.Exit();
            }
        }

        private IEnumerable<BlobMetadata> InstrumentedEnumerate(Func<IEnumerable<BlobMetadata>> source)
        {
            TelemetryOperationScope scope = BeginTelemetry(BlobjectTelemetryNames.OperationEnumerate, null);

            if (scope == null)
            {
                foreach (BlobMetadata md in source())
                {
                    yield return md;
                }

                yield break;
            }

            IEnumerator<BlobMetadata> enumerator = null;
            bool failed = false;

            try
            {
                while (true)
                {
                    bool hasNext;
                    BlobMetadata current = null;

                    // the span is ambient only while the provider runs, never while the caller handles an item
                    scope.Enter();

                    try
                    {
                        if (enumerator == null) enumerator = source().GetEnumerator();
                        hasNext = enumerator.MoveNext();
                        if (hasNext) current = enumerator.Current;
                    }
                    catch (Exception e)
                    {
                        failed = true;
                        scope.Complete(e, SafeIsNotFound(e));
                        throw;
                    }
                    finally
                    {
                        scope.Exit();
                    }

                    if (!hasNext) break;
                    scope.AddObjects(1);
                    yield return current;
                }
            }
            finally
            {
                if (enumerator != null)
                {
                    scope.Enter();
                    try { enumerator.Dispose(); }
                    finally { scope.Exit(); }
                }

                if (!failed) scope.Complete(null, false);
            }
        }

        private async IAsyncEnumerable<BlobMetadata> InstrumentedEnumerateAsync(
            Func<CancellationToken, IAsyncEnumerable<BlobMetadata>> source,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            TelemetryOperationScope scope = BeginTelemetry(BlobjectTelemetryNames.OperationEnumerate, null);

            if (scope == null)
            {
                await foreach (BlobMetadata md in source(token).ConfigureAwait(false))
                {
                    yield return md;
                }

                yield break;
            }

            IAsyncEnumerator<BlobMetadata> enumerator = null;
            bool failed = false;

            try
            {
                while (true)
                {
                    bool hasNext;
                    BlobMetadata current = null;

                    scope.Enter();

                    try
                    {
                        if (enumerator == null) enumerator = source(token).GetAsyncEnumerator(token);
                        hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        if (hasNext) current = enumerator.Current;
                    }
                    catch (Exception e)
                    {
                        failed = true;
                        scope.Complete(e, SafeIsNotFound(e));
                        throw;
                    }
                    finally
                    {
                        scope.Exit();
                    }

                    if (!hasNext) break;
                    scope.AddObjects(1);
                    yield return current;
                }
            }
            finally
            {
                if (enumerator != null)
                {
                    scope.Enter();
                    try { await enumerator.DisposeAsync().ConfigureAwait(false); }
                    finally { scope.Exit(); }
                }

                if (!failed) scope.Complete(null, false);
            }
        }

        #endregion

#pragma warning restore CS8424 // The EnumeratorCancellationAttribute will have no effect. The attribute is only effective on a parameter of type CancellationToken in an async-iterator method returning IAsyncEnumerable
    }
}
