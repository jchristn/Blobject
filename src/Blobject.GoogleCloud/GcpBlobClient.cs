namespace Blobject.GoogleCloud
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using Google.Apis.Auth.OAuth2;
    using Google.Cloud.Storage.V1;
    using Google.Api.Gax;
    using Google.Api.Gax.Rest;
    using Object = Google.Apis.Storage.v1.Data.Object;

    /// <inheritdoc />
    public class GcpBlobClient : BlobClientBase, IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[GcpBlobClient] ";
        private bool _Disposed = false;
        private GcpBlobSettings _Settings = null;
        private StorageClient _StorageClient = null;
        private GoogleCredential _Credential = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Google Cloud Storage BLOB client.
        /// </summary>
        /// <param name="googleSettings">Settings.</param>
        public GcpBlobClient(GcpBlobSettings googleSettings)
        {
            if (googleSettings == null) throw new ArgumentNullException(nameof(googleSettings));

            _Settings = googleSettings;

            // Create credential from JSON string
            _Credential = GoogleCredential.FromServiceAccountCredential(CredentialFactory.FromJson<ServiceAccountCredential>(_Settings.JsonCredentials));

            // Build storage client
            StorageClientBuilder builder = new StorageClientBuilder
            {
                Credential = _Credential
            };

            if (!String.IsNullOrEmpty(_Settings.CustomEndpoint))
            {
                builder.BaseUri = _Settings.CustomEndpoint;
            }

            _StorageClient = builder.Build();
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
                _Settings = null;
                _StorageClient?.Dispose();
                _StorageClient = null;
                _Credential = null;
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
        /// List buckets available in the project.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of bucket names.</returns>
        public Task<List<string>> ListBuckets(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationListContainers, null, async () =>
            {
                List<string> ret = new List<string>();

                PagedAsyncEnumerable<Google.Apis.Storage.v1.Data.Buckets, Google.Apis.Storage.v1.Data.Bucket> buckets = _StorageClient.ListBucketsAsync(_Settings.ProjectId);
                await foreach (Google.Apis.Storage.v1.Data.Bucket bucket in buckets)
                {
                    if (token.IsCancellationRequested) break;
                    ret.Add(bucket.Name);
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

                using (MemoryStream ms = new MemoryStream())
                {
                    await _StorageClient.DownloadObjectAsync(_Settings.Bucket, key, ms, null, token).ConfigureAwait(false);
                    byte[] data = ms.ToArray();
                    SetTelemetryBytes(data.Length);
                    return data;
                }
            });
        }

        /// <inheritdoc />
        public override Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetStream, key, async () =>
            {
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

                BlobMetadata md = await GetMetadataInternalAsync(key, token).ConfigureAwait(false);

                MemoryStream ms = new MemoryStream();
                await _StorageClient.DownloadObjectAsync(_Settings.Bucket, key, ms, null, token).ConfigureAwait(false);
                ms.Seek(0, SeekOrigin.Begin);

                BlobData bd = new BlobData(md.ContentLength, ms);
                SetTelemetryBytes(md.ContentLength);
                return bd;
            });
        }

        /// <inheritdoc />
        public override Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetMetadata, key, () => GetMetadataInternalAsync(key, token));
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) data = "";
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
            {
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
                if (data == null) data = Array.Empty<byte>();

                using (MemoryStream ms = new MemoryStream(data))
                {
                    await _StorageClient.UploadObjectAsync(_Settings.Bucket, key, contentType, ms, null, token).ConfigureAwait(false);
                }

                SetTelemetryBytes(data.Length);
            });
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
            {
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
                if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
                if (stream == null && contentLength > 0) throw new ArgumentNullException(nameof(stream));
                if (stream == null) stream = new MemoryStream(Array.Empty<byte>());
                if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
                if (stream.CanSeek && stream.Length == stream.Position) stream.Seek(0, SeekOrigin.Begin);

                // For large uploads, use resumable upload
                UploadObjectOptions uploadOptions = new UploadObjectOptions();

                // upload at most contentLength bytes; the stream may hold more
                using (LengthLimitedReadStream source = new LengthLimitedReadStream(stream, contentLength))
                {
                    await _StorageClient.UploadObjectAsync(_Settings.Bucket, key, contentType, source, uploadOptions, token).ConfigureAwait(false);
                }

                SetTelemetryBytes(contentLength);
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
                if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

                try
                {
                    await _StorageClient.DeleteObjectAsync(_Settings.Bucket, key, null, token).ConfigureAwait(false);
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Object doesn't exist, consider this a successful deletion
                }
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
                try
                {
                    await _StorageClient.GetObjectAsync(_Settings.Bucket, key, null, token).ConfigureAwait(false);
                    return true;
                }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            return $"https://storage.googleapis.com/{_Settings.Bucket}/{key}";
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
                return BlobjectTelemetryNames.ProviderGoogleCloud;
            }
        }

        /// <inheritdoc />
        protected override string TelemetryContainer
        {
            get
            {
                GcpBlobSettings settings = _Settings;
                return settings != null ? settings.Bucket : null;
            }
        }

        /// <inheritdoc />
        protected override string TelemetryServerAddress
        {
            get
            {
                GcpBlobSettings settings = _Settings;
                if (settings == null || String.IsNullOrEmpty(settings.CustomEndpoint)) return "storage.googleapis.com";

                Uri uri;
                if (Uri.TryCreate(settings.CustomEndpoint, UriKind.Absolute, out uri)) return uri.Host;
                return null;
            }
        }

        /// <inheritdoc />
        protected override bool IsTelemetryNotFound(Exception e)
        {
            if (base.IsTelemetryNotFound(e)) return true;
            Google.GoogleApiException gae = e as Google.GoogleApiException;
            return gae != null && gae.HttpStatusCode == System.Net.HttpStatusCode.NotFound;
        }

        #endregion

        #region Private-Methods

        private async Task<BlobMetadata> GetMetadataInternalAsync(string key, CancellationToken token)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            Object obj = await _StorageClient.GetObjectAsync(_Settings.Bucket, key, null, token).ConfigureAwait(false);
            return BuildMetadata(obj);
        }

        private IEnumerable<BlobMetadata> EnumerateInternal(EnumerationFilter filter)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            ListObjectsOptions listOptions = new ListObjectsOptions
            {
                PageSize = 1000
            };

            foreach (Object obj in _StorageClient.ListObjects(_Settings.Bucket, filter.Prefix, listOptions))
            {
                BlobMetadata md = BuildMetadata(obj);
                if (!MatchesFilter(md, filter, StringComparison.Ordinal)) continue;
                yield return md;
            }
        }

        private async IAsyncEnumerable<BlobMetadata> EnumerateInternalAsync(
            EnumerationFilter filter,
            [EnumeratorCancellation] CancellationToken token)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            ListObjectsOptions listOptions = new ListObjectsOptions
            {
                PageSize = 1000
            };

            await foreach (Object obj in _StorageClient.ListObjectsAsync(_Settings.Bucket, filter.Prefix, listOptions))
            {
                if (token.IsCancellationRequested) break;

                BlobMetadata md = BuildMetadata(obj);
                if (!MatchesFilter(md, filter, StringComparison.Ordinal)) continue;
                yield return md;
            }
        }

        private static BlobMetadata BuildMetadata(Object obj)
        {
            BlobMetadata md = new BlobMetadata();
            md.Key = obj.Name;
            md.ETag = obj.ETag;
            md.ContentLength = (long)(obj.Size ?? 0);
            md.ContentType = obj.ContentType;
            md.CreatedUtc = obj.TimeCreatedDateTimeOffset?.UtcDateTime;
            md.LastUpdateUtc = obj.UpdatedDateTimeOffset?.UtcDateTime;
            md.LastAccessUtc = obj.UpdatedDateTimeOffset?.UtcDateTime; // GCS doesn't track last access separately
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
