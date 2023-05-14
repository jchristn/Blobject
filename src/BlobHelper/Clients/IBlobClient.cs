using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BlobHelper
{
    /// <summary>
    /// An interface for interacting with different BLOB storage providers.
    /// </summary>
    public interface IBlobClient
    {
        /// <summary>
        /// Gets the content of the BLOB with the specified key.
        /// </summary>
        /// <param name="key">The key of the BLOB to get.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A byte array containing the content of the BLOB.</returns>
        Task<byte[]> GetAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets the stream of the BLOB with the specified key.
        /// </summary>
        /// <param name="key">The key of the BLOB to get.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobData"/> object containing the stream of the BLOB.</returns>
        Task<BlobData> GetStreamAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets the metadata of the BLOB with the specified key.
        /// </summary>
        /// <param name="key">The key of the BLOB to get metadata for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobMetadata"/> object containing the metadata of the BLOB.</returns>
        Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Writes the specified data to the BLOB with the specified key.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="data">The data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        Task WriteAsync(string key, string contentType, string data, CancellationToken token = default);

        /// <summary>
        /// Writes the specified data to the BLOB with the specified key.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="data">The data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default);

        /// <summary>
        /// Writes the data from the specified stream to the BLOB with the specified key.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="contentLength">The length of the content in the stream.</param>
        /// <param name="stream">The stream containing the data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        Task WriteAsync(string key, string contentType, long contentLength, Stream stream,
            CancellationToken token = default);

        /// <summary>
        /// Writes many objects to the BLOB storage asynchronously.
        /// </summary>
        /// <param name="objects">The list of objects to write to the BLOB storage.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default);

        /// <summary>
        /// Deletes an object from the BLOB storage asynchronously.
        /// </summary>
        /// <param name="key">The key of the object to delete from the BLOB storage.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Checks if an object with the specified key exists in the BLOB storage asynchronously.
        /// </summary>
        /// <param name="key">The key of the object to check.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation. The task result is true if the object exists; otherwise, false.</returns>
        public Task<bool> ExistsAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Generates a URL to access the object with the specified key in the BLOB storage asynchronously.
        /// </summary>
        /// <param name="key">The key of the object to generate the URL for.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A string representing the URL to access the object.</returns>
        string GenerateUrl(string key, CancellationToken token = default);

        /// <summary>
        /// Enumerates all objects in the BLOB storage asynchronously.
        /// </summary>
        /// <param name="prefix">The prefix used to filter the objects to enumerate.</param>
        /// <param name="continuationToken">The continuation token used to resume a previous enumeration operation.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation. The task result is an <see cref="EnumerationResult"/> object containing the enumeration results.</returns>
        Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null,
            CancellationToken token = default);

        /// <summary>
        /// Deletes all objects in the BLOB storage asynchronously.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task<EmptyResult> EmptyAsync(CancellationToken token = default);
    }
}