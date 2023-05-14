using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlobHelper
{
    /// <summary>
    /// BLOB storage client.
    /// </summary>
    public class BlobClient
    {
        #region Public-Members

        #endregion

        #region Private-Members

        private readonly IBlobClient _BlobClient = null;

        #endregion

        #region Constructors-and-Factories

        private BlobClient()
        {
            throw new NotImplementedException("Use a StorageType specific constructor.");
        }

        /// <summary>
        /// Instantiate the object for Azure BLOB strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public BlobClient(AzureSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _BlobClient = new AzureBlobClient(config);
        }

        /// <summary>
        /// Instantiate the object for AWS S3 strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public BlobClient(AwsSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _BlobClient = new AwsS3BlobClient(config);
        }

        /// <summary>
        /// Instantiate the object for disk strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public BlobClient(DiskSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _BlobClient = new DiskBlobClient(config);
        }

        /// <summary>
        /// Instantiate the object for Kvpbase strorage.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public BlobClient(KvpbaseSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _BlobClient = new KvpbaseBlobClient(config);
        }

        /// <summary>
        /// Instantiate the object for a Komodo index.
        /// </summary>
        /// <param name="config">Storage configuration.</param>
        public BlobClient(KomodoSettings config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            _BlobClient = new KomodoBlobClient(config);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Delete a BLOB by its key.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>True if successful.</returns>
        public Task Delete(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                return _BlobClient.DeleteAsync(key, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve a BLOB.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>Byte data of the BLOB.</returns>
        public Task<byte[]> Get(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                return _BlobClient.GetAsync(key, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Retrieve a BLOB.  Be sure to dispose of the stream.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>BLOB data.</returns>
        public Task<BlobData> GetStream(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                return _BlobClient.GetStreamAsync(key, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Write a BLOB using a string.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content-type of the object.</param>
        /// <param name="data">BLOB data.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns></returns>
        public Task Write(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return Write(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <summary>
        /// Write a BLOB using a byte array.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content-type of the object.</param>
        /// <param name="data">BLOB data.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        public Task Write(
            string key,
            string contentType,
            byte[] data,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                return _BlobClient.WriteAsync(key, contentType, data, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Write a BLOB using a stream.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="contentType">Content type.</param>
        /// <param name="contentLength">Content length.</param>
        /// <param name="stream">Stream containing the data.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        public Task Write(
            string key,
            string contentType,
            long contentLength,
            Stream stream,
            CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentException("Content length must be zero or greater.");
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new IOException("Cannot read from supplied stream.");

            if (stream.CanSeek && stream.Length == stream.Position) stream.Seek(0, SeekOrigin.Begin);

            try
            {
                return _BlobClient.WriteAsync(key, contentType, contentLength, stream, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a BLOB exists.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>True if exists.</returns>
        public Task<bool> Exists(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                return _BlobClient.ExistsAsync(key, token);
            }
            catch (TaskCanceledException)
            {
                return Task.FromResult(false);
            }
            catch (OperationCanceledException)
            {
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Generate a URL for a given object key.
        /// </summary>
        /// <param name="key">Object key.</param>
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>URL.</returns>
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            return _BlobClient.GenerateUrl(key, token);
        }

        /// <summary>
        /// Retrieve BLOB metadata.
        /// </summary>
        /// <param name="key">Key of the BLOB.</param> 
        /// <param name="token">Cancellation token to cancel the request.</param>
        /// <returns>BLOB metadata.</returns>
        public Task<BlobMetadata> GetMetadata(string key, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            try
            {
                return _BlobClient.GetMetadataAsync(key, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Enumerate BLOBs.
        /// </summary>
        /// <param name="prefix">Key prefix that must match.</param>
        /// <param name="continuationToken">Continuation token to use if issuing a subsequent enumeration request.</param>
        /// <param name="token">Cancellation token to cancel the request.</param> 
        /// <returns>Enumeration result.</returns>
        public Task<EnumerationResult> Enumerate(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            try
            {
                return _BlobClient.EnumerateAsync(prefix, continuationToken, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Empty all BLOBs from the container.  Note: this is a destructive operation!
        /// </summary>
        /// <param name="token">Cancellation token to cancel the request.</param> 
        /// <returns>Empty result.</returns>
        public Task<EmptyResult> Empty(CancellationToken token = default)
        {
            try
            {
                return _BlobClient.EmptyAsync(token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Write multiple BLOBs.
        /// </summary>
        /// <param name="objects">Objects to write.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public Task WriteMany(List<WriteRequest> objects, CancellationToken token = default)
        {
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            if (objects.Count < 1) return Task.CompletedTask;

            try
            {
                return _BlobClient.WriteManyAsync(objects, token);
            }
            catch (TaskCanceledException)
            {
                return null;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        #endregion
    }
}