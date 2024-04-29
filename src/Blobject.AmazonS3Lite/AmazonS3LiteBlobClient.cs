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

    /// <inheritdoc />
    public class AmazonS3LiteBlobClient : IBlobClient, IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger
        {
            get
            {
                return _S3Client.Logger;
            }
            set
            {
                _S3Client.Logger = value;
            }
        }

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
            _AwsSettings = awsSettings;

            if (String.IsNullOrEmpty(_AwsSettings.Endpoint))
            {
                _S3Client = new S3Client()
                      .WithRegion(_AwsSettings.Region)
                      .WithAccessKey(_AwsSettings.AccessKey)
                      .WithSecretKey(_AwsSettings.SecretKey)
                      .WithRequestStyle(_AwsSettings.RequestStyle)
                      .WithSignatureVersion(SignatureVersionEnum.Version4);
            }
            else
            {
                Uri uri = new Uri(_AwsSettings.Endpoint);

                ProtocolEnum proto = ProtocolEnum.Http;
                if (_AwsSettings.Endpoint.StartsWith("https://")) proto = ProtocolEnum.Https;

                _S3Client = new S3Client()
                      .WithRegion(_AwsSettings.Region)
                      .WithAccessKey(_AwsSettings.AccessKey)
                      .WithSecretKey(_AwsSettings.SecretKey)
                      .WithHostname(uri.Host)
                      .WithPort(uri.Port)
                      .WithProtocol(proto)
                      .WithRequestStyle(_AwsSettings.RequestStyle)
                      .WithSignatureVersion(SignatureVersionEnum.Version4);
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
            return await _S3Client.Object.GetAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
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
        public Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            await _S3Client.Object.WriteAsync(_AwsSettings.Bucket, key, data, contentType, null, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            byte[] bytes = Common.ReadStreamFully(stream);
            await WriteAsync(key, contentType, bytes, token).ConfigureAwait(false);
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
            await _S3Client.Object.DeleteAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return await _S3Client.Object.ExistsAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
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

                ret += _AwsSettings.Bucket + ".s3." + _AwsSettings.Region + ".amazonaws.com/" + key;

                return ret;
            }
        }

        /// <inheritdoc />
        public async Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            Log("enumerating using prefix " + prefix);

            ListBucketResult lbr = await _S3Client.Bucket.ListAsync(_AwsSettings.Bucket, prefix, null, continuationToken, 1000, null, token).ConfigureAwait(false);

            EnumerationResult ret = new EnumerationResult();

            foreach (ObjectMetadata curr in lbr.Contents)
            {
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

                ret.Blobs.Add(md);
            }

            if (!String.IsNullOrEmpty(lbr.NextContinuationToken)) ret.NextContinuationToken = lbr.NextContinuationToken;

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

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}