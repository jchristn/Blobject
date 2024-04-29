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

        private IBlobClient _From = null;
        private IBlobClient _To = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="copyFrom">Settings of the repository from which objects should be copied.</param>
        /// <param name="copyTo">Settings of the repository to which objects should be copied.</param>
        /// <param name="prefix">Prefix of the objects that should be copied.</param>
        public BlobCopy(IBlobClient copyFrom, IBlobClient copyTo, string prefix = null)
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
        /// <param name="token">Cancellation token.</param>
        /// <returns>Copy statistics.</returns>
        public async Task<CopyStatistics> Start(int stopAfter = -1, CancellationToken token = default)
        {
            if (stopAfter < -1 || stopAfter == 0) throw new ArgumentException("Value for stopAfter must be -1 or a positive integer.");

            CopyStatistics ret = new CopyStatistics();
            ret.Time.Start = DateTime.Now;

            try
            {
                while (true)
                {
                    if (token.IsCancellationRequested) break;

                    string continuationToken = null;
                    EnumerationResult enumResult = await _From.EnumerateAsync(_Prefix, continuationToken, token).ConfigureAwait(false);
                    if (enumResult == null)
                    {
                        Log("no enumeration resource from source");
                        break;
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(enumResult.NextContinuationToken)) continuationToken = enumResult.NextContinuationToken;

                        ret.BlobsEnumerated += enumResult.Count;
                        ret.BytesEnumerated += enumResult.Bytes;

                        if (enumResult.Blobs != null && enumResult.Blobs.Count > 0)
                        {
                            bool maxCopiesReached = false;

                            foreach (BlobMetadata blob in enumResult.Blobs)
                            {
                                byte[] blobData = await _From.GetAsync(blob.Key, token).ConfigureAwait(false);

                                ret.BlobsRead += 1;
                                ret.BytesRead += blobData.Length;

                                await _To.WriteAsync(blob.Key, blob.ContentType, blobData, token).ConfigureAwait(false);

                                ret.BlobsWritten += 1;
                                ret.BytesWritten += blobData.Length;
                                ret.Keys.Add(blob.Key);

                                if (stopAfter != -1)
                                {
                                    if (ret.BlobsWritten >= stopAfter)
                                    {
                                        maxCopiesReached = true;
                                        break;
                                    }
                                }
                            }

                            if (maxCopiesReached)
                            {
                                break;
                            }
                        }

                        if (!enumResult.HasMore) break;
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
