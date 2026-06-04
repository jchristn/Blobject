namespace Blobject.AmazonS3Lite
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
        public bool Ssl { get; set; } = true;

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
        public string Region
        {
            get
            {
                return _Region;
            }
            set
            {
                _Region = NormalizeRegion(value);
            }
        }

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

        /// <summary>
        /// Check if credentials are configured.
        /// Returns true if both AccessKey and SecretKey are non-null and non-empty.
        /// </summary>
        public bool HasCredentials
        {
            get { return !String.IsNullOrEmpty(AccessKey) && !String.IsNullOrEmpty(SecretKey); }
        }

        #endregion

        #region Private-Members

        private string _Region = "us-west-1";

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
        /// <param name="accessKey">Access key with which to access AWS S3.  Leave null for anonymous access.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.  Leave null for anonymous access.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        public AwsSettings(string accessKey, string secretKey, string region, string bucket)
        {
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));

            Endpoint = null;
            Ssl = true;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Region = region;
            Bucket = bucket;

            ValidateCredentials();
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="accessKey">Access key with which to access AWS S3.  Leave null for anonymous access.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.  Leave null for anonymous access.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        public AwsSettings(string accessKey, string secretKey, string region, string bucket, bool ssl)
        {
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));

            Endpoint = null;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Region = region;
            Bucket = bucket;
            Ssl = ssl;

            ValidateCredentials();
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="endpoint">Override the AWS S3 endpoint (if using non-Amazon storage).  Use the form http://localhost:8000/.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="accessKey">Access key with which to access AWS S3.  Leave null for anonymous access.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.  Leave null for anonymous access.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        /// <param name="baseUrl">Base URL to use for objects, i.e. https://[bucketname].s3.[regionname].amazonaws.com/.  For non-S3 endpoints, use {bucket} and {key} to indicate where these values should be inserted, i.e. http://{bucket}.[hostname]:[port]/{key} or https://[hostname]:[port]/{bucket}/key.</param>
        public AwsSettings(string endpoint, bool ssl, string accessKey, string secretKey, string region, string bucket, string baseUrl)
        {
            if (String.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
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

            ValidateCredentials();
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        private void ValidateCredentials()
        {
            bool hasAccessKey = !String.IsNullOrEmpty(AccessKey);
            bool hasSecretKey = !String.IsNullOrEmpty(SecretKey);
            if (hasAccessKey != hasSecretKey)
                throw new ArgumentException("Both AccessKey and SecretKey must be provided, or neither for anonymous access.");
        }

        private static string NormalizeRegion(string region)
        {
            if (String.IsNullOrEmpty(region)) return region;

            region = region.Trim();
            string normalized = region.ToLowerInvariant()
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "");

            switch (normalized)
            {
                case "afsouth1":
                    return "af-south-1";
                case "apeast1":
                    return "ap-east-1";
                case "apeast2":
                    return "ap-east-2";
                case "apnortheast1":
                    return "ap-northeast-1";
                case "apnortheast2":
                    return "ap-northeast-2";
                case "apnortheast3":
                    return "ap-northeast-3";
                case "apsouth1":
                    return "ap-south-1";
                case "apsouth2":
                    return "ap-south-2";
                case "apsoutheast1":
                    return "ap-southeast-1";
                case "apsoutheast2":
                    return "ap-southeast-2";
                case "apsoutheast3":
                    return "ap-southeast-3";
                case "apsoutheast4":
                    return "ap-southeast-4";
                case "apsoutheast5":
                    return "ap-southeast-5";
                case "apsoutheast6":
                    return "ap-southeast-6";
                case "apsoutheast7":
                    return "ap-southeast-7";
                case "cacentral1":
                    return "ca-central-1";
                case "cawest1":
                    return "ca-west-1";
                case "eucentral1":
                    return "eu-central-1";
                case "eucentral2":
                    return "eu-central-2";
                case "eunorth1":
                    return "eu-north-1";
                case "eusouth1":
                    return "eu-south-1";
                case "eusouth2":
                    return "eu-south-2";
                case "euwest1":
                    return "eu-west-1";
                case "euwest2":
                    return "eu-west-2";
                case "euwest3":
                    return "eu-west-3";
                case "ilcentral1":
                    return "il-central-1";
                case "mecentral1":
                    return "me-central-1";
                case "mesouth1":
                    return "me-south-1";
                case "mxcentral1":
                    return "mx-central-1";
                case "saeast1":
                    return "sa-east-1";
                case "useast1":
                    return "us-east-1";
                case "useast2":
                    return "us-east-2";
                case "usgoveast1":
                    return "us-gov-east-1";
                case "usgovwest1":
                    return "us-gov-west-1";
                case "uswest1":
                    return "us-west-1";
                case "uswest2":
                    return "us-west-2";
                default:
                    if (region.Contains("-")) return region.ToLowerInvariant();
                    return region;
            }
        }

        #endregion
    }
}
