namespace Blobject.Disk
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <inheritdoc />
    public class DiskBlobClient : BlobClientBase, IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        #endregion

        #region Private-Members

        private string _Header = "[DiskBlobClient] ";
        private bool _Disposed = false;
        private DiskSettings _DiskSettings = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="DiskBlobClient"/> class.
        /// </summary>
        /// <param name="diskSettings">Settings for <see cref="DiskBlobClient"/>.</param>
        public DiskBlobClient(DiskSettings diskSettings)
        {
            if (diskSettings == null) throw new ArgumentNullException(nameof(diskSettings));

            _DiskSettings = diskSettings;

            if (!Directory.Exists(diskSettings.Directory)) Directory.CreateDirectory(diskSettings.Directory);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Disposed.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            Log("disposing");

            if (!_Disposed)
            {
                _DiskSettings = null;
                _Disposed = true;
            }

            Log("disposed");
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        public override async Task<bool> ValidateConnectivity(CancellationToken token = default)
        {
            try
            {
                return Directory.Exists(_DiskSettings.Directory);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc />
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            string filename = GenerateUrl(key);
            if (Directory.Exists(filename))
            {
                return Array.Empty<byte>();
            }
            else if (File.Exists(filename))
            {
                using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, true))
                {
                    return await ReadStreamFullyAsync(fs, token).ConfigureAwait(false);
                }
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }

        /// <inheritdoc />
        public override async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            string filename = GenerateUrl(key);
            if (File.Exists(filename))
            {
                long contentLength = new FileInfo(filename).Length;
                FileStream stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferSize, true);
                return new BlobData(contentLength, stream);
            }
            else if (Directory.Exists(filename))
            {
                return new BlobData(0, new MemoryStream());
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }

        /// <inheritdoc />
        public override async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            string filename = GenerateUrl(key);

            if (File.Exists(filename))
            {
                FileInfo fi = new FileInfo(filename);
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                md.LastAccessUtc = fi.LastAccessTimeUtc;
                md.LastUpdateUtc = fi.LastWriteTimeUtc;
                return md;
            }
            else if (Directory.Exists(filename))
            {
                DirectoryInfo di = new DirectoryInfo(filename);
                BlobMetadata md = new BlobMetadata();
                md.Key = key;
                md.IsFolder = true;
                md.ContentLength = 0;
                md.CreatedUtc = di.CreationTimeUtc;
                md.LastAccessUtc = di.LastAccessTimeUtc;
                md.LastUpdateUtc = di.LastWriteTimeUtc;

                return md;
            }
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
        }

        /// <inheritdoc />
        public override Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (data == null) data = "";
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            long contentLength = 0;
            if (data == null) data = Array.Empty<byte>();

            using (MemoryStream stream = new MemoryStream(data))
            {
                contentLength = data.Length;
                stream.Seek(0, SeekOrigin.Begin);
                await WriteAsync(key, contentType, contentLength, stream, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (contentLength < 0) throw new ArgumentOutOfRangeException(nameof(contentLength));
            if (stream == null && contentLength > 0) throw new ArgumentNullException(nameof(stream));

            string filename = GenerateUrl(key);

            if (
                (key.EndsWith("\\") || key.EndsWith("/"))
                &&
                contentLength == 0
            )
            {
                Directory.CreateDirectory(filename);
            }
            else
            {
                string dirName = Path.GetDirectoryName(filename);
                if (!Directory.Exists(dirName))
                {
                    Directory.CreateDirectory(dirName);
                }

                using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None, StreamBufferSize, true))
                {
                    if (contentLength > 0) await CopyStreamAsync(stream, fs, contentLength, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public override async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            await base.WriteManyAsync(objects, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task DeleteAsync(string key, CancellationToken token = default)
        {
            string filename = GenerateUrl(key);
            if (File.Exists(filename))
            {
                File.Delete(filename);
            }
            else if (Directory.Exists(filename))
            {
                Directory.Delete(filename);
            }
        }

        /// <inheritdoc />
        public override async Task<DeleteManyResult> DeleteManyAsync(IEnumerable<string> keys, CancellationToken token = default)
        {
            return await base.DeleteManyAsync(keys, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            string filename = GenerateUrl(key);
            if (File.Exists(filename))
            {
                return true;
            }
            else if (Directory.Exists(filename))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <inheritdoc />
        public override string GenerateUrl(string key, CancellationToken token = default)
        {
            string dir = _DiskSettings.Directory;
            dir = dir.Replace("\\", "/");
            dir = dir.Replace("//", "/");
            while (dir.EndsWith("/")) dir = dir.Substring(0, dir.Length - 1);
            return dir + "/" + key;
        }

        /// <inheritdoc />
        public override IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            IEnumerable<string> files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);

                string filename = file;
                if (filename.StartsWith(_DiskSettings.Directory)) filename = file.Substring(_DiskSettings.Directory.Length);
                if (!String.IsNullOrEmpty(filename)) filename = filename.Replace("\\", "/");
                while (!String.IsNullOrEmpty(filename) && filename.StartsWith("/")) filename = filename.Substring(1);

                BlobMetadata md = new BlobMetadata();
                md.Key = filename;

                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                md.LastAccessUtc = fi.LastAccessTimeUtc;
                md.LastUpdateUtc = fi.LastWriteTimeUtc;

                if (!MatchesFilter(md, filter, StringComparison.OrdinalIgnoreCase)) continue;

                yield return md;
            }

            yield break;
        }

        /// <inheritdoc />
        public override async IAsyncEnumerable<BlobMetadata> EnumerateAsync(
            EnumerationFilter filter = null,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            filter = CloneFilter(filter);
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            IEnumerable<string> files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (token.IsCancellationRequested) break;

                FileInfo fi = new FileInfo(file);

                string filename = file;
                if (filename.StartsWith(_DiskSettings.Directory)) filename = file.Substring(_DiskSettings.Directory.Length);
                if (!String.IsNullOrEmpty(filename)) filename = filename.Replace("\\", "/");
                while (!String.IsNullOrEmpty(filename) && filename.StartsWith("/")) filename = filename.Substring(1);

                BlobMetadata md = new BlobMetadata();
                md.Key = filename;

                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                md.LastAccessUtc = fi.LastAccessTimeUtc;
                md.LastUpdateUtc = fi.LastWriteTimeUtc;

                if (!MatchesFilter(md, filter, StringComparison.OrdinalIgnoreCase)) continue;

                yield return md;
            }

            yield break;
        }

        /// <inheritdoc />
        public override async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult result = await base.EmptyAsync(token).ConfigureAwait(false);

            foreach (string directory in Directory.EnumerateDirectories(_DiskSettings.Directory, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            {
                if (token.IsCancellationRequested) break;
                if (Directory.Exists(directory)) Directory.Delete(directory);
            }

            return result;
        }

        #endregion

        #region Private-Methods

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}
