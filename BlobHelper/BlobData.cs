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
        /// <summary>
        /// Content-length of the object (how many bytes to read from Data).
        /// </summary>
        public long ContentLength;

        /// <summary>
        /// Stream containing requested data.
        /// </summary>
        public Stream Data;

        /// <summary>
        /// Instantiates the object.
        /// </summary>
        public BlobData()
        {

        }

        internal BlobData(long contentLength, Stream data)
        {
            ContentLength = contentLength;
            Data = data;
        }
    }
}
