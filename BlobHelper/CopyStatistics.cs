using System;
using System.Collections.Generic;
using System.Text;

namespace BlobHelper
{
    /// <summary>
    /// BLOB copy statistics.
    /// </summary>
    public class CopyStatistics
    {
        #region Public-Members

        /// <summary>
        /// Flag indicating if the operation was successful.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Timestamps from the copy operation.
        /// </summary>
        public Timestamps Time { get; set; } = new Timestamps();

        /// <summary>
        /// Exception, if any was encountered.
        /// </summary>
        public Exception Exception { get; set; } = null;

        /// <summary>
        /// Number of continuation tokens used.
        /// </summary>
        public int ContinuationTokens { get; set; } = 0;

        /// <summary>
        /// Number of BLOBs enumerated.
        /// </summary>
        public long BlobsEnumerated { get; set; } = 0;

        /// <summary>
        /// Number of bytes enumerated.
        /// </summary>
        public long BytesEnumerated { get; set; } = 0;

        /// <summary>
        /// Number of BLOBs read.
        /// </summary>
        public long BlobsRead { get; set; } = 0;

        /// <summary>
        /// Number of bytes read.
        /// </summary>
        public long BytesRead { get; set; } = 0;

        /// <summary>
        /// Number of BLOBs written.
        /// </summary>
        public long BlobsWritten { get; set; } = 0;

        /// <summary>
        /// Number of bytes written.
        /// </summary>
        public long BytesWritten { get; set; } = 0;

        /// <summary>
        /// Keys copied.
        /// </summary>
        public List<string> Keys { get; set; } = new List<string>();

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CopyStatistics()
        {

        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
