﻿namespace Blobject.AmazonS3Lite
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Blobject.Core;
    using S3Lite;

    /// <summary>
    /// Settings when using AWS S3 for storage.
    /// </summary>
    public class AwsSettings : BlobSettings
    {
        #region Public-Members

        /// <summary>
        /// Override the AWS S3 endpoint (if using non-Amazon storage), otherwise leave null.
        /// Use the form http://localhost:8000/
        /// </summary>
        public string Endpoint { get; set; } = null;

        /// <summary>
        /// Enable or disable SSL (only if using non-Amazon storage).
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// AWS S3 access key.
        /// </summary>
        public string AccessKey { get; set; } = null;

        /// <summary>
        /// AWS S3 secret key.
        /// </summary>
        public string SecretKey { get; set; } = null;

        /// <summary>
        /// AWS S3 region.
        /// </summary>
        public string Region { get; set; } = "us-west-1";

        /// <summary>
        /// AWS S3 bucket.
        /// </summary>
        public string Bucket { get; set; } = null;

        /// <summary>
        /// Base URL to use for objects, i.e. https://[bucketname].s3.[regionname].amazonaws.com/.
        /// For non-S3 endpoints, use {bucket} and {key} to indicate where these values should be inserted, i.e. http://{bucket}.[hostname]:[port]/{key} or https://[hostname]:[port]/{bucket}/key.
        /// </summary>
        public string BaseUrl { get; set; } = null;

        /// <summary>
        /// Request style.
        /// Virtual-hosted style URLs are of the form http://{bucket}.{hostname}:{port}/{key}.
        /// Path-style URLs are of the form http://{hostname}:{port}/{bucket}/{key}.
        /// </summary>
        public RequestStyleEnum RequestStyle { get; set; } = RequestStyleEnum.VirtualHostedStyle;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the object.
        /// </summary>
        public AwsSettings()
        {

        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        public AwsSettings(string accessKey, string secretKey, string region, string bucket)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));

            Endpoint = null;
            Ssl = true;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Region = region;
            Bucket = bucket; 
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        public AwsSettings(string accessKey, string secretKey, string region, string bucket, bool ssl)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));

            Endpoint = null;
            Ssl = true;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Region = region;
            Bucket = bucket;
            Ssl = ssl; 
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="endpoint">Override the AWS S3 endpoint (if using non-Amazon storage).  Use the form http://localhost:8000/.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        /// <param name="baseUrl">Base URL to use for objects, i.e. https://[bucketname].s3.[regionname].amazonaws.com/.  For non-S3 endpoints, use {bucket} and {key} to indicate where these values should be inserted, i.e. http://{bucket}.[hostname]:[port]/{key} or https://[hostname]:[port]/{bucket}/key.</param>
        public AwsSettings(string endpoint, bool ssl, string accessKey, string secretKey, string region, string bucket, string baseUrl)
        {
            if (String.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));
            if (String.IsNullOrEmpty(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));
            
            Endpoint = endpoint;
            Ssl = ssl;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Region = region;
            Bucket = bucket;
            BaseUrl = baseUrl;

            if (!BaseUrl.EndsWith("/")) BaseUrl += "/"; 
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
