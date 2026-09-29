namespace Blobject.Core
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Read-only, forward-only stream exposing at most a fixed number of bytes from an underlying stream.
    /// Used to honor the contentLength argument of stream writes.  The underlying stream is not disposed.
    /// </summary>
    public sealed class LengthLimitedReadStream : Stream
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool CanRead => true;

        /// <inheritdoc />
        public override bool CanSeek => false;

        /// <inheritdoc />
        public override bool CanWrite => false;

        /// <inheritdoc />
        public override long Length => throw new NotSupportedException();

        /// <summary>
        /// Number of bytes read so far.  Setting the position is not supported.
        /// </summary>
        public override long Position
        {
            get => _Consumed;
            set => throw new NotSupportedException();
        }

        #endregion

        #region Private-Members

        private readonly Stream _Inner;
        private readonly long _Limit;
        private long _Consumed = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="inner">Stream to read from.</param>
        /// <param name="limit">Maximum number of bytes to expose.</param>
        public LengthLimitedReadStream(Stream inner, long limit)
        {
            if (inner == null) throw new ArgumentNullException(nameof(inner));
            if (limit < 0) throw new ArgumentOutOfRangeException(nameof(limit));

            _Inner = inner;
            _Limit = limit;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            int allowed = Allowed(count);
            if (allowed < 1) return 0;

            int read = _Inner.Read(buffer, offset, allowed);
            _Consumed += read;
            return read;
        }

        /// <inheritdoc />
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int allowed = Allowed(count);
            if (allowed < 1) return 0;

            int read = await _Inner.ReadAsync(buffer, offset, allowed, cancellationToken).ConfigureAwait(false);
            _Consumed += read;
            return read;
        }

        /// <inheritdoc />
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int allowed = Allowed(buffer.Length);
            if (allowed < 1) return 0;

            int read = await _Inner.ReadAsync(buffer.Slice(0, allowed), cancellationToken).ConfigureAwait(false);
            _Consumed += read;
            return read;
        }

        /// <inheritdoc />
        public override void Flush()
        {
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        #endregion

        #region Private-Methods

        private int Allowed(int count)
        {
            long remaining = _Limit - _Consumed;
            if (remaining <= 0 || count <= 0) return 0;
            return (int)Math.Min(remaining, count);
        }

        #endregion
    }
}
