namespace Blobject.AmazonS3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Blobject.Core;

    /// <inheritdoc />
    public class AmazonS3BlobClient : IBlobClient, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Header = "[AmazonS3BlobClient] ";
        private AwsSettings _AwsSettings = null;
        private AmazonS3Client _S3Client = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="AmazonS3BlobClient"/> class.
        /// </summary>
        /// <param name="awsSettings">Settings for <see cref="AmazonS3BlobClient"/>.</param>
        public AmazonS3BlobClient(AwsSettings awsSettings)
        {
            AmazonS3Config s3Config;
            _AwsSettings = awsSettings;
            BasicAWSCredentials s3Credentials = new Amazon.Runtime.BasicAWSCredentials(_AwsSettings.AccessKey, _AwsSettings.SecretKey);

            if (String.IsNullOrEmpty(_AwsSettings.Endpoint))
            {
                Amazon.RegionEndpoint s3Region = _AwsSettings.GetAwsRegionEndpoint();
                s3Config = new AmazonS3Config
                {
                    RegionEndpoint = s3Region,
                    UseHttp = !_AwsSettings.Ssl,
                };

                // _S3Client = new AmazonS3Client(_S3Credentials, _S3Region);
                _S3Client = new AmazonS3Client(s3Credentials, s3Config);
            }
            else
            {
                s3Config = new AmazonS3Config
                {
                    ServiceURL = _AwsSettings.Endpoint,
                    ForcePathStyle = true,
                    UseHttp = !_AwsSettings.Ssl
                };

                _S3Client = new AmazonS3Client(s3Credentials, s3Config);
            }
        }
        
        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            Log("disposing");

            if (!_Disposed)
            {
                _AwsSettings = null;
                _S3Client = null;
                _Disposed = true;
            }

            Log("disposed");
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key,
            };

            using (GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false))
            {
                using (Stream responseStream = response.ResponseStream)
                {
                    using (StreamReader reader = new StreamReader(responseStream))
                    {
                        if (response.ContentLength > 0)
                        {
                            // first copy the stream
                            byte[] data = new byte[response.ContentLength];

                            using (Stream bodyStream = response.ResponseStream)
                            {
                                data = Common.ReadStreamFully(bodyStream);

                                int statusCode = (int)response.HttpStatusCode;
                                return data;
                            }
                        }
                        else
                        {
                            throw new IOException("Unable to read object.");
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key,
            };

            using (GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false))
            {
                BlobData ret = new BlobData();

                if (response.ContentLength > 0)
                {
                    ret.ContentLength = response.ContentLength;
                    await response.ResponseStream.CopyToAsync(ret.Data);
                    ret.Data.Seek(0, SeekOrigin.Begin);
                }
                else
                {
                    ret.ContentLength = 0;
                    ret.Data = new MemoryStream(Array.Empty<byte>());
                }

                return ret;
            }
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            GetObjectMetadataRequest request = new GetObjectMetadataRequest();
            request.BucketName = _AwsSettings.Bucket;
            request.Key = key;

            GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);

            if (response.ContentLength > 0)
            {
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = response.ContentLength;
                md.ContentType = response.Headers.ContentType;
                md.ETag = response.ETag;
                md.CreatedUtc = response.LastModified;

                if (!String.IsNullOrEmpty(md.ETag))
                {
                    while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                }

                return md;
            }
            else
            {
                throw new KeyNotFoundException("The requested object was not found.");
            }
        }

        /// <inheritdoc />
        public Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            long contentLength = 0;
            if (data != null && data.Length > 0)
            {
                using (MemoryStream stream = new MemoryStream(data))
                {
                    contentLength = data.Length;
                    stream.Seek(0, SeekOrigin.Begin);
                    await WriteAsync(key, contentType, contentLength, stream, token).ConfigureAwait(false);
                }
            }
            else
            {
                using (MemoryStream stream = new MemoryStream(Array.Empty<byte>()))
                {
                    contentLength = 0;
                    await WriteAsync(key, contentType, contentLength, stream, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            PutObjectRequest request = new PutObjectRequest();

            if (stream == null || contentLength < 1)
            {
                request.BucketName = _AwsSettings.Bucket;
                request.Key = key;
                request.ContentType = contentType;
                request.UseChunkEncoding = false;
                request.InputStream = new MemoryStream(Array.Empty<byte>());
            }
            else
            {
                request.BucketName = _AwsSettings.Bucket;
                request.Key = key;
                request.ContentType = contentType;
                request.UseChunkEncoding = false;
                request.InputStream = stream;
            }

            await _S3Client.PutObjectAsync(request, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            foreach (WriteRequest obj in objects)
            {
                if (obj.Data != null)
                {
                    await WriteAsync(obj.Key, obj.ContentType, obj.Data, token).ConfigureAwait(false);
                }
                else
                {
                    await WriteAsync(obj.Key, obj.ContentType, obj.ContentLength, obj.DataStream, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string key, CancellationToken token = default)
        {
            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key
            };

            await _S3Client.DeleteObjectAsync(request, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            GetObjectMetadataRequest request = new GetObjectMetadataRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key
            };

            try
            {
                GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);
                return true;
            }
            catch (Amazon.S3.AmazonS3Exception ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return false;

                // status wasn't not found, so throw the exception
                throw;
            }
        }

        /// <inheritdoc />
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            if (!String.IsNullOrEmpty(_AwsSettings.BaseUrl))
            {
                string url = _AwsSettings.BaseUrl;
                url = url.Replace("{bucket}", _AwsSettings.Bucket);
                url = url.Replace("{key}", key);
                return url;
            }
            else
            {
                string ret = "";

                // https://[bucketname].s3.[regionname].amazonaws.com/
                if (_AwsSettings.Ssl) ret = "https://";
                else ret = "http://";

                ret += _AwsSettings.Bucket + ".s3." + S3RegionToString(_AwsSettings.Region) + ".amazonaws.com/" + key;

                return ret;
            }
        }

        /// <inheritdoc />
        public async Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            Log("enumerating using prefix " + prefix);

            ListObjectsRequest req = new ListObjectsRequest();
            req.BucketName = _AwsSettings.Bucket;
            if (!String.IsNullOrEmpty(prefix)) req.Prefix = prefix;

            if (!String.IsNullOrEmpty(continuationToken)) req.Marker = continuationToken;

            ListObjectsResponse resp = await _S3Client.ListObjectsAsync(req, token).ConfigureAwait(false);
            EnumerationResult ret = new EnumerationResult();

            if (resp.S3Objects != null && resp.S3Objects.Count > 0)
            {
                foreach (S3Object curr in resp.S3Objects)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.Key = curr.Key;
                    md.ContentLength = curr.Size;
                    md.ETag = curr.ETag;
                    md.CreatedUtc = curr.LastModified;

                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    ret.Blobs.Add(md);
                }
            }

            if (!String.IsNullOrEmpty(resp.NextMarker)) ret.NextContinuationToken = resp.NextMarker;

            Log("enumeration complete with " + ret.Blobs.Count + " BLOBs");
            return ret;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            string continuationToken = null;

            while (true)
            {
                EnumerationResult result = await EnumerateAsync(null, continuationToken, token).ConfigureAwait(false);
                continuationToken = result.NextContinuationToken;

                if (result.Blobs != null && result.Blobs.Count > 0)
                {
                    foreach (BlobMetadata md in result.Blobs)
                    {
                        await DeleteAsync(md.Key, token).ConfigureAwait(false);
                        er.Blobs.Add(md);
                    }
                }
                else
                {
                    break;
                }
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private string S3RegionToString(AwsRegion region)
        {
            switch (region)
            {
                case AwsRegion.APNortheast1:
                    return "ap-northeast-1";
                case AwsRegion.APNortheast2:
                    return "ap-northeast-2";
                case AwsRegion.APNortheast3:
                    return "ap-northeast-3";
                case AwsRegion.APSouth1:
                    return "ap-south-1";
                case AwsRegion.APSoutheast1:
                    return "ap-southeast-1";
                case AwsRegion.APSoutheast2:
                    return "ap-southeast-2";
                case AwsRegion.APSoutheast3:
                    return "ap-southeast-3";
                case AwsRegion.APSoutheast4:
                    return "ap-southeast-4";
                case AwsRegion.CACentral1:
                    return "ca-central-1";
                case AwsRegion.CNNorth1:
                    return "cn-north-1";
                case AwsRegion.CNNorthwest1:
                    return "cn-northwest-1";
                case AwsRegion.EUCentral1:
                    return "eu-central-1";
                case AwsRegion.EUNorth1:
                    return "eu-north-1";
                case AwsRegion.EUWest1:
                    return "eu-west-1";
                case AwsRegion.EUWest2:
                    return "eu-west-2";
                case AwsRegion.EUWest3:
                    return "eu-west-3";
                case AwsRegion.SAEast1:
                    return "sa-east-1";
                case AwsRegion.USEast1:
                    return "us-east-1";
                case AwsRegion.USEast2:
                    return "us-east-2";
                case AwsRegion.USGovCloudEast1:
                    return "us-gov-east-1";
                case AwsRegion.USGovCloudWest1:
                    return "us-gov-west-1";
                case AwsRegion.USWest1:
                    return "us-west-1";
                case AwsRegion.USWest2:
                    return "us-west-2";
                default:
                    throw new ArgumentException("Unknown region: " + region.ToString());
            }
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion
    }
}