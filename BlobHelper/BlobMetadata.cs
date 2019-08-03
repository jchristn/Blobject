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
        public DateTime Created { get; set; }

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
                "   ETag           : " + ETag + Environment.NewLine +
                "   Created        : " + Created.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine;

            return ret; 
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
