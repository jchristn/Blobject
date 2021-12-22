using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlobHelper
{
    /// <summary>
    /// Settings when using Komodo for storage.
    /// </summary>
    public class KomodoSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// Komodo endpoint URL, of the form http://[hostname]:[port]/.
        /// </summary>
        public string Endpoint { get; set; } = null;

        /// <summary>
        /// Komodo index GUID.
        /// </summary>
        public string IndexGUID { get; set; } = null;

        /// <summary>
        /// Komodo API key.
        /// </summary>
        public string ApiKey { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public KomodoSettings()
        {

        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="endpoint">Komodo endpoint, i.e. http://localhost:8000/</param>
        /// <param name="indexGuid">GUID of the index.</param>
        /// <param name="apiKey">API key with read, write, and delete permissions.</param>
        public KomodoSettings(string endpoint, string indexGuid, string apiKey)
        {
            if (String.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (String.IsNullOrEmpty(indexGuid)) throw new ArgumentNullException(nameof(indexGuid)); 
            if (String.IsNullOrEmpty(apiKey)) throw new ArgumentNullException(nameof(apiKey));

            Endpoint = endpoint;
            IndexGUID = indexGuid;
            ApiKey = apiKey;

            if (!Endpoint.EndsWith("/")) Endpoint += "/";
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
