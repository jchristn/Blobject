namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Timestamps;

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
        public Timestamp Time { get; set; } = new Timestamp();

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

        /// <summary>
        /// Produce a human-readable string representation of the object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--- Copy Statistics ---" + Environment.NewLine);
            sb.Append("  Success   : " + Success.ToString() + Environment.NewLine);

            if (Exception != null)
            {
                sb.Append("  Exception : " + Exception.Message + Environment.NewLine + Exception.ToString() + Environment.NewLine);
            }

            sb.Append("  Time" + Environment.NewLine);
            sb.Append("    Start      : " + Time.Start.ToString() + Environment.NewLine);
            sb.Append("    End        : " + Time.End.ToString() + Environment.NewLine);
            sb.Append("    Total MS   : " + Time.TotalMs + Environment.NewLine);
            
            sb.Append("  Continuation Tokens : " + ContinuationTokens + Environment.NewLine);
            sb.Append("  BLOBs" + Environment.NewLine);
            sb.Append("    Enumerated : " + BlobsEnumerated + Environment.NewLine);
            sb.Append("    Read       : " + BlobsRead + Environment.NewLine);
            sb.Append("    Written    : " + BlobsWritten + Environment.NewLine);
            sb.Append("  Bytes" + Environment.NewLine);
            sb.Append("    Enumerated : " + BytesEnumerated + Environment.NewLine);
            sb.Append("    Read       : " + BytesRead + Environment.NewLine);
            sb.Append("    Written    : " + BytesWritten + Environment.NewLine);
            sb.Append("  Keys                : " + Keys.Count + Environment.NewLine);

            foreach (string key in Keys)
                sb.Append("    " + key + Environment.NewLine);

            sb.Append("---" + Environment.NewLine);
            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
