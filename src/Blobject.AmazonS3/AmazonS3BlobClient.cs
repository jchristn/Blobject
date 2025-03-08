namespace Blobject.AmazonS3
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.Runtime;
    using Amazon.Runtime.Internal;
    using Amazon.S3;
    using Amazon.S3.Model;
    using Blobject.Core;

    /// <inheritdoc />
    public class AmazonS3BlobClient : BlobClientBase, IDisposable
    {
        #region Public-Members

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
            if (awsSettings == null) throw new ArgumentNullException(nameof(awsSettings));

            AmazonS3Config s3Config;
            _AwsSettings = awsSettings;
            BasicAWSCredentials s3Credentials = new Amazon.Runtime.BasicAWSCredentials(_AwsSettings.AccessKey, _AwsSettings.SecretKey);

            if (String.IsNullOrEmpty(_AwsSettings.Endpoint))
            {
                s3Config = new AmazonS3Config
                {
                    RegionEndpoint = _AwsSettings.AwsRegion,
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
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override async Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            try
            {
                List<string> buckets = await ListBuckets(token).ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// List buckets available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of bucket names.</returns>
        public async Task<List<string>> ListBuckets(CancellationToken token = default)
        {
            ListBucketsRequest lbr = new ListBucketsRequest();
            List<string> ret = new List<string>();

            ListBucketsResponse response = await _S3Client.ListBucketsAsync(lbr, token).ConfigureAwait(false);
            if (response != null && response.Buckets != null)
            {
                foreach (var bucket in response.Buckets)
                {
                    ret.Add(bucket.BucketName);
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public override async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            GetObjectRequest request = new GetObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key,
            };

            using (GetObjectResponse response = await _S3Client.GetObjectAsync(request, token).ConfigureAwait(false))
            {
                if (response != null && response.HttpStatusCode == System.Net.HttpStatusCode.OK)
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
                                return null;
                            }
                        }
                    }
                }
                else
                {
                    throw new IOException("Unable to read object.");
                }
            }
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
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
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            GetObjectMetadataRequest request = new GetObjectMetadataRequest();
            request.BucketName = _AwsSettings.Bucket;
            request.Key = key;

            GetObjectMetadataResponse response = await _S3Client.GetObjectMetadataAsync(request, token).ConfigureAwait(false);

            if (response != null && response.HttpStatusCode == System.Net.HttpStatusCode.OK)
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
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
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
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
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
        public override async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
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
        public override async Task DeleteAsync(string key, CancellationToken token = default)
        {
            DeleteObjectRequest request = new DeleteObjectRequest
            {
                BucketName = _AwsSettings.Bucket,
                Key = key
            };

            await _S3Client.DeleteObjectAsync(request, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync(string key, CancellationToken token = default)
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
        public override string GenerateUrl(string key, CancellationToken token = default)
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

                ret += _AwsSettings.Bucket + ".s3." + _AwsSettings.Region + ".amazonaws.com/" + key;

                return ret;
            }
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            string marker = "";

            while (true)
            {
                ListObjectsRequest req = new ListObjectsRequest();
                req.BucketName = _AwsSettings.Bucket;
                if (!String.IsNullOrEmpty(filter.Prefix)) req.Prefix = filter.Prefix;

                if (!String.IsNullOrEmpty(marker)) req.Marker = marker;

                ListObjectsResponse resp = _S3Client.ListObjectsAsync(req).Result;

                if (resp.S3Objects != null && resp.S3Objects.Count > 0)
                {
                    foreach (S3Object curr in resp.S3Objects)
                    {
                        if (curr.Size < filter.MinimumSize || curr.Size > filter.MaximumSize) continue;
                        if (!String.IsNullOrEmpty(filter.Suffix) && !curr.Key.EndsWith(filter.Suffix)) continue;

                        BlobMetadata md = new BlobMetadata();
                        md.Key = curr.Key;
                        md.ContentLength = curr.Size;
                        md.ETag = curr.ETag;
                        md.CreatedUtc = curr.LastModified;
                        md.LastAccessUtc = curr.LastModified;
                        md.LastUpdateUtc = curr.LastModified;

                        if (!String.IsNullOrEmpty(md.ETag))
                        {
                            if (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                        }

                        yield return md;
                    }
                }

                marker = resp.NextMarker;

                if (String.IsNullOrEmpty(marker)) break;
            }

            yield break;
        }

        /// <inheritdoc />
        public override async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            foreach (BlobMetadata md in Enumerate())
            {
                await DeleteAsync(md.Key, token).ConfigureAwait(false);
                er.Blobs.Add(md);
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion
    }
}