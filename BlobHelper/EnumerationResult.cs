using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace BlobHelper
{
    /// <summary>
    /// Enumeration results.
    /// </summary>
    public class EnumerationResult
    {
        #region Public-Members

        /// <summary>
        /// Flag indicating if more results are available.
        /// </summary>
        [JsonProperty(Order = 0)]
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
        [JsonProperty(Order = 1)]
        public string NextContinuationToken { get; set; } = null;

        /// <summary>
        /// The number of BLOBs.
        /// </summary>
        [JsonProperty(Order = 2)]
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
        [JsonProperty(Order = 3)]
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
        [JsonProperty(Order = 999)]
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

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public EnumerationResult()
        {
            Blobs = new List<BlobMetadata>();
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

        #endregion

        #region Private-Methods

        #endregion
    }
}
