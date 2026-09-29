namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// Disposable pass-through wrapper around an unscoped CIFS or NFS client.
    /// </summary>
    public sealed class RawFileShareClient : BlobClientBase, IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Wrapped client.
        /// </summary>
        public BlobClientBase Inner { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">Wrapped client, which must implement <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/>.</param>
        public RawFileShareClient(BlobClientBase inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <inheritdoc />
        public override Task<bool> ValidateConnectivity(CancellationToken token = default) => Inner.ValidateConnectivity(token);

        /// <inheritdoc />
        public override Task<byte[]> GetAsync(string key, CancellationToken token = default) => Inner.GetAsync(key, token);

        /// <inheritdoc />
        public override Task<BlobData> GetStreamAsync(string key, CancellationToken token = default) => Inner.GetStreamAsync(key, token);

        /// <inheritdoc />
        public override Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default) => Inner.GetMetadataAsync(key, token);

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default) => Inner.WriteAsync(key, contentType, data, token);

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default) => Inner.WriteAsync(key, contentType, data, token);

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default) => Inner.WriteAsync(key, contentType, contentLength, stream, token);

        /// <inheritdoc />
        public override Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default) => Inner.WriteManyAsync(objects, token);

        /// <inheritdoc />
        public override Task DeleteAsync(string key, CancellationToken token = default) => Inner.DeleteAsync(key, token);

        /// <inheritdoc />
        public override Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default) => Inner.DeleteManyAsync(keys, token);

        /// <inheritdoc />
        public override Task<bool> ExistsAsync(string key, CancellationToken token = default) => Inner.ExistsAsync(key, token);

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default) => Inner.GenerateUrl(key, token);

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null) => Inner.Enumerate(filter);

        /// <inheritdoc />
        public override IAsyncEnumerable<BlobMetadata> EnumerateAsync(EnumerationFilter filter = null, CancellationToken token = default) => Inner.EnumerateAsync(filter, token);

        /// <inheritdoc />
        public override Task<EmptyResult> EmptyAsync(CancellationToken token = default) => Inner.EmptyAsync(token);

        /// <inheritdoc />
        public void Dispose() => ((IDisposable)Inner).Dispose();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ((IAsyncDisposable)Inner).DisposeAsync();
    }
}
