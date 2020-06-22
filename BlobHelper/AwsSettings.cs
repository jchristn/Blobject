using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 
namespace BlobHelper
{
    /// <summary>
    /// Settings when using AWS S3 for storage.
    /// </summary>
    public class AwsSettings
    {
        #region Public-Members

        /// <summary>
        /// Override the AWS S3 endpoint (if using non-Amazon storage), otherwise leave null.
        /// Use the form http://localhost:8000/
        /// </summary>
        public string Endpoint { get; set; }

        /// <summary>
        /// Enable or disable SSL (only if using non-Amazon storage).
        /// </summary>
        public bool Ssl { get; set; }

        /// <summary>
        /// AWS S3 access key.
        /// </summary>
        public string AccessKey { get; set; }

        /// <summary>
        /// AWS S3 secret key.
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// AWS S3 region.
        /// </summary>
        public AwsRegion Region { get; set; }

        /// <summary>
        /// AWS S3 bucket.
        /// </summary>
        public string Bucket { get; set; }

        /// <summary>
        /// Base URL to use for objects, i.e. https://[bucketname].s3.[regionname].amazonaws.com/
        /// </summary>
        public string BaseUrl { get; set; }

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
        public AwsSettings(string accessKey, string secretKey, AwsRegion region, string bucket)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
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
        public AwsSettings(string accessKey, string secretKey, string region, string bucket)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));

            AccessKey = accessKey;
            SecretKey = secretKey;
            Bucket = bucket;

            if (!ValidateRegion(region)) throw new ArgumentException("Unable to validate region: " + region);
            Region = GetRegionFromString(region);
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        public AwsSettings(string accessKey, string secretKey, AwsRegion region, string bucket, bool ssl)
        {
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
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
            
            AccessKey = accessKey;
            SecretKey = secretKey;
            Bucket = bucket;
            Ssl = ssl;

            if (!ValidateRegion(region)) throw new ArgumentException("Unable to validate region: " + region);
            Region = GetRegionFromString(region);
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
        /// <param name="baseUrl">Base URL to use for objects, i.e. https://[bucketname].s3.[regionname].amazonaws.com/</param>
        public AwsSettings(string endpoint, bool ssl, string accessKey, string secretKey, AwsRegion region, string bucket, string baseUrl)
        {
            if (String.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
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

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="endpoint">Override the AWS S3 endpoint (if using non-Amazon storage).  Use the form http://localhost:8000/.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        /// <param name="baseUrl">Base URL to use for objects, i.e. https://[bucketname].s3.[regionname].amazonaws.com/</param>
        public AwsSettings(string endpoint, bool ssl, string accessKey, string secretKey, string region, string bucket, string baseUrl)
        {
            if (String.IsNullOrEmpty(endpoint)) throw new ArgumentNullException(nameof(endpoint));
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));
            if (String.IsNullOrEmpty(baseUrl)) throw new ArgumentNullException(nameof(baseUrl));

            Endpoint = endpoint;
            Ssl = ssl;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Bucket = bucket;
            BaseUrl = baseUrl;

            if (!BaseUrl.EndsWith("/")) BaseUrl += "/";

            if (!ValidateRegion(region)) throw new ArgumentException("Unable to validate region: " + region);
            Region = GetRegionFromString(region);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Retrieve AWS region endpoint.
        /// </summary>
        /// <returns>AWS region endpoint.</returns>
        public Amazon.RegionEndpoint GetAwsRegionEndpoint()
        { 
            switch (Region)
            {
                case AwsRegion.APNortheast1:
                    return Amazon.RegionEndpoint.APNortheast1;
                case AwsRegion.APNortheast2:
                    return Amazon.RegionEndpoint.APNortheast2;
                case AwsRegion.APNortheast3:
                    return Amazon.RegionEndpoint.APNortheast3;
                case AwsRegion.APSouth1:
                    return Amazon.RegionEndpoint.APSouth1;
                case AwsRegion.APSoutheast1:
                    return Amazon.RegionEndpoint.APSoutheast1;
                case AwsRegion.APSoutheast2:
                    return Amazon.RegionEndpoint.APSoutheast2;
                case AwsRegion.CACentral1:
                    return Amazon.RegionEndpoint.CACentral1;
                case AwsRegion.CNNorth1:
                    return Amazon.RegionEndpoint.CNNorth1;
                case AwsRegion.EUCentral1:
                    return Amazon.RegionEndpoint.EUCentral1;
                case AwsRegion.EUNorth1:
                    return Amazon.RegionEndpoint.EUNorth1;
                case AwsRegion.EUWest1:
                    return Amazon.RegionEndpoint.EUWest1;
                case AwsRegion.SAEast1:
                    return Amazon.RegionEndpoint.SAEast1;
                case AwsRegion.USEast1:
                    return Amazon.RegionEndpoint.USEast1;
                case AwsRegion.USEast2:
                    return Amazon.RegionEndpoint.USEast2;
                case AwsRegion.USGovCloudEast1:
                    return Amazon.RegionEndpoint.USGovCloudEast1;
                case AwsRegion.USGovCloudWest1:
                    return Amazon.RegionEndpoint.USGovCloudWest1;
                case AwsRegion.USWest1:
                    return Amazon.RegionEndpoint.USWest1;
                case AwsRegion.USWest2:
                    return Amazon.RegionEndpoint.USWest2;
            }

            throw new ArgumentException("Unknown region: " + Region.ToString());
        }

        internal bool ValidateRegion(string region)
        {
            return ValidRegions().Contains(region);
        }
         
        #endregion

        #region Private-Methods

        private List<String> ValidRegions()
        {
            List<string> ret = new List<string>();
            ret.Add("APNortheast1");
            ret.Add("APNortheast2");
            ret.Add("APNortheast3");
            ret.Add("APSoutheast1");
            ret.Add("APSoutheast2");
            ret.Add("APSouth1");
            ret.Add("CACentral1");
            ret.Add("CNNorth1");
            ret.Add("EUCentral1");
            ret.Add("EUNorth1");
            ret.Add("EUWest1");
            ret.Add("EUWest2");
            ret.Add("EUWest3");
            ret.Add("SAEast1");
            ret.Add("USEast1");
            ret.Add("USEast2");
            ret.Add("USGovCloudEast1");
            ret.Add("USGovCloudWest1");
            ret.Add("USWest1");
            ret.Add("USWest2");
            return ret;
        }

        private AwsRegion GetRegionFromString(string region)
        {
            switch (region)
            {
                case "APNortheast1":
                    return AwsRegion.APNortheast1;
                case "APSouth1":
                    return AwsRegion.APSouth1;
                case "APSoutheast1":
                    return AwsRegion.APSoutheast1;
                case "APSoutheast2":
                    return AwsRegion.APSoutheast2;
                case "CACentral1":
                    return AwsRegion.CACentral1;
                case "CNNorth1":
                    return AwsRegion.CNNorth1;
                case "EUCentral1":
                    return AwsRegion.EUCentral1;
                case "EUNorth1":
                    return AwsRegion.EUNorth1;
                case "EUWest1":
                    return AwsRegion.EUWest1;
                case "EUWest2":
                    return AwsRegion.EUWest2;
                case "EUWest3":
                    return AwsRegion.EUWest3;
                case "SAEast1":
                    return AwsRegion.SAEast1;
                case "USEast1":
                    return AwsRegion.USEast1;
                case "USEast2":
                    return AwsRegion.USEast2;
                case "USGovCloudEast1":
                    return AwsRegion.USGovCloudEast1;
                case "USGovCloudWest1":
                    return AwsRegion.USGovCloudWest1;
                case "USWest1":
                    return AwsRegion.USWest1;
                case "USWest2":
                    return AwsRegion.USWest2;
                default:
                    throw new ArgumentException("Unknown region: " + region);
            }
        }

        #endregion
    }
}
