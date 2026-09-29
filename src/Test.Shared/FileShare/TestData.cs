namespace Test.Shared.FileShare
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// Test data helpers.
    /// </summary>
    public static class TestData
    {
        /// <summary>
        /// Deterministic pseudo-random bytes.
        /// </summary>
        /// <param name="length">Length.</param>
        /// <param name="seed">Seed.</param>
        /// <returns>Bytes.</returns>
        public static byte[] Pattern(int length, int seed)
        {
            byte[] data = new byte[length];
            new Random(seed).NextBytes(data);
            return data;
        }

        /// <summary>
        /// Lower-case hexadecimal SHA-256.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <returns>Hash.</returns>
        public static string Sha256(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Read a stream to its end.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Bytes.</returns>
        public static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken token)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms, 81920, token).ConfigureAwait(false);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Read exactly count bytes, throwing if the stream ends first.
        /// </summary>
        /// <param name="stream">Stream.</param>
        /// <param name="count">Count.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Bytes.</returns>
        public static async Task<byte[]> ReadExactlyAsync(Stream stream, int count, CancellationToken token)
        {
            byte[] buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int read = await stream.ReadAsync(buffer, offset, count - offset, token).ConfigureAwait(false);
                if (read < 1) throw new EndOfStreamException("Stream ended after " + offset + " of " + count + " bytes.");
                offset += read;
            }

            return buffer;
        }

        /// <summary>
        /// Keys of the objects (non-folder entries) in a listing.
        /// </summary>
        /// <param name="listing">Listing.</param>
        /// <returns>Keys.</returns>
        public static List<string> FileKeys(IEnumerable<BlobMetadata> listing)
        {
            List<string> ret = new List<string>();
            foreach (BlobMetadata md in listing) if (!md.IsFolder) ret.Add(md.Key);
            return ret;
        }

        /// <summary>
        /// Keys of the folder entries in a listing.
        /// </summary>
        /// <param name="listing">Listing.</param>
        /// <returns>Keys.</returns>
        public static List<string> FolderKeys(IEnumerable<BlobMetadata> listing)
        {
            List<string> ret = new List<string>();
            foreach (BlobMetadata md in listing) if (md.IsFolder) ret.Add(md.Key);
            return ret;
        }

        /// <summary>
        /// Every ancestor folder key of the supplied object keys, e.g. a/b/c.txt yields a/ and a/b/.
        /// </summary>
        /// <param name="keys">Object keys.</param>
        /// <param name="excludePrefixFolder">Folder key to exclude (the folder named by an enumeration prefix).</param>
        /// <returns>Folder keys.</returns>
        public static List<string> AncestorFolders(IEnumerable<string> keys, string excludePrefixFolder = null)
        {
            HashSet<string> ret = new HashSet<string>(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                int index = key.IndexOf('/');
                while (index >= 0 && index < key.Length - 1)
                {
                    ret.Add(key.Substring(0, index + 1));
                    index = key.IndexOf('/', index + 1);
                }
            }

            if (excludePrefixFolder != null) ret.Remove(excludePrefixFolder);
            return new List<string>(ret);
        }

        /// <summary>
        /// Enumerate into a list.
        /// </summary>
        /// <param name="client">Client.</param>
        /// <param name="filter">Filter.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Metadata.</returns>
        public static async Task<List<BlobMetadata>> EnumerateAsync(BlobClientBase client, EnumerationFilter filter, CancellationToken token)
        {
            List<BlobMetadata> ret = new List<BlobMetadata>();
            await foreach (BlobMetadata md in client.EnumerateAsync(filter, token).ConfigureAwait(false)) ret.Add(md);
            return ret;
        }
    }

    /// <summary>
    /// Read-only stream over a byte array that does not support seeking or report a length.
    /// </summary>
    public sealed class NonSeekableReadStream : Stream
    {
        private readonly byte[] _Data;
        private int _Position = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="data">Data.</param>
        public NonSeekableReadStream(byte[] data)
        {
            _Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc />
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            // return deliberately small, uneven reads
            int toRead = Math.Min(Math.Min(count, 7919), _Data.Length - _Position);
            if (toRead <= 0) return 0;
            Buffer.BlockCopy(_Data, _Position, buffer, offset, toRead);
            _Position += toRead;
            return toRead;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>
    /// Read-only stream that produces zero bytes slowly, honoring cancellation, used to cancel writes mid-transfer.
    /// </summary>
    public sealed class SlowReadStream : Stream
    {
        private readonly long _Length;
        private readonly int _ChunkSize;
        private readonly TimeSpan _Delay;
        private long _Position = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="length">Total length.</param>
        /// <param name="chunkSize">Maximum bytes per read.</param>
        /// <param name="delay">Delay per read.</param>
        public SlowReadStream(long length, int chunkSize, TimeSpan delay)
        {
            _Length = length;
            _ChunkSize = chunkSize;
            _Delay = delay;
        }

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => _Length;

        /// <inheritdoc />
        public override long Position
        {
            get => _Position;
            set => throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Delay(_Delay, cancellationToken).ConfigureAwait(false);
            int toRead = (int)Math.Min(Math.Min(count, _ChunkSize), _Length - _Position);
            if (toRead <= 0) return 0;
            Array.Clear(buffer, offset, toRead);
            _Position += toRead;
            return toRead;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(_Delay, cancellationToken).ConfigureAwait(false);
            int toRead = (int)Math.Min(Math.Min(buffer.Length, _ChunkSize), _Length - _Position);
            if (toRead <= 0) return 0;
            buffer.Span.Slice(0, toRead).Clear();
            _Position += toRead;
            return toRead;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
