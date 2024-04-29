namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Enumeration results.
    /// </summary>
    public class EnumerationResult : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Flag indicating if more results are available.
        /// </summary>
        public bool HasMore
        {
            get
            {
                return (!String.IsNullOrEmpty(NextContinuationToken));
            }
        }

        /// <summary>
        /// Next continuation token to supply in order to continue enumerating from the end of the previous request.
        /// </summary>
        public string NextContinuationToken { get; set; } = null;

        /// <summary>
        /// The number of BLOBs.
        /// </summary>
        public long Count
        {
            get
            {
                return _Blobs.Count;
            }
        }

        /// <summary>
        /// The total number of bytes represented by the BLOBs.
        /// </summary>
        public long Bytes
        {
            get
            {
                return _Blobs.Sum(b => b.ContentLength);
            }
        }

        /// <summary>
        /// List of BLOB metadata objects.
        /// </summary>
        public List<BlobMetadata> Blobs
        {
            get
            {
                return _Blobs;
            }
            set
            {
                if (value == null) _Blobs = new List<BlobMetadata>();
                else _Blobs = value;
            }
        }

        #endregion

        #region Private-Members

        private List<BlobMetadata> _Blobs = new List<BlobMetadata>();
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public EnumerationResult()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="continuationToken">Continuation token.</param>
        /// <param name="blobs">BLOBs.</param>
        public EnumerationResult(string continuationToken, List<BlobMetadata> blobs)
        {
            NextContinuationToken = continuationToken;
            if (blobs != null) Blobs = blobs;
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
                NextContinuationToken = null;
                Blobs = null;

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

        #endregion

        #region Private-Methods

        #endregion
    }
}
