namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Timestamps;

    /// <summary>
    /// BLOB copy.
    /// </summary>
    public class BlobCopy : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Header = "[BlobCopy] ";
        private string _Prefix = null;

        private BlobClientBase _From = null;
        private BlobClientBase _To = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="copyFrom">Repository from which objects should be copied.</param>
        /// <param name="copyTo">Repository to which objects should be copied.</param>
        /// <param name="prefix">Prefix of the objects that should be copied.</param>
        public BlobCopy(BlobClientBase copyFrom, BlobClientBase copyTo, string prefix = null)
        {
            if (copyFrom == null) throw new ArgumentNullException(nameof(copyFrom));
            if (copyTo == null) throw new ArgumentNullException(nameof(copyTo));

            _From = copyFrom;
            _To = copyTo;
            _Prefix = prefix;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                _Prefix = null;
                _From = null;
                _To = null;
                _Disposed = true;
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start the copy operation.
        /// </summary>
        /// <param name="stopAfter">Stop after this many objects have been copied.</param>
        /// <param name="filter">Enumeration filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Copy statistics.</returns>
        public async Task<CopyStatistics> Start(int stopAfter = -1, EnumerationFilter filter = null, CancellationToken token = default)
        {
            return await StartAsync(stopAfter, filter, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Start the copy operation asynchronously.
        /// </summary>
        /// <param name="stopAfter">Stop after this many objects have been copied.</param>
        /// <param name="filter">Enumeration filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Copy statistics.</returns>
        public async Task<CopyStatistics> StartAsync(int stopAfter = -1, EnumerationFilter filter = null, CancellationToken token = default)
        {
            if (stopAfter < -1 || stopAfter == 0) throw new ArgumentException("Value for stopAfter must be -1 or a positive integer.");

            filter = filter != null ? filter.Clone() : new EnumerationFilter();
            if (!String.IsNullOrEmpty(_Prefix) && String.IsNullOrEmpty(filter.Prefix)) filter.Prefix = _Prefix;

            CopyStatistics ret = new CopyStatistics();

            string sourceProvider = _From.SafeTelemetryProvider();
            string targetProvider = _To.SafeTelemetryProvider();
            long jobStart = Stopwatch.GetTimestamp();
            Activity previous = Activity.Current;
            Activity job = StartSpan(BlobjectTelemetryNames.SpanCopy, sourceProvider, targetProvider, null);
            Exception failure = null;

            ret.Time.Start = DateTime.Now;

            try
            {
                IAsyncEnumerator<BlobMetadata> enumerator = _From.EnumerateAsync(filter, token).GetAsyncEnumerator(token);

                try
                {
                    while (true)
                    {
                        if (token.IsCancellationRequested) break;

                        long enumerateStart = Stopwatch.GetTimestamp();
                        bool hasNext;

                        try
                        {
                            hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                        }
                        catch (Exception e)
                        {
                            BlobjectInstrumentation.RecordCopyStage(sourceProvider, targetProvider, BlobjectTelemetryNames.StageEnumerate, BlobjectInstrumentation.ElapsedSeconds(enumerateStart), e);
                            throw;
                        }

                        if (!hasNext) break;
                        BlobjectInstrumentation.RecordCopyStage(sourceProvider, targetProvider, BlobjectTelemetryNames.StageEnumerate, BlobjectInstrumentation.ElapsedSeconds(enumerateStart), null);

                        BlobMetadata sourceBlob = enumerator.Current;
                        if (sourceBlob == null) continue;

                        ret.BlobsEnumerated += 1;
                        ret.BytesEnumerated += sourceBlob.ContentLength;

                        string targetKey = sourceBlob.Key;
                        string contentType = String.IsNullOrEmpty(sourceBlob.ContentType)
                            ? "application/octet-stream"
                            : sourceBlob.ContentType;

                        if (sourceBlob.IsFolder)
                        {
                            if (!targetKey.EndsWith("/") && !targetKey.EndsWith("\\")) targetKey += "/";
                            await RunStageAsync(BlobjectTelemetryNames.StageWrite, sourceProvider, targetProvider, () =>
                                _To.WriteAsync(targetKey, contentType, Array.Empty<byte>(), token)).ConfigureAwait(false);
                            ret.BlobsRead += 1;
                            ret.BlobsWritten += 1;
                            ret.Keys.Add(targetKey);
                        }
                        else
                        {
                            BlobData blobData = null;
                            await RunStageAsync(BlobjectTelemetryNames.StageRead, sourceProvider, targetProvider, async () =>
                            {
                                blobData = await _From.GetStreamAsync(sourceBlob.Key, token).ConfigureAwait(false);
                            }).ConfigureAwait(false);

                            using (blobData)
                            {
                                long contentLength = blobData != null ? blobData.ContentLength : 0;

                                await RunStageAsync(BlobjectTelemetryNames.StageWrite, sourceProvider, targetProvider, async () =>
                                {
                                    if (blobData != null && blobData.Data != null)
                                    {
                                        if (blobData.Data.CanSeek && blobData.Data.Length == blobData.Data.Position)
                                            blobData.Data.Seek(0, SeekOrigin.Begin);

                                        await _To.WriteAsync(targetKey, contentType, contentLength, blobData.Data, token).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        using (MemoryStream empty = new MemoryStream(Array.Empty<byte>()))
                                        {
                                            await _To.WriteAsync(targetKey, contentType, 0, empty, token).ConfigureAwait(false);
                                        }
                                    }
                                }).ConfigureAwait(false);

                                ret.BlobsRead += 1;
                                ret.BytesRead += contentLength;
                                ret.BlobsWritten += 1;
                                ret.BytesWritten += contentLength;
                                ret.Keys.Add(targetKey);
                            }
                        }

                        if (stopAfter != -1 && ret.BlobsWritten >= stopAfter) break;
                    }
                }
                finally
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }

                ret.Success = true;
            }
            catch (Exception e)
            {
                failure = e;
                ret.Success = false;
                ret.Exception = e;
            }
            finally
            {
                ret.Time.End = DateTime.Now;
            }

            CompleteSpan(job, failure, ret.BlobsWritten, ret.BytesWritten);
            BlobjectInstrumentation.RecordCopyJob(sourceProvider, targetProvider, BlobjectInstrumentation.ElapsedSeconds(jobStart), failure, ret.BlobsWritten, ret.BytesWritten);
            RestoreCurrent(previous);
            if (failure != null) Log("copy failed: " + failure.Message);

            return ret;
        }

        #endregion

        #region Private-Methods

        private void Log(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Logger?.Invoke(_Header + msg);
        }

        private static Activity StartSpan(string name, string sourceProvider, string targetProvider, string stage)
        {
            if (!BlobjectTelemetry.Enabled) return null;

            try
            {
                Activity activity = BlobjectInstrumentation.Source.StartActivity(name, ActivityKind.Internal);
                if (activity != null && activity.IsAllDataRequested)
                {
                    activity.SetTag(BlobjectTelemetryNames.AttributeCopySource, sourceProvider);
                    activity.SetTag(BlobjectTelemetryNames.AttributeCopyTarget, targetProvider);
                    if (stage != null) activity.SetTag(BlobjectTelemetryNames.AttributeStage, stage);
                }

                return activity;
            }
            catch
            {
                return null;
            }
        }

        private static void CompleteSpan(Activity activity, Exception e, long objects, long bytes)
        {
            if (activity == null) return;

            try
            {
                if (activity.IsAllDataRequested)
                {
                    string outcome = BlobjectInstrumentation.ClassifyOutcome(e, false);
                    activity.SetTag(BlobjectTelemetryNames.AttributeOutcome, outcome);
                    if (objects >= 0) activity.SetTag(BlobjectTelemetryNames.AttributeObjects, objects);
                    if (bytes >= 0) activity.SetTag(BlobjectTelemetryNames.AttributeBytes, bytes);

                    if (e != null && !(e is OperationCanceledException))
                    {
                        activity.SetTag(BlobjectTelemetryNames.AttributeErrorType, BlobjectInstrumentation.ErrorType(e));
                        activity.SetStatus(ActivityStatusCode.Error, e.Message);
                        BlobjectInstrumentation.RecordException(activity, e);
                    }
                    else if (e == null)
                    {
                        activity.SetStatus(ActivityStatusCode.Ok);
                    }
                }

                activity.Dispose();
            }
            catch
            {
                // best-effort
            }
        }

        private static void RestoreCurrent(Activity previous)
        {
            try
            {
                Activity.Current = previous;
            }
            catch
            {
                // best-effort
            }
        }

        private static async Task RunStageAsync(string stage, string sourceProvider, string targetProvider, Func<Task> body)
        {
            long start = Stopwatch.GetTimestamp();
            Activity previous = Activity.Current;
            Activity activity = StartSpan("stage:" + stage, sourceProvider, targetProvider, stage);
            Exception failure = null;

            try
            {
                await body().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                failure = e;
                throw;
            }
            finally
            {
                CompleteSpan(activity, failure, -1, -1);
                BlobjectInstrumentation.RecordCopyStage(sourceProvider, targetProvider, stage, BlobjectInstrumentation.ElapsedSeconds(start), failure);
                RestoreCurrent(previous);
            }
        }

        #endregion
    }
}
