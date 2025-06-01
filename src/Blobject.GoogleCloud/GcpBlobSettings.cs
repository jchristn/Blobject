namespace Blobject.GoogleCloud
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// Settings when using Google Cloud Storage for storage.
    /// </summary>
    public class GcpBlobSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// Google Cloud project ID.
        /// </summary>
        public string ProjectId { get; set; } = null;

        /// <summary>
        /// Google Cloud Storage bucket name.
        /// </summary>
        public string Bucket { get; set; } = null;

        /// <summary>
        /// JSON credentials for service account authentication.
        /// This should be the full JSON content of the service account key file.
        /// </summary>
        public string JsonCredentials { get; set; } = null;

        /// <summary>
        /// Optional: Custom endpoint URL for Google Cloud Storage.
        /// Default is null, which uses the standard GCS endpoint.
        /// </summary>
        public string CustomEndpoint { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Settings when using Google Cloud Storage for storage.
        /// </summary>
        public GcpBlobSettings()
        {
        }

        /// <summary>
        /// Settings when using Google Cloud Storage for storage.
        /// </summary>
        /// <param name="projectId">The Google Cloud project ID.</param>
        /// <param name="bucket">The bucket in which BLOBs should be stored.</param>
        /// <param name="jsonCredentials">The JSON credentials for service account authentication.</param>
        public GcpBlobSettings(string projectId, string bucket, string jsonCredentials)
        {
            if (String.IsNullOrEmpty(projectId)) throw new ArgumentNullException(nameof(projectId));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));
            if (String.IsNullOrEmpty(jsonCredentials)) throw new ArgumentNullException(nameof(jsonCredentials));

            ProjectId = projectId;
            Bucket = bucket;
            JsonCredentials = jsonCredentials;
        }

        /// <summary>
        /// Settings when using Google Cloud Storage for storage.
        /// </summary>
        /// <param name="projectId">The Google Cloud project ID.</param>
        /// <param name="bucket">The bucket in which BLOBs should be stored.</param>
        /// <param name="jsonCredentials">The JSON credentials for service account authentication.</param>
        /// <param name="customEndpoint">Custom endpoint URL for Google Cloud Storage.</param>
        public GcpBlobSettings(string projectId, string bucket, string jsonCredentials, string customEndpoint)
            : this(projectId, bucket, jsonCredentials)
        {
            CustomEndpoint = customEndpoint;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}