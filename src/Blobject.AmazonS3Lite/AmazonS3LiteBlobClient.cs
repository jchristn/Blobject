namespace Blobject.AmazonS3Lite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using S3Lite;
    using S3Lite.ApiObjects;
    using Blobject.Core;
    using System.Runtime.CompilerServices;

    /// <inheritdoc />
    public class AmazonS3LiteBlobClient : BlobClientBase, IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[AmazonS3LiteBlobClient] ";
        private AwsSettings _AwsSettings = null;
        private S3Client _S3Client = null;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="AmazonS3LiteBlobClient"/> class.
        /// </summary>
        /// <param name="awsSettings">Settings for <see cref="AmazonS3LiteBlobClient"/>.</param>
        public AmazonS3LiteBlobClient(AwsSettings awsSettings)
        {
            if (awsSettings == null) throw new ArgumentNullException(nameof(awsSettings));

            _AwsSettings = awsSettings;

            if (String.IsNullOrEmpty(_AwsSettings.Endpoint))
            {
                _S3Client = new S3Client()
                      .WithRegion(_AwsSettings.Region)
                      .WithRequestStyle(_AwsSettings.RequestStyle)
                      .WithSignatureVersion(SignatureVersionEnum.Version4);

                if (_AwsSettings.HasCredentials)
                {
                    _S3Client
                        .WithAccessKey(_AwsSettings.AccessKey)
                        .WithSecretKey(_AwsSettings.SecretKey);
                }
            }
            else
            {
                Uri uri = new Uri(_AwsSettings.Endpoint);

                ProtocolEnum proto = ProtocolEnum.Http;
                if (_AwsSettings.Endpoint.StartsWith("https://")) proto = ProtocolEnum.Https;

                _S3Client = new S3Client()
                      .WithRegion(_AwsSettings.Region)
                      .WithHostname(uri.Host)
                      .WithPort(uri.Port)
                      .WithProtocol(proto)
                      .WithRequestStyle(_AwsSettings.RequestStyle)
                      .WithSignatureVersion(SignatureVersionEnum.Version4);

                if (_AwsSettings.HasCredentials)
                {
                    _S3Client
                        .WithAccessKey(_AwsSettings.AccessKey)
                        .WithSecretKey(_AwsSettings.SecretKey);
                }
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
            ListAllMyBucketsResult result = await _S3Client.Service.ListBucketsAsync(null, token).ConfigureAwait(false);
            List<string> ret = new List<string>();

            if (result != null && result.Buckets != null && result.Buckets.BucketList != null)
            {
                foreach (Bucket bucket in result.Buckets.BucketList)
                {
                    ret.Add(bucket.Name);
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public override async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return await _S3Client.Object.GetAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            ObjectMetadata md = await _S3Client.Object.GetMetadataAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
            if (md == null)
                throw new KeyNotFoundException("The requested object was not found.");

            return new BlobMetadata
            {
                Key = md.Key,
                ETag = md.ETag,
                ContentLength = md.Size,
                ContentType = md.ContentType,
                CreatedUtc = md.LastModified,
                LastUpdateUtc = md.LastModified,
                LastAccessUtc = md.LastModified
            };
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
            await _S3Client.Object.WriteAsync(_AwsSettings.Bucket, key, data, contentType, null, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            byte[] bytes = Common.ReadStreamFully(stream);
            await WriteAsync(key, contentType, bytes, token).ConfigureAwait(false);
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
            await _S3Client.Object.DeleteAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return await _S3Client.Object.ExistsAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
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

            string continuationToken = "";

            while (true)
            {
                ListBucketResult lbr = _S3Client.Bucket.ListAsync(_AwsSettings.Bucket, filter.Prefix, null, continuationToken, 1000, null).Result;

                foreach (ObjectMetadata curr in lbr.Contents)
                {
                    if (curr.Size < filter.MinimumSize || curr.Size > filter.MaximumSize) continue;
                    if (!String.IsNullOrEmpty(filter.Suffix) && !curr.Key.EndsWith(filter.Suffix)) continue;

                    BlobMetadata md = new BlobMetadata
                    {
                        Key = curr.Key,
                        ContentLength = curr.Size,
                        ETag = curr.ETag,
                        CreatedUtc = curr.LastModified,
                        LastAccessUtc = curr.LastModified,
                        LastUpdateUtc = curr.LastModified
                    };

                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    yield return md;
                }

                continuationToken = lbr.NextContinuationToken;

                if (String.IsNullOrEmpty(continuationToken)) break;
            }

            yield break;
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            string continuationToken = "";

            while (true)
            {
                if (token.IsCancellationRequested) break;

                ListBucketResult lbr = await _S3Client.Bucket.ListAsync(
                    _AwsSettings.Bucket, 
                    filter.Prefix, null, 
                    continuationToken, 
                    1000, 
                    null).ConfigureAwait(false);

                foreach (ObjectMetadata curr in lbr.Contents)
                {
                    if (token.IsCancellationRequested) break;
                    if (curr.Size < filter.MinimumSize || curr.Size > filter.MaximumSize) continue;
                    if (!String.IsNullOrEmpty(filter.Suffix) && !curr.Key.EndsWith(filter.Suffix)) continue;

                    BlobMetadata md = new BlobMetadata
                    {
                        Key = curr.Key,
                        ContentLength = curr.Size,
                        ETag = curr.ETag,
                        CreatedUtc = curr.LastModified,
                        LastAccessUtc = curr.LastModified,
                        LastUpdateUtc = curr.LastModified
                    };

                    if (!String.IsNullOrEmpty(md.ETag))
                    {
                        while (md.ETag.Contains("\"")) md.ETag = md.ETag.Replace("\"", "");
                    }

                    yield return md;
                }

                continuationToken = lbr.NextContinuationToken;

                if (String.IsNullOrEmpty(continuationToken)) break;
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

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}