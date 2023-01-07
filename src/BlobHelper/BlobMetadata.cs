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
        public string Key { get; set; } = null;

        /// <summary>
        /// Content type for the object.
        /// </summary>
        public string ContentType { get; set; } = null;

        /// <summary>
        /// Content length of the object.
        /// </summary>
        public long ContentLength
        {
            get
            {
                return _ContentLength;
            }
            set
            {
                if (value < 0) throw new ArgumentException("Content length must be zero or greater.");
                _ContentLength = value;
            }
        }

        /// <summary>
        /// ETag of the object.
        /// </summary>
        public string ETag { get; set; } = null;

        /// <summary>
        /// Timestamp from when the object was created.
        /// </summary>
        public DateTime? CreatedUtc { get; set; } = null;

        /// <summary>
        /// Timestamp from when the object was last updated, if available.
        /// </summary>
        public DateTime? LastUpdateUtc { get; set; } = null;

        /// <summary>
        /// Timestamp from when the object was last accessed, if available.
        /// </summary>
        public DateTime? LastAccessUtc { get; set; } = null;

        #endregion

        #region Private-Members

        private long _ContentLength = 0;

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
                "   Last Update    : " + LastUpdateUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;

            if (LastAccessUtc != null) ret +=
                "   Last Access    : " + LastAccessUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;

            return ret; 
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
