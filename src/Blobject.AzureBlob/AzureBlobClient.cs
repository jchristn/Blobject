namespace Blobject.AzureBlob
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Azure;
    using Azure.Storage.Blobs;
    using Azure.Storage.Blobs.Models;
    using Blobject.Core;

    /// <inheritdoc />
    public class AzureBlobClient : BlobClientBase, IDisposable
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[AzureBlobClient] ";
        private int _StreamBufferSize = 65536;
        private bool _Disposed = false;
        private AzureBlobSettings _Settings = null;
        private string _ConnectionString = null;
        private BlobServiceClient _ServiceClient = null;
        private BlobContainerClient _ContainerClient = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobClient"/> class.
        /// </summary>
        /// <param name="azureSettings">Settings for <see cref="AzureBlobClient"/>.</param>
        public AzureBlobClient(AzureBlobSettings azureSettings)
        {
            if (azureSettings == null) throw new ArgumentNullException(nameof(azureSettings));

            _Settings = azureSettings;
            _ConnectionString = GetAzureConnectionString();
            _ServiceClient = new BlobServiceClient(_ConnectionString);
            _ContainerClient = new BlobContainerClient(_ConnectionString, _Settings.Container);

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
                _ConnectionString = null;
                _ContainerClient = null;
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
                List<string> containers = await ListContainers(token).ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// List containers available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of container names.</returns>
        public async Task<List<string>> ListContainers(CancellationToken token = default)
        {
            List<string> ret = new List<string>();
            string prefix = null;

            var resultSegment =
                _ServiceClient.GetBlobContainersAsync(BlobContainerTraits.Metadata, BlobContainerStates.None, prefix, token)
                .AsPages(null, null)
                .ConfigureAwait(false);

            await foreach (Azure.Page<BlobContainerItem> containerPage in resultSegment)
            {
                foreach (BlobContainerItem containerItem in containerPage.Values)
                {
                    ret.Add(containerItem.Name);
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public override async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_ConnectionString, _Settings.Container, key);
            byte[] buff = new byte[_StreamBufferSize];
            byte[] ret = null;

            int totalRead = 0;

            using (Stream str = await bc.OpenReadAsync(new BlobOpenReadOptions(false), token).ConfigureAwait(false))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        int read = await str.ReadAsync(buff, 0, buff.Length, token).ConfigureAwait(false);
                        if (read > 0)
                        {
                            await ms.WriteAsync(buff, 0, read, token).ConfigureAwait(false);
                            totalRead += read;
                        }
                        else break;
                    }

                    ret = ms.ToArray();
                }
            }

            return ret;
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_ConnectionString, _Settings.Container, key);
            BlobMetadata md = await GetMetadataAsync(key, token).ConfigureAwait(false);
            BlobData bd = new BlobData(md.ContentLength, await bc.OpenReadAsync(new BlobOpenReadOptions(false), token).ConfigureAwait(false));
            return bd;
        }

        /// <inheritdoc />
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_ConnectionString, _Settings.Container, key);
            BlobProperties bp = await bc.GetPropertiesAsync(null, token).ConfigureAwait(false);
            BlobMetadata md = new BlobMetadata();
            md.ETag = bp.ETag.ToString();
            md.ContentLength = bp.ContentLength;
            md.CreatedUtc = bp.CreatedOn.DateTime;
            md.LastUpdateUtc = bp.LastModified.DateTime;
            md.LastAccessUtc = bp.LastAccessed.DateTime;
            md.ContentType = bp.ContentType;
            md.Key = key;
            return md;
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
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_ConnectionString, _Settings.Container, key);

            using (Stream str = await bc.OpenWriteAsync(true, null, token).ConfigureAwait(false))
            {
                await str.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            }

            await bc.SetHttpHeadersAsync(new BlobHttpHeaders() { ContentType = contentType }, null, token).ConfigureAwait(false);
            return;
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (stream.CanSeek && stream.Length == stream.Position) stream.Seek(0, SeekOrigin.Begin);

            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_ConnectionString, _Settings.Container, key);
            byte[] buff = new byte[_StreamBufferSize];
            int read = 0;
            long bytesRemaining = contentLength;

            using (Stream str = await bc.OpenWriteAsync(true, null, token).ConfigureAwait(false))
            {
                while (bytesRemaining > 0)
                {
                    if (bytesRemaining >= _StreamBufferSize)
                    {
                        read = await stream.ReadAsync(buff, 0, _StreamBufferSize, token).ConfigureAwait(false);
                    }
                    else
                    {
                        read = await stream.ReadAsync(buff, 0, (int)bytesRemaining, token).ConfigureAwait(false);
                    }

                    if (read > 0)
                    {
                        await str.WriteAsync(buff, 0, read, token).ConfigureAwait(false);
                        bytesRemaining -= read;
                    }
                }
            }

            await bc.SetHttpHeadersAsync(new BlobHttpHeaders() { ContentType = contentType }, null, token).ConfigureAwait(false);
            return;
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
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_ConnectionString, _Settings.Container, key);
            await bc.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            try
            {
                BlobMetadata md = await GetMetadataAsync(key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            return "https://" +
                _Settings.AccountName +
                ".blob.core.windows.net/" +
                _Settings.Container +
                "/" +
                key;
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
                var pages = _ContainerClient.GetBlobs(BlobTraits.None, BlobStates.None, filter.Prefix).AsPages(continuationToken, 1000);

                foreach (Page<BlobItem> page in pages)
                {
                    continuationToken = page.ContinuationToken;

                    if (page.Values == null || page.Values.Count < 1) break;

                    foreach (BlobItem item in page.Values)
                    {
                        long contentLength = (item.Properties.ContentLength != null ? Convert.ToInt64(item.Properties.ContentLength) : 0);

                        if (contentLength < filter.MinimumSize || contentLength > filter.MaximumSize) continue;
                        if (!String.IsNullOrEmpty(filter.Suffix) && !item.Name.EndsWith(filter.Suffix)) continue;

                        BlobMetadata md = new BlobMetadata();
                        md.Key = item.Name;
                        md.ContentType = item.Properties.ContentType;
                        md.ContentLength = contentLength;
                        md.ETag = item.Properties.ETag.ToString();
                        md.CreatedUtc = item.Properties.CreatedOn != null ? item.Properties.CreatedOn.Value.DateTime.ToUniversalTime() : DateTime.UtcNow;
                        md.LastUpdateUtc = item.Properties.LastModified != null ? item.Properties.LastModified.Value.DateTime.ToUniversalTime() : DateTime.UtcNow;
                        md.LastAccessUtc = item.Properties.LastAccessedOn != null ? item.Properties.LastAccessedOn.Value.DateTime.ToUniversalTime() : DateTime.UtcNow;

                        yield return md;
                    }

                    if (String.IsNullOrEmpty(continuationToken)) break;
                }

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

        private string GetAzureConnectionString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("DefaultEndpointsProtocol=" + (_Settings.Ssl ? "https" : "http") + ";");
            sb.Append("AccountName=" + _Settings.AccountName + ";");
            sb.Append("AccountKey=" + _Settings.AccessKey + ";");
            sb.Append("BlobEndpoint=" + _Settings.Endpoint);
            return sb.ToString();
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion
    }
}