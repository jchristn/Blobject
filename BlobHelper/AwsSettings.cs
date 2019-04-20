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
        /// <summary>
        /// Override the AWS S3 endpoint (if using non-Amazon storage), otherwise leave null.
        /// Use the form http://localhost:8000/
        /// </summary>
        public string Hostname { get; private set; }

        /// <summary>
        /// Enable or disable SSL (only if using non-Amazon storage).
        /// </summary>
        public bool Ssl { get; private set; }

        /// <summary>
        /// AWS S3 access key.
        /// </summary>
        public string AccessKey { get; private set; }

        /// <summary>
        /// AWS S3 secret key.
        /// </summary>
        public string SecretKey { get; private set; }

        /// <summary>
        /// AWS S3 region.
        /// </summary>
        public AwsRegion Region { get; private set; }

        /// <summary>
        /// AWS S3 bucket.
        /// </summary>
        public string Bucket { get; private set; }

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
            Hostname = null;
            Ssl = true;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Region = region;
            Bucket = bucket;
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="hostname">Override the AWS S3 endpoint (if using non-Amazon storage).  Use the form http://localhost:8000/.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        public AwsSettings(string hostname, bool ssl, string accessKey, string secretKey, AwsRegion region, string bucket)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));
            Hostname = hostname;
            Ssl = ssl;
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

            switch (region)
            {
                case "APNortheast1":
                    Region = AwsRegion.APNortheast1;
                    break;
                case "APSoutheast1":
                    Region = AwsRegion.APSoutheast1;
                    break;
                case "APSoutheast2":
                    Region = AwsRegion.APSoutheast2;
                    break;
                case "EUWest1":
                    Region = AwsRegion.EUWest1;
                    break;
                case "SAEast1":
                    Region = AwsRegion.SAEast1;
                    break;
                case "USEast1":
                    Region = AwsRegion.USEast1;
                    break;
                case "USGovCloudWest1":
                    Region = AwsRegion.USGovCloudWest1;
                    break;
                case "USWest1":
                    Region = AwsRegion.USWest1;
                    break;
                case "USWest2":
                    Region = AwsRegion.USWest2;
                    break;
            }
        }

        /// <summary>
        /// Initialize the object.
        /// </summary>
        /// <param name="hostname">Override the AWS S3 endpoint (if using non-Amazon storage).  Use the form http://localhost:8000/.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="accessKey">Access key with which to access AWS S3.</param>
        /// <param name="secretKey">Secret key with which to access AWS S3.</param>
        /// <param name="region">AWS region.</param>
        /// <param name="bucket">Bucket in which to store BLOBs.</param>
        public AwsSettings(string hostname, bool ssl, string accessKey, string secretKey, string region, string bucket)
        {
            if (String.IsNullOrEmpty(hostname)) throw new ArgumentNullException(nameof(hostname));
            if (String.IsNullOrEmpty(accessKey)) throw new ArgumentNullException(nameof(accessKey));
            if (String.IsNullOrEmpty(secretKey)) throw new ArgumentNullException(nameof(secretKey));
            if (String.IsNullOrEmpty(region)) throw new ArgumentNullException(nameof(region));
            if (String.IsNullOrEmpty(bucket)) throw new ArgumentNullException(nameof(bucket));
            Hostname = hostname;
            Ssl = ssl;
            AccessKey = accessKey;
            SecretKey = secretKey;
            Bucket = bucket;

            if (!ValidateRegion(region)) throw new ArgumentException("Unable to validate region: " + region);

            switch (region)
            {
                case "APNortheast1":
                    Region = AwsRegion.APNortheast1;
                    break;
                case "APSoutheast1":
                    Region = AwsRegion.APSoutheast1;
                    break;
                case "APSoutheast2":
                    Region = AwsRegion.APSoutheast2;
                    break;
                case "EUWest1":
                    Region = AwsRegion.EUWest1;
                    break;
                case "SAEast1":
                    Region = AwsRegion.SAEast1;
                    break;
                case "USEast1":
                    Region = AwsRegion.USEast1;
                    break;
                case "USGovCloudWest1":
                    Region = AwsRegion.USGovCloudWest1;
                    break;
                case "USWest1":
                    Region = AwsRegion.USWest1;
                    break;
                case "USWest2":
                    Region = AwsRegion.USWest2;
                    break;
            }
        }

        internal Amazon.RegionEndpoint GetAwsRegion()
        { 
            switch (Region)
            {
                case AwsRegion.APNortheast1:
                    return Amazon.RegionEndpoint.APNortheast1;
                case AwsRegion.APSoutheast1:
                    return Amazon.RegionEndpoint.APSoutheast1;
                case AwsRegion.APSoutheast2:
                    return Amazon.RegionEndpoint.APSoutheast2;
                case AwsRegion.EUWest1:
                    return Amazon.RegionEndpoint.EUWest1;
                case AwsRegion.SAEast1:
                    return Amazon.RegionEndpoint.SAEast1;
                case AwsRegion.USEast1:
                    return Amazon.RegionEndpoint.USEast1;
                case AwsRegion.USGovCloudWest1:
                    return Amazon.RegionEndpoint.USGovCloudWest1;
                case AwsRegion.USWest1:
                    return Amazon.RegionEndpoint.USWest1;
                case AwsRegion.USWest2:
                    return Amazon.RegionEndpoint.USWest2;
            }

            throw new ArgumentException("Unknown region: " + Region.ToString());
        }

        private bool ValidateRegion(string region)
        {
            return ValidRegions().Contains(region);
        }

        private List<String> ValidRegions()
        {
            List<string> ret = new List<string>();
            ret.Add("APNortheast1");
            ret.Add("APSoutheast1");
            ret.Add("APSoutheast2");
            ret.Add("EUWest1");
            ret.Add("SAEast1");
            ret.Add("USEast1");
            ret.Add("USGovCloudWest1");
            ret.Add("USWest1");
            ret.Add("USWest2");
            return ret;
        }
    }
}
