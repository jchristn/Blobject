using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace BlobHelper
{
    /// <inheritdoc />
    public class AzureBlobClient : IBlobClient
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly AzureSettings _AzureSettings;
        private readonly string _AzureConnectionString;
        private readonly BlobContainerClient _AzureContainerClient;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureBlobClient"/> class.
        /// </summary>
        /// <param name="azureSettings">Settings for <see cref="AzureBlobClient"/>.</param>
        public AzureBlobClient(AzureSettings azureSettings)
        {
            _AzureSettings = azureSettings;
            _AzureConnectionString = GetAzureConnectionString();
            _AzureContainerClient = new BlobContainerClient(_AzureConnectionString, _AzureSettings.Container);

        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_AzureConnectionString, _AzureSettings.Container, key);
            byte[] buff = new byte[4096];
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
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_AzureConnectionString, _AzureSettings.Container, key);
            byte[] buff = new byte[4096];

            BlobMetadata md = await GetMetadataAsync(key, token).ConfigureAwait(false);
            BlobData bd = new BlobData(md.ContentLength, await bc.OpenReadAsync(new BlobOpenReadOptions(false), token).ConfigureAwait(false));
            return bd;
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_AzureConnectionString, _AzureSettings.Container, key);
            byte[] buff = new byte[4096];

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
        public Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_AzureConnectionString, _AzureSettings.Container, key);

            using (Stream str = await bc.OpenWriteAsync(true, null, token).ConfigureAwait(false))
            {
                await str.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            }

            await bc.SetHttpHeadersAsync(new BlobHttpHeaders() { ContentType = contentType }, null, token).ConfigureAwait(false);
            return;
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");
            if (stream.CanSeek && stream.Length == stream.Position) stream.Seek(0, SeekOrigin.Begin);

            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_AzureConnectionString, _AzureSettings.Container, key);
            byte[] buff = new byte[4096];

            using (Stream str = await bc.OpenWriteAsync(true, null, token).ConfigureAwait(false))
            {
                int read = 0;

                while (true)
                {
                    read = await stream.ReadAsync(buff, 0, buff.Length, token).ConfigureAwait(false);
                    if (read > 0)
                    {
                        await str.WriteAsync(buff, 0, read, token).ConfigureAwait(false);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            await bc.SetHttpHeadersAsync(new BlobHttpHeaders() { ContentType = contentType }, null, token).ConfigureAwait(false);
            return;
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
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            Azure.Storage.Blobs.BlobClient bc = new Azure.Storage.Blobs.BlobClient(_AzureConnectionString, _AzureSettings.Container, key);
            await bc.DeleteIfExistsAsync(DeleteSnapshotsOption.None, null, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
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
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            return "https://" +
                   _AzureSettings.AccountName +
                   ".blob.core.windows.net/" +
                   _AzureSettings.Container +
                   "/" +
                   key;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            List<BlobMetadata> mds = new List<BlobMetadata>();

            var pages = _AzureContainerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, token).AsPages(continuationToken, 5000).ConfigureAwait(false);

            await foreach (var page in pages)
            {
                continuationToken = page.ContinuationToken;

                foreach (BlobItem item in page.Values)
                {
                    BlobMetadata md = new BlobMetadata();
                    md.ETag = item.Properties.ETag.ToString();
                    md.ContentLength = (item.Properties.ContentLength != null ? Convert.ToInt64(item.Properties.ContentLength) : 0);
                    md.CreatedUtc = item.Properties.CreatedOn != null ? item.Properties.CreatedOn.Value.DateTime : DateTime.UtcNow;
                    md.LastUpdateUtc = item.Properties.LastModified != null ? item.Properties.LastModified.Value.DateTime : DateTime.UtcNow;
                    md.LastAccessUtc = item.Properties.LastAccessedOn != null ? item.Properties.LastAccessedOn.Value.DateTime : DateTime.UtcNow;
                    md.ContentType = item.Properties.ContentType;
                    md.Key = item.Name;
                    mds.Add(md);
                }

                break;
            }

            EnumerationResult er = new EnumerationResult(continuationToken, mds);

            return er;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            string continuationToken = null;

            while (true)
            {
                EnumerationResult result = await EnumerateAsync(null, null, token).ConfigureAwait(false);
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

        private string GetAzureConnectionString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("DefaultEndpointsProtocol=" + (_AzureSettings.Ssl ? "https" : "http") + ";");
            sb.Append("AccountName=" + _AzureSettings.AccountName + ";");
            sb.Append("AccountKey=" + _AzureSettings.AccessKey + ";");
            sb.Append("BlobEndpoint=" + _AzureSettings.Endpoint);
            return sb.ToString();
        }

        #endregion
    }
}