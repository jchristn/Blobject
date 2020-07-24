using System;
using System.Collections.Generic;
using System.Text;

namespace BlobHelper
{
    /// <summary>
    /// Metadata about a BLOB.
    /// </summary>
    public class BlobMetadata
    {
        #region Public-Members

        /// <summary>
        /// Object key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Content type for the object.
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Content length of the object.
        /// </summary>
        public long ContentLength { get; set; }

        /// <summary>
        /// ETag of the object.
        /// </summary>
        public string ETag { get; set; }

        /// <summary>
        /// Timestamp from when the object was created.
        /// </summary>
        public DateTime? CreatedUtc = null;

        /// <summary>
        /// Timestamp from when the object was last updated, if available.
        /// </summary>
        public DateTime? LastUpdateUtc = null;

        /// <summary>
        /// Timestamp from when the object was last accessed, if available.
        /// </summary>
        public DateTime? LastAccessUtc = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public BlobMetadata()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a human-readable string of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret =
                "---" + Environment.NewLine +
                "   Key            : " + Key + Environment.NewLine +
                "   Content Type   : " + ContentType + Environment.NewLine +
                "   Content Length : " + ContentLength + Environment.NewLine +
                "   ETag           : " + ETag + Environment.NewLine;

            if (CreatedUtc != null) ret +=
                "   Created        : " + CreatedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;

            if (LastUpdateUtc != null) ret +=
                "   Last Update    : " + CreatedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;

            if (LastAccessUtc != null) ret +=
                "   Last Access    : " + CreatedUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;

            return ret; 
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
