namespace Blobject.Disk
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;

    /// <inheritdoc />
    public class DiskBlobClient : BlobClientBase, IDisposable
    {
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
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
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
                return File.ReadAllBytes(filename);
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
                FileStream stream = new FileStream(filename, FileMode.Open);
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
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            long contentLength = 0;
            MemoryStream stream = new MemoryStream(Array.Empty<byte>());

            if (data != null && data.Length > 0)
            {
                contentLength = data.Length;
                stream = new MemoryStream(data);
                stream.Seek(0, SeekOrigin.Begin);
            }

            await WriteAsync(key, contentType, contentLength, stream, token).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public override async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
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

                int read = 0;
                long bytesRemaining = contentLength;
                byte[] buffer = new byte[StreamBufferSize];

                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    while (bytesRemaining > 0)
                    {
                        if (bytesRemaining >= StreamBufferSize)
                        {
                            read = await stream.ReadAsync(buffer, 0, StreamBufferSize, token).ConfigureAwait(false);
                        }
                        else
                        {
                            read = await stream.ReadAsync(buffer, 0, (int)bytesRemaining, token).ConfigureAwait(false);
                        }

                        if (read > 0)
                        {
                            await fs.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                            bytesRemaining -= read;
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public override async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            foreach (WriteRequest obj in objects)
            {
                if (obj.Data != null)
                {
                    await WriteAsync(obj.Key, string.Empty, obj.Data, token).ConfigureAwait(false);
                }
                else
                {
                    await WriteAsync(obj.Key, string.Empty, obj.ContentLength, obj.DataStream, token).ConfigureAwait(false);
                }
            }
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
            else
            {
                throw new FileNotFoundException("Could not find file '" + key + "'.");
            }
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
            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix)) Log("beginning enumeration");
            else Log("beginning enumeration using prefix " + filter.Prefix);

            IEnumerable<string> files;

            if (!String.IsNullOrEmpty(filter.Prefix))
            {
                if (Directory.Exists(_DiskSettings.Directory + filter.Prefix))
                {
                    string tempPrefix = filter.Prefix;
                    tempPrefix = tempPrefix.Replace("\\", "/");
                    if (!tempPrefix.EndsWith("/")) tempPrefix += "/";
                    files = Directory.EnumerateFiles(_DiskSettings.Directory, tempPrefix + "*", SearchOption.AllDirectories);
                }
                else
                {
                    files = Directory.EnumerateFiles(_DiskSettings.Directory, filter.Prefix + "*", SearchOption.AllDirectories);
                }
            }
            else
            {
                files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.AllDirectories);
            }

            if (files.Count() < 1)
            {
                yield break;
            }

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);

                string filename = file;
                if (filename.StartsWith(_DiskSettings.Directory)) filename = file.Substring(_DiskSettings.Directory.Length);
                if (!String.IsNullOrEmpty(filename)) filename = filename.Replace("\\", "/");

                if (fi.Length < filter.MinimumSize || fi.Length > filter.MaximumSize) continue;
                if (!String.IsNullOrEmpty(filter.Suffix) && !filename.EndsWith(filter.Suffix)) continue;

                BlobMetadata md = new BlobMetadata();
                md.Key = filename;

                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                md.LastAccessUtc = fi.LastAccessTimeUtc;
                md.LastUpdateUtc = fi.LastWriteTimeUtc;

                yield return md;
            }

            yield break;
        }

        /// <inheritdoc />
        public override async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            foreach (BlobMetadata md in Enumerate())
            { 
                await DeleteAsync(md.Key, token).ConfigureAwait(false);
                er.Blobs.Add(md);
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        #endregion
    }
}