using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobHelper
{
    /// <summary>
    /// Settings when using local filesystem for storage.
    /// </summary>
    public class DiskSettings
    {
        #region Public-Members

        /// <summary>
        /// The filesystem directory to use.
        /// </summary>
        public string Directory { get; set; }

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public DiskSettings()
        {

        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="directory">The directory where BLOBs should be stored.</param>
        public DiskSettings(string directory)
        {
            if (String.IsNullOrEmpty(directory)) throw new ArgumentNullException(nameof(directory));
            Directory = directory; 
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
