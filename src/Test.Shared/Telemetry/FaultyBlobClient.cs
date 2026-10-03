namespace Test.Shared.Telemetry
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// In-memory BLOB client that does not override TelemetryProvider (so it reports as "custom") and fails on demand,
    /// used to exercise telemetry failure paths.
    /// </summary>
    public sealed class FaultyBlobClient : BlobClientBase
    {
        #region Public-Members

        /// <summary>
        /// Keys whose reads, writes, and deletes throw <see cref="IOException"/>.
        /// </summary>
        public HashSet<string> FailingKeys { get; } = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// When true, enumeration throws <see cref="IOException"/> after returning the first object.
        /// </summary>
        public bool FailEnumeration { get; set; } = false;

        /// <summary>
        /// Number of simulated transport failures GetAsync recovers from by resetting and retrying, as CIFS and NFS do.
        /// </summary>
        public int TransientFailures { get; set; } = 0;

        #endregion

        #region Private-Members

        private readonly ConcurrentDictionary<string, byte[]> _Objects = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationValidateConnectivity, null, () => Task.FromResult(true));
        }

        /// <inheritdoc />
        public override Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGet, key, async () =>
            {
                await Task.Yield();

                while (TransientFailures > 0)
                {
                    TransientFailures--;
                    RecordTelemetryConnectionReset(new System.Net.Sockets.SocketException());
                    RecordTelemetryRetry();
                }

                ThrowIfFailing(key);
                if (!_Objects.TryGetValue(key, out byte[] data)) throw new KeyNotFoundException("The requested object was not found.");
                SetTelemetryBytes(data.Length);
                return data;
            });
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            return await InstrumentAsync(BlobjectTelemetryNames.OperationGetStream, key, async () =>
            {
                await Task.Yield();
                ThrowIfFailing(key);
                if (!_Objects.TryGetValue(key, out byte[] data)) throw new KeyNotFoundException("The requested object was not found.");
                SetTelemetryBytes(data.Length);
                return new BlobData(data.Length, new MemoryStream(data));
            }).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationGetMetadata, key, () =>
            {
                ThrowIfFailing(key);
                if (!_Objects.TryGetValue(key, out byte[] data)) throw new KeyNotFoundException("The requested object was not found.");
                return Task.FromResult(new BlobMetadata { Key = key, ContentLength = data.Length });
            });
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data ?? ""), token);
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationWrite, key, async () =>
            {
                await Task.Delay(5, token).ConfigureAwait(false);
                ThrowIfFailing(key);
                _Objects[key] = data ?? Array.Empty<byte>();
                SetTelemetryBytes(data != null ? data.Length : 0);
            });
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            byte[] data = new byte[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                int n = await stream.ReadAsync(data, read, (int)contentLength - read, token).ConfigureAwait(false);
                if (n < 1) break;
                read += n;
            }

            await WriteAsync(key, contentType, data, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override Task DeleteAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationDelete, key, () =>
            {
                ThrowIfFailing(key);
                _Objects.TryRemove(key, out _);
                return Task.CompletedTask;
            });
        }

        /// <inheritdoc />
        public override Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            return InstrumentAsync(BlobjectTelemetryNames.OperationExists, key, () => Task.FromResult(_Objects.ContainsKey(key)));
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            return "memory://" + key;
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            return InstrumentEnumerate(() => EnumerateInternal());
        }

        /// <inheritdoc />
        public override IAsyncEnumerable<BlobMetadata> EnumerateAsync(EnumerationFilter filter = null, CancellationToken token = default)
        {
            return InstrumentEnumerateAsync(t => EnumerateInternalAsync(t), token);
        }

        #endregion

        #region Private-Methods

        private void ThrowIfFailing(string key)
        {
            if (key != null && FailingKeys.Contains(key)) throw new IOException("Simulated storage failure.");
        }

        private IEnumerable<BlobMetadata> EnumerateInternal()
        {
            int count = 0;

            foreach (KeyValuePair<string, byte[]> entry in _Objects.OrderBy(e => e.Key, StringComparer.Ordinal).ToList())
            {
                if (FailEnumeration && count > 0) throw new IOException("Simulated enumeration failure.");
                count++;
                yield return new BlobMetadata { Key = entry.Key, ContentLength = entry.Value.Length };
            }
        }

        private async IAsyncEnumerable<BlobMetadata> EnumerateInternalAsync([EnumeratorCancellation] CancellationToken token)
        {
            foreach (BlobMetadata md in EnumerateInternal())
            {
                await Task.Yield();
                token.ThrowIfCancellationRequested();
                yield return md;
            }
        }

        #endregion
    }
}
