namespace Test.Shared
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <summary>
    /// Provider context for one isolated contract test.
    /// </summary>
    public sealed class BlobProviderContext : IDisposable
    {
        private bool _Disposed;

        /// <summary>
        /// Test client.
        /// </summary>
        public BlobClientBase Client { get; set; } = null;

        /// <summary>
        /// Provider options.
        /// </summary>
        public BlobProviderOptions Options { get; set; } = null;

        /// <summary>
        /// Optional cleanup delegate.
        /// </summary>
        public Func<CancellationToken, Task> CleanupAsync { get; set; } = null;

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;

            if (Client is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Client = null;
            Options = null;
            CleanupAsync = null;
            _Disposed = true;
        }
    }
}
