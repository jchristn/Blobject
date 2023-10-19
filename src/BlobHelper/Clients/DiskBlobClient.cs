using Azure.Storage.Blobs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlobHelper
{
    /// <inheritdoc />
    public class DiskBlobClient : IBlobClient, IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Buffer size to use when reading from a stream.
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
            if (!_Disposed)
            {
                _DiskSettings = null;
                _Disposed = true;
            }
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
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
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
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
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
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
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
        public Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
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
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
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
                byte[] buffer = new byte[_StreamBufferSize];

                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    while (bytesRemaining > 0)
                    {
                        if (bytesRemaining >= _StreamBufferSize)
                        {
                            read = await stream.ReadAsync(buffer, 0, _StreamBufferSize, token).ConfigureAwait(false);
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
        public async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
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
        public async Task DeleteAsync(string key, CancellationToken token = default)
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
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
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
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            string dir = _DiskSettings.Directory;
            dir = dir.Replace("\\", "/");
            dir = dir.Replace("//", "/");
            while (dir.EndsWith("/")) dir = dir.Substring(0, dir.Length - 1);
            return dir + "/" + key;
        }

        /// <inheritdoc />
        public async Task<EnumerationResult> EnumerateAsync(string prefix = null, string continuationToken = null, CancellationToken token = default)
        {
            int startIndex = 0;
            int count = 1000;

            if (!String.IsNullOrEmpty(continuationToken))
            {
                if (!DiskParseContinuationToken(continuationToken, out startIndex, out count))
                {
                    throw new ArgumentException("Unable to parse continuation token.");
                }
            }

            IEnumerable<string> files = null;

            if (!String.IsNullOrEmpty(prefix))
            {
                if (Directory.Exists(_DiskSettings.Directory + prefix))
                {
                    string tempPrefix = prefix;
                    tempPrefix = tempPrefix.Replace("\\", "/");
                    if (!tempPrefix.EndsWith("/")) tempPrefix += "/";
                    files = Directory.EnumerateFiles(_DiskSettings.Directory, tempPrefix + "*", SearchOption.AllDirectories);
                }
                else
                {
                    files = Directory.EnumerateFiles(_DiskSettings.Directory, prefix + "*", SearchOption.AllDirectories);
                }
            }
            else
            {
                files = Directory.EnumerateFiles(_DiskSettings.Directory, "*", SearchOption.AllDirectories);
            }

            long totalFiles = files.Count();
            files = files.Skip(startIndex).Take(count);

            EnumerationResult ret = new EnumerationResult();
            if (files.Count() < 1) return ret;

            ret.NextContinuationToken = DiskBuildContinuationToken(startIndex + count, count, totalFiles);

            foreach (string file in files)
            {
                FileInfo fi = new FileInfo(file);

                string filename = file;
                if (filename.StartsWith(_DiskSettings.Directory)) filename = file.Substring(_DiskSettings.Directory.Length);
                if (!String.IsNullOrEmpty(filename)) filename = filename.Replace("\\", "/");

                BlobMetadata md = new BlobMetadata();
                md.Key = filename;

                md.ContentLength = fi.Length;
                md.CreatedUtc = fi.CreationTimeUtc;
                ret.Blobs.Add(md);

                continue;
            }

            return ret;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();

            string continuationToken = null;

            while (true)
            {
                EnumerationResult result = await EnumerateAsync(null, null, token).ConfigureAwait(false);
                continuationToken = result.NextContinuationToken;

                if (result.Blobs != null && result.Blobs.Count > 0)
                {
                    foreach (BlobMetadata md in result.Blobs)
                    {
                        await DeleteAsync(md.Key, token).ConfigureAwait(false);
                        er.Blobs.Add(md);
                    }
                }
                else
                {
                    break;
                }
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private bool DiskParseContinuationToken(string continuationToken, out int start, out int count)
        {
            start = -1;
            count = -1;
            if (String.IsNullOrEmpty(continuationToken)) return false;
            byte[] encoded = Convert.FromBase64String(continuationToken);
            string encodedStr = Encoding.UTF8.GetString(encoded);
            string[] parts = encodedStr.Split(' ');
            if (parts.Length != 2) return false;

            if (!Int32.TryParse(parts[0], out start)) return false;
            if (!Int32.TryParse(parts[1], out count)) return false;
            return true;
        }

        private string DiskBuildContinuationToken(int start, int count, long totalFiles)
        {
            if ((start + count) > totalFiles) return null;
            string ret = start.ToString() + " " + count.ToString();
            byte[] retBytes = Encoding.UTF8.GetBytes(ret);
            return Convert.ToBase64String(retBytes);
        }

        #endregion
    }
}