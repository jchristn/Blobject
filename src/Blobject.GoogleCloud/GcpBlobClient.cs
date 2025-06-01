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
            _Credential = GoogleCredential.FromJson(_Settings.JsonCredentials);

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
        /// List buckets available in the project.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of bucket names.</returns>
        public async Task<List<string>> ListBuckets(CancellationToken token = default)
        {
            List<string> ret = new List<string>();

            var buckets = _StorageClient.ListBucketsAsync(_Settings.ProjectId);
            await foreach (var bucket in buckets)
            {
                if (token.IsCancellationRequested) break;
                ret.Add(bucket.Name);
            }

            return ret;
        }

        /// <inheritdoc />
        public override async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            using (MemoryStream ms = new MemoryStream())
            {
                await _StorageClient.DownloadObjectAsync(_Settings.Bucket, key, ms, null, token).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            BlobMetadata md = await GetMetadataAsync(key, token).ConfigureAwait(false);

            MemoryStream ms = new MemoryStream();
            await _StorageClient.DownloadObjectAsync(_Settings.Bucket, key, ms, null, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);

            BlobData bd = new BlobData(md.ContentLength, ms);
            return bd;
        }

        /// <inheritdoc />
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            var obj = await _StorageClient.GetObjectAsync(_Settings.Bucket, key, null, token).ConfigureAwait(false);

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

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) data = "";
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (data == null) data = Array.Empty<byte>();

            using (MemoryStream ms = new MemoryStream(data))
            {
                await _StorageClient.UploadObjectAsync(_Settings.Bucket, key, contentType, ms, null, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (stream.CanSeek && stream.Length == stream.Position) stream.Seek(0, SeekOrigin.Begin);

            // For large uploads, use resumable upload
            var uploadOptions = new UploadObjectOptions();

            await _StorageClient.UploadObjectAsync(_Settings.Bucket, key, contentType, stream, uploadOptions, token).ConfigureAwait(false);
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
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                await _StorageClient.DeleteObjectAsync(_Settings.Bucket, key, null, token).ConfigureAwait(false);
            }
            catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Object doesn't exist, consider this a successful deletion
            }
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync(string key, CancellationToken token = default)
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
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            return $"https://storage.googleapis.com/{_Settings.Bucket}/{key}";
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            var listOptions = new ListObjectsOptions
            {
                PageSize = 1000
            };

            foreach (var obj in _StorageClient.ListObjects(_Settings.Bucket, filter.Prefix, listOptions))
            {
                long contentLength = (long)(obj.Size ?? 0);

                if (contentLength < filter.MinimumSize || contentLength > filter.MaximumSize) continue;
                if (!String.IsNullOrEmpty(filter.Suffix) && !obj.Name.EndsWith(filter.Suffix)) continue;

                BlobMetadata md = new BlobMetadata();
                md.Key = obj.Name;
                md.ContentType = obj.ContentType;
                md.ContentLength = contentLength;
                md.ETag = obj.ETag;
                md.CreatedUtc = obj.TimeCreatedDateTimeOffset?.UtcDateTime;
                md.LastUpdateUtc = obj.UpdatedDateTimeOffset?.UtcDateTime;
                md.LastAccessUtc = obj.UpdatedDateTimeOffset?.UtcDateTime;

                yield return md;
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            var listOptions = new ListObjectsOptions
            {
                PageSize = 1000
            };

            var objects = _StorageClient.ListObjectsAsync(_Settings.Bucket, filter.Prefix, listOptions);

            await foreach (var obj in objects)
            {
                if (token.IsCancellationRequested) break;

                long contentLength = (long)(obj.Size ?? 0);

                if (contentLength < filter.MinimumSize || contentLength > filter.MaximumSize) continue;
                if (!String.IsNullOrEmpty(filter.Suffix) && !obj.Name.EndsWith(filter.Suffix)) continue;

                BlobMetadata md = new BlobMetadata();
                md.Key = obj.Name;
                md.ContentType = obj.ContentType;
                md.ContentLength = contentLength;
                md.ETag = obj.ETag;
                md.CreatedUtc = obj.TimeCreatedDateTimeOffset?.UtcDateTime;
                md.LastUpdateUtc = obj.UpdatedDateTimeOffset?.UtcDateTime;
                md.LastAccessUtc = obj.UpdatedDateTimeOffset?.UtcDateTime;

                yield return md;
            }
        }

        /// <inheritdoc />
        public override async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            await foreach (BlobMetadata md in EnumerateAsync(null, token))
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