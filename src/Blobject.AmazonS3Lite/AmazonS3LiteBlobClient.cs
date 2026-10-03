namespace Blobject.AmazonS3Lite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using S3Lite;
    using S3Lite.ApiObjects;

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
        public override Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationValidateConnectivity, null, async () =>
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
            });
        }

        /// <summary>
        /// List buckets available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of bucket names.</returns>
        public Task<List<string>> ListBuckets(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationListContainers, null, async () =>
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
            });
        }

        /// <inheritdoc />
        public override Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGet, key, async () =>
            {
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
                byte[] data = await _S3Client.Object.GetAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
                if (data != null) SetTelemetryBytes(data.Length);
                return data;
            });
        }

        /// <inheritdoc />
        public override Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetStream, key, async () =>
            {
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
                byte[] data = await _S3Client.Object.GetAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
                if (data == null) data = Array.Empty<byte>();
                SetTelemetryBytes(data.Length);
                return new BlobData(data.Length, new MemoryStream(data));
            });
        }

        /// <inheritdoc />
        public override Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetMetadata, key, async () =>
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
            });
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (data == null) data = "";
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
            {
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
                if (String.IsNullOrEmpty(contentType)) contentType = "application/octet-stream";
                if (data == null) data = Array.Empty<byte>();

                await _S3Client.Object.WriteAsync(_AwsSettings.Bucket, key, data, contentType, null, null, token).ConfigureAwait(false);
                SetTelemetryBytes(data.Length);
            });
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
            {
                if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
                if (stream == null && contentLength > 0) throw new ArgumentNullException(nameof(stream));

                if (contentLength == 0)
                {
                    await WriteAsync(key, contentType, Array.Empty<byte>(), token).ConfigureAwait(false);
                    return;
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    await CopyStreamAsync(stream, ms, contentLength, token).ConfigureAwait(false);
                    await WriteAsync(key, contentType, ms.ToArray(), token).ConfigureAwait(false);
                }
            });
        }

        /// <inheritdoc />
        public override async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            await base.WriteManyAsync(objects, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task DeleteAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationDelete, key, async () =>
            {
                await _S3Client.Object.DeleteAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
            });
        }

        /// <inheritdoc />
        public override async Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default)
        {
            return await base.DeleteManyAsync(keys, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationExists, key, async () =>
            {
                return await _S3Client.Object.ExistsAsync(_AwsSettings.Bucket, key, null, null, token).ConfigureAwait(false);
            });
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
            return InstrumentEnumerate(() => EnumerateInternal(filter));
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            CancellationToken token = default)
        {
            return InstrumentEnumerateAsync(t => EnumerateInternalAsync(filter, t), token);
        }

        /// <inheritdoc />
        public override async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            return await base.EmptyAsync(token).ConfigureAwait(false);
        }

        #endregion

        #region Protected-Methods

        /// <inheritdoc />
        protected override string TelemetryProvider
        {
            get
            {
                return BlobjectTelemetryNames.ProviderAmazonS3Lite;
            }
        }

        /// <inheritdoc />
        protected override string TelemetryContainer
        {
            get
            {
                AwsSettings settings = _AwsSettings;
                return settings != null ? settings.Bucket : null;
            }
        }

        /// <inheritdoc />
        protected override string TelemetryServerAddress
        {
            get
            {
                AwsSettings settings = _AwsSettings;
                if (settings == null || String.IsNullOrEmpty(settings.Endpoint)) return null;

                Uri uri;
                if (Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out uri)) return uri.Host;
                return null;
            }
        }

        /// <inheritdoc />
        protected override bool IsTelemetryNotFound(Exception e)
        {
            if (base.IsTelemetryNotFound(e)) return true;

            WebException we = e as WebException;
            if (we == null) return false;

            HttpWebResponse response = we.Response as HttpWebResponse;
            if (response != null && response.StatusCode == HttpStatusCode.NotFound) return true;

            string message = we.Message ?? "";
            return message.IndexOf("NoSuchKey", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("404", StringComparison.Ordinal) >= 0;
        }

        #endregion

        #region Private-Methods

        private IEnumerable<BlobMetadata> EnumerateInternal(EnumerationFilter filter)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            string continuationToken = "";

            while (true)
            {
                ListBucketResult lbr = _S3Client.Bucket.ListAsync(_AwsSettings.Bucket, filter.Prefix, null, continuationToken, 1000, null).Result;

                foreach (ObjectMetadata curr in lbr.Contents)
                {
                    BlobMetadata md = BuildMetadata(curr);
                    if (!MatchesFilter(md, filter, StringComparison.Ordinal)) continue;
                    yield return md;
                }

                continuationToken = lbr.NextContinuationToken;

                if (String.IsNullOrEmpty(continuationToken)) break;
            }
        }

        private async IAsyncEnumerable<BlobMetadata> EnumerateInternalAsync(
            EnumerationFilter filter,
            [EnumeratorCancellation] CancellationToken token)
        {
            filter = CloneFilter(filter);
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
                    BlobMetadata md = BuildMetadata(curr);
                    if (!MatchesFilter(md, filter, StringComparison.Ordinal)) continue;
                    yield return md;
                }

                continuationToken = lbr.NextContinuationToken;

                if (String.IsNullOrEmpty(continuationToken)) break;
            }
        }

        private static BlobMetadata BuildMetadata(ObjectMetadata curr)
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

            return md;
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
