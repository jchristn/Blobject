using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BlobHelper
{
    /// <summary>
    /// Contains content-length (how many bytes to read) and data stream for a given object.
    /// </summary>
    public class BlobData
    {
        #region Public-Members

        /// <summary>
        /// Content-length of the object (how many bytes to read from Data).
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
        /// Stream containing requested data.
        /// </summary>
        public Stream Data { get; set; } = null;

        #endregion

        #region Private-Members

        private long _ContentLength = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public BlobData()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="contentLength">Content length.</param>
        /// <param name="data">Data stream.</param>
        public BlobData(long contentLength, Stream data)
        {
            ContentLength = contentLength;
            Data = data;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
