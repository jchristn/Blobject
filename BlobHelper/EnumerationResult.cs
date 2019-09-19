using System;
using System.Collections.Generic;
using System.Text;

namespace BlobHelper
{
    /// <summary>
    /// Enumeration results.
    /// </summary>
    public class EnumerationResult
    {
        /// <summary>
        /// Next continuation token to supply in order to continue enumerating from the end of the previous request.
        /// </summary>
        public string NextContinuationToken;

        /// <summary>
        /// List of BLOB metadata objects.
        /// </summary>
        public List<BlobMetadata> Blobs;

        /// <summary>
        /// Instantiates the object.
        /// </summary>
        public EnumerationResult()
        {
            Blobs = new List<BlobMetadata>();
        }

        internal EnumerationResult(string continuationToken, List<BlobMetadata> blobs)
        {
            NextContinuationToken = continuationToken;
            Blobs = blobs;
        }
    }
}
