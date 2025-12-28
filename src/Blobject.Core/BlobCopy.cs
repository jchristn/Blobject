namespace Blobject.Core
{
    using System;
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
            if (filter == null) filter = new EnumerationFilter();
            if (stopAfter < -1 || stopAfter == 0) throw new ArgumentException("Value for stopAfter must be -1 or a positive integer.");

            CopyStatistics ret = new CopyStatistics();

            ret.Time.Start = DateTime.Now;

            try
            {
                while (true)
                {
                    if (token.IsCancellationRequested) break;

                    bool maxCopiesReached = false;

                    foreach (BlobMetadata sourceBlob in _From.Enumerate(filter))
                    {
                        if (ret.BlobsWritten >= stopAfter) maxCopiesReached = true;
                        if (maxCopiesReached) break;

                        ret.BlobsEnumerated += 1;
                        ret.BytesEnumerated += sourceBlob.ContentLength;

                        byte[] blobData = await _From.GetAsync(sourceBlob.Key, token).ConfigureAwait(false);

                        ret.BlobsRead += 1;
                        ret.BytesRead += blobData.Length;

                        await _To.WriteAsync(sourceBlob.Key, sourceBlob.ContentType, blobData, token).ConfigureAwait(false);

                        ret.BlobsWritten += 1;
                        ret.BytesWritten += blobData.Length;
                        ret.Keys.Add(sourceBlob.Key);

                        if (stopAfter != -1)
                        {
                            if (ret.BlobsWritten >= stopAfter)
                            {
                                maxCopiesReached = true;
                                break;
                            }
                        }
                    }
                }

                ret.Success = true;
            }
            catch (Exception e)
            {
                ret.Success = false;
                ret.Exception = e;
            }
            finally
            {
                ret.Time.End = DateTime.Now;
            }

            return ret;
        }

        #endregion

        #region Private-Methods
         
        private void Log(string msg)
        {
            if (String.IsNullOrEmpty(msg)) return;
            Logger?.Invoke(_Header + msg);
        }

        #endregion
    }
}
