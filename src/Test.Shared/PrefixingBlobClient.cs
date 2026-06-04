namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// Prefix-scoped client wrapper for provider contract tests.
    /// </summary>
    public sealed class PrefixingBlobClient : BlobClientBase, IDisposable
    {
        private readonly BlobClientBase _Inner;
        private readonly string _Prefix;
        private bool _Disposed;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">Inner client.</param>
        /// <param name="prefix">Prefix.</param>
        public PrefixingBlobClient(BlobClientBase inner, string prefix)
        {
            _Inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _Prefix = NormalizePrefix(prefix);
            MaxConcurrency = inner.MaxConcurrency;
            StreamBufferSize = inner.StreamBufferSize;
            Logger = inner.Logger;
        }

        /// <summary>
        /// Prefix used by the wrapper.
        /// </summary>
        public string Prefix
        {
            get { return _Prefix; }
        }

        /// <inheritdoc />
        public override Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            return _Inner.ValidateConnectivity(token);
        }

        /// <inheritdoc />
        public override Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return _Inner.GetAsync(ApplyPrefix(key), token);
        }

        /// <inheritdoc />
        public override Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            return _Inner.GetStreamAsync(ApplyPrefix(key), token);
        }

        /// <inheritdoc />
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            BlobMetadata metadata = await _Inner.GetMetadataAsync(ApplyPrefix(key), token).ConfigureAwait(false);
            return StripPrefix(metadata);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            return _Inner.WriteAsync(ApplyPrefix(key), contentType, data, token);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            return _Inner.WriteAsync(ApplyPrefix(key), contentType, data, token);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            return _Inner.WriteAsync(ApplyPrefix(key), contentType, contentLength, stream, token);
        }

        /// <inheritdoc />
        public override Task DeleteAsync(string key, CancellationToken token = default)
        {
            return _Inner.DeleteAsync(ApplyPrefix(key), token);
        }

        /// <inheritdoc />
        public override Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return _Inner.ExistsAsync(ApplyPrefix(key), token);
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            return _Inner.GenerateUrl(ApplyPrefix(key), token);
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            EnumerationFilter innerFilter = BuildInnerFilter(filter);

            foreach (BlobMetadata metadata in _Inner.Enumerate(innerFilter))
            {
                BlobMetadata stripped = StripPrefix(metadata);
                if (stripped != null) yield return stripped;
            }
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            EnumerationFilter innerFilter = BuildInnerFilter(filter);

            await foreach (BlobMetadata metadata in _Inner.EnumerateAsync(innerFilter, token).ConfigureAwait(false))
            {
                BlobMetadata stripped = StripPrefix(metadata);
                if (stripped != null) yield return stripped;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;

            if (_Inner is IDisposable disposable) disposable.Dispose();
            _Disposed = true;
        }

        private string ApplyPrefix(string key)
        {
            if (String.IsNullOrEmpty(key)) return _Prefix;
            return _Prefix + key.Replace("\\", "/");
        }

        private EnumerationFilter BuildInnerFilter(EnumerationFilter filter)
        {
            filter = filter != null ? filter.Clone() : new EnumerationFilter();
            filter.Prefix = _Prefix + filter.Prefix;
            return filter;
        }

        private BlobMetadata StripPrefix(BlobMetadata metadata)
        {
            if (metadata == null || String.IsNullOrEmpty(metadata.Key)) return metadata;
            if (!metadata.Key.StartsWith(_Prefix, StringComparison.Ordinal)) return null;

            return new BlobMetadata
            {
                Key = metadata.Key.Substring(_Prefix.Length),
                IsFolder = metadata.IsFolder,
                ContentType = metadata.ContentType,
                ContentLength = metadata.ContentLength,
                ETag = metadata.ETag,
                CreatedUtc = metadata.CreatedUtc,
                LastUpdateUtc = metadata.LastUpdateUtc,
                LastAccessUtc = metadata.LastAccessUtc
            };
        }

        private static string NormalizePrefix(string prefix)
        {
            if (String.IsNullOrEmpty(prefix)) return "";
            prefix = prefix.Replace("\\", "/");
            while (prefix.StartsWith("/")) prefix = prefix.Substring(1);
            while (prefix.EndsWith("/")) prefix = prefix.Substring(0, prefix.Length - 1);
            return prefix + "/";
        }
    }
}
