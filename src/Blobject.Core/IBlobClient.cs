namespace Blobject.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An interface for interacting with different BLOB storage providers.
    /// </summary>
    public abstract class BlobClientBase
    {
        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        /// <summary>
        /// Buffer size to use when reading from a stream.  Default is 65536.
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return _StreamBufferSize;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                _StreamBufferSize = value;
            }
        }

        #endregion

        #region Private-Members

        private int _StreamBufferSize = 65536;

        #endregion

        #region Constructors-and-Factories

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate connectivity to the repository.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if connectivity can be established.</returns>
        public abstract Task<bool> ValidateConnectivity(CancellationToken token = default);

        /// <summary>
        /// Gets the content of the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the BLOB to get.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A byte array containing the content of the BLOB.</returns>
        public abstract Task<byte[]> GetAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets the stream of the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the BLOB to get.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobData"/> object containing the stream of the BLOB.</returns>
        public abstract Task<BlobData> GetStreamAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Gets the metadata of the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the BLOB to get metadata for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="BlobMetadata"/> object containing the metadata of the BLOB.</returns>
        public abstract Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Writes the specified data to the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="data">The data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public abstract Task WriteAsync(string key, string contentType, string data, CancellationToken token = default);

        /// <summary>
        /// Writes the specified data to the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="data">The data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public abstract Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default);

        /// <summary>
        /// Writes the data from the specified stream to the BLOB with the specified key.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="key">The key of the BLOB to write to.</param>
        /// <param name="contentType">The content type of the BLOB.</param>
        /// <param name="contentLength">The length of the content in the stream.</param>
        /// <param name="stream">The stream containing the data to write to the BLOB.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        public abstract Task WriteAsync(string key, string contentType, long contentLength, Stream stream,
            CancellationToken token = default);

        /// <summary>
        /// Writes many objects to the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// To create a folder, have the key end in the / character, and send an empty string, an empty byte array, or an empty stream with zero content length.
        /// </summary>
        /// <param name="objects">The list of objects to write to the BLOB storage.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default);

        /// <summary>
        /// Deletes an object from the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// For file storage platforms, when deleting a folder, use / at the end of the key.
        /// </summary>
        /// <param name="key">The key of the object to delete from the BLOB storage.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task DeleteAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Checks if an object with the specified key exists in the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.  For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the object to check.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation. The task result is true if the object exists; otherwise, false.</returns>
        public abstract Task<bool> ExistsAsync(string key, CancellationToken token = default);

        /// <summary>
        /// Generates a URL to access the object with the specified key in the BLOB storage asynchronously.
        /// For objects contained within subdirectories or folders, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="key">The key of the object to generate the URL for.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A string representing the URL to access the object.</returns>
        public abstract string GenerateUrl(string key, CancellationToken token = default);

        /// <summary>
        /// Enumerate all BLOBs within the repository asynchronously.
        /// To enumerate only a specific prefix or contents of a specific folder, use the / character.
        /// For example, path/to/folder/myfile.txt
        /// </summary>
        /// <param name="filter">Enumeration filter.</param>
        /// <returns>IEnumerable of BlobMetadata.</returns>
        public abstract IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null);

        /// <summary>
        /// WARNING: This API deletes all objects in the BLOB storage asynchronously recursively.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public abstract Task<EmptyResult> EmptyAsync(CancellationToken token = default);

        #endregion

        #region Private-Methods

        #endregion
    }
}