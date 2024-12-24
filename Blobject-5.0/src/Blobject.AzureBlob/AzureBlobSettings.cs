namespace Blobject.AzureBlob
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// Settings when using Microsoft Azure BLOB Storage for storage.
    /// </summary>
    public class AzureBlobSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable SSL (only if using non-Azure storage).
        /// </summary>
        public bool Ssl { get; set; } = true;

        /// <summary>
        /// Microsoft Azure BLOB storage account name (the name of the account in the Azure portal).
        /// </summary>
        public string AccountName { get; set; } = null;

        /// <summary>
        /// Microsoft Azure BLOB storage access key.
        /// </summary>
        public string AccessKey { get; set; } = null;

        /// <summary>
        /// Microsoft Azure BLOB storage endpoint (primary or secondary from the Azure portal, likely of the form https://[accountname].blob.core.windows.net/.
        /// </summary>
        public string Endpoint { get; set; } = null;

        /// <summary>
        /// Microsoft Azure BLOB storage container.
        /// </summary>
        public string Container { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public AzureBlobSettings()
        {

        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="accountName">The account name.</param>
        /// <param name="accessKey">The access key with which to access Azure BLOB storage.</param>
        /// <param name="endpoint">The Azure BLOB storage endpoint for the account.</param>
        /// <param name="container">The container in which BLOBs should be stored.</param>
        public AzureBlobSettings(string accountName, string accessKey, string endpoint, string container)
        {
            if (String.IsNullOrEmpty(accountName)) throw new ArgumentNullException(nameof(accountName));
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (String.IsNullOrEmpty(container)) throw new ArgumentNullException(nameof(container));
            AccountName = accountName;
            AccessKey = accessKey;
            Endpoint = endpoint;
            Container = container;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
