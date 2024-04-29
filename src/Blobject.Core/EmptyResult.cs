namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Results from a container empty operation. 
    /// </summary>
    public class EmptyResult
    {
        #region Public-Members

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

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public EmptyResult()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
