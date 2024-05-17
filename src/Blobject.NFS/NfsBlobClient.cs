namespace Blobject.NFS
{
    /*
     * Helpful links
     * 
     * https://www.dummies.com/article/technology/computers/operating-systems/linux/how-to-share-files-with-nfs-on-linux-systems-255851/
     * https://github.com/SonnyX/NFS-Client
     * https://github.com/nekoni/nekodrive
     * https://code.google.com/archive/p/nekodrive/wikis/UseNFSDotNetLibrary.wiki
     * https://ubuntu.com/server/docs/network-file-system-nfs
     * https://www.hanewin.net/nfs-e.htm
     * https://serverfault.com/questions/240897/how-to-properly-set-permissions-for-nfs-folder-permission-denied-on-mounting-en
     * https://temasre.medium.com/connecting-to-nfs-client-v4-using-net-core-and-c-bc1f4af814c9
     * https://superuser.com/questions/1454750/how-to-get-nfs-server-on-windows-10
     * 
     */

    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Blobject.Core;
    using NFSLibrary;
    using NFSLibrary.Protocols.Commons;

    /// <inheritdoc />
    public class NfsBlobClient : IBlobClient, IDisposable
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        #region Public-Members

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger { get; set; } = null;

        /// <summary>
        /// Buffer size when working with streams.
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

        private string _Header = "[NfsBlobClient] ";
        private NfsSettings _NfsSettings = null;
        private int _StreamBufferSize = 4096;
        private bool _Disposed = false;

        private NFSClient _Client = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the <see cref="NfsBlobClient"/> class.
        /// </summary>
        /// <param name="nfsSettings">Settings for <see cref="NfsBlobClient"/>.</param>
        public NfsBlobClient(NfsSettings nfsSettings)
        {
            _NfsSettings = nfsSettings;
            _Client = InitializeClient();
            _Client.MountDevice(nfsSettings.Share);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            Log("disposing");

            if (!_Disposed)
            {
                if (_Client != null)
                {
                    if (_Client.IsMounted) _Client.UnMountDevice();
                    if (_Client.IsConnected) _Client.Disconnect();
                    _Client = null;
                }

                _NfsSettings = null;
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

        /// <summary>
        /// List shares available on the server.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>List of share names.</returns>
        public async Task<List<string>> ListShares(CancellationToken token = default)
        {
            return _Client.GetExportedDevices();
        }

        /// <inheritdoc />
        public async Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            byte[] ret = null;
            key = PathNormalizer(key);

            Stream stream = new MemoryStream();
            _Client.Read(key, ref stream);

            if (stream != null)
            {
                stream.Seek(0, SeekOrigin.Begin);
                ret = ((MemoryStream)stream).ToArray();
                stream.Close();
                stream.Dispose();
                stream = null;
            }

            return ret;
        }

        /// <inheritdoc />
        public async Task<BlobData> GetStreamAsync(string key, CancellationToken token = default)
        {
            BlobMetadata md = await GetMetadataAsync(key, token).ConfigureAwait(false);
            BlobData ret = null;
            key = PathNormalizer(key);

            Stream stream = new MemoryStream();
            _Client.Read(key, ref stream);

            if (stream != null)
            {
                stream.Seek(0, SeekOrigin.Begin);

                ret = new BlobData
                {
                    Data = stream,
                    ContentLength = md.ContentLength
                };
            }

            return ret;
        }

        /// <inheritdoc />
        public async Task<BlobMetadata> GetMetadataAsync(string key, CancellationToken token = default)
        {
            string normalizedKey = PathNormalizer(key);

            NFSAttributes attrib = null; 
            BlobMetadata md = null;

            attrib = _Client.GetItemAttributes(normalizedKey);
            if (attrib != null)
            {
                bool isFolder = _Client.IsDirectory(normalizedKey);
                long size = attrib.Size;

                md = new BlobMetadata
                {
                    Key = key,
                    ContentLength = size,
                    ContentType = "application/octet-stream",
                    IsFolder = isFolder,
                    CreatedUtc = attrib.CreateDateTime,
                    LastAccessUtc = attrib.LastAccessedDateTime,
                    LastUpdateUtc = attrib.ModifiedDateTime
                };
            }

            if (md == null) throw new KeyNotFoundException("The requested object was not found.");
            return md;
        }

        /// <inheritdoc />
        public Task WriteAsync(string key, string contentType, string data, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            key = PathNormalizer(key);

            return WriteAsync(key, contentType, Encoding.UTF8.GetBytes(data), token);
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, byte[] data, CancellationToken token = default)
        {
            key = PathNormalizer(key);

            using (MemoryStream stream = new MemoryStream())
            {
                await stream.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
                stream.Seek(0, SeekOrigin.Begin);
                _Client.Write(key, stream);
            }
        }

        /// <inheritdoc />
        public async Task WriteAsync(string key, string contentType, long contentLength, Stream stream, CancellationToken token = default)
        {
            key = PathNormalizer(key);

            _Client.Write(key, stream);
        }

        /// <inheritdoc />
        public async Task WriteManyAsync(List<WriteRequest> objects, CancellationToken token = default)
        {
            foreach (WriteRequest obj in objects)
            {
                string key = PathNormalizer(obj.Key);

                if (obj.Data != null)
                {
                    await WriteAsync(key, obj.ContentType, obj.Data, token).ConfigureAwait(false);
                }
                else
                {
                    await WriteAsync(key, obj.ContentType, obj.ContentLength, obj.DataStream, token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string key, CancellationToken token = default)
        {
            string normalizedKey = PathNormalizer(key);

            if (await ExistsAsync(key, token).ConfigureAwait(false))
            {
                BlobMetadata md = await GetMetadataAsync(key, token).ConfigureAwait(false);

                if (md != null)
                {
                    if (md.IsFolder)
                    {
                        _Client.DeleteDirectory(normalizedKey);
                    }
                    else
                    {
                        _Client.DeleteFile(normalizedKey);
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string key, CancellationToken token = default)
        {
            key = PathNormalizer(key);
            bool exists = false;

            try
            {
                BlobMetadata blob = await GetMetadataAsync(key, token).ConfigureAwait(false);
                if (blob != null) exists = true;
            }
            catch (KeyNotFoundException)
            {

            }
            
            return exists;
        }

        /// <inheritdoc />
        public string GenerateUrl(string key, CancellationToken token = default)
        {
            string url = "/" + _NfsSettings.Ip.ToString() + "/" + _NfsSettings.Share + "/" + key;
            url = url.Replace("\\", "/").Replace("//", "/");
            return url;
        }

        /// <inheritdoc />
        public IEnumerable<BlobMetadata> Enumerate(EnumerationFilter filter = null)
        {
            #region Set-Filter

            if (filter == null) filter = new EnumerationFilter();
            if (String.IsNullOrEmpty(filter.Prefix))
            {
                filter.Prefix = ".";
                Log("beginning enumeration");
            }
            else
            {
                Log("beginning enumeration using prefix " + filter.Prefix);
            }

            while (filter.Prefix.StartsWith("/")) filter.Prefix = filter.Prefix.Substring(1);

            filter.Prefix = filter.Prefix.Replace("\\", "/");

            string baseDirectory = "";
            string filePrefix = "";

            if (!filter.Prefix.Equals("."))
            {
                string[] parts = filter.Prefix.Split('/');

                if (filter.Prefix.EndsWith("/"))
                {
                    baseDirectory = filter.Prefix;
                }
                else
                {
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        baseDirectory += parts[i] + "/";
                    }

                    filePrefix = parts[parts.Length - 1];
                }
            }
            else
            {
                baseDirectory = ".";
            }

            #endregion

            #region Iterate

            IEnumerable<BlobMetadata> blobs = EnumerateSubdirectory(filter, baseDirectory, filePrefix);

            if (blobs != null)
            {
                foreach (BlobMetadata blob in blobs)
                {
                    if (blob.IsFolder)
                    {
                        EnumerationFilter childFilter = new EnumerationFilter
                        {
                            MinimumSize = filter.MinimumSize,
                            MaximumSize = filter.MaximumSize,
                            Prefix = blob.Key + "/",
                            Suffix = filter.Suffix
                        };

                        IEnumerable<BlobMetadata> childBlobs = Enumerate(childFilter);
                        if (childBlobs != null)
                        {
                            foreach (BlobMetadata childBlob in childBlobs)
                            {
                                yield return childBlob;
                            }
                        }

                        // return the directories last to support empty operations which need to first
                        // delete any documents contained in the subdirectory
                        yield return blob;

                    }
                    else
                    {
                        yield return blob;
                    }
                }
            }

            #endregion

            yield break;
        }

        /// <inheritdoc />
        public async Task<EmptyResult> EmptyAsync(CancellationToken token = default)
        {
            EmptyResult er = new EmptyResult();
             
            foreach (BlobMetadata md in Enumerate())
            {
                if (md.IsFolder)
                {
                    await EmptyDirectory(er, md.Key, 0);
                    _Client.DeleteDirectory(PathNormalizer(md.Key));
                }
                else
                {
                    Log("deleting file " + md.Key);
                    _Client.DeleteFile(PathNormalizer(md.Key));
                    er.Blobs.Add(md);
                }
            }

            return er;
        }

        #endregion

        #region Private-Methods

        private NFSClient InitializeClient()
        {
            NFSClient client = null;

            switch (_NfsSettings.Version)
            {
                case NfsVersionEnum.V2:
                    client = new NFSClient(NFSClient.NFSVersion.v2);
                    break;
                case NfsVersionEnum.V3:
                    client = new NFSClient(NFSClient.NFSVersion.v3);
                    break;
                case NfsVersionEnum.V4:
                    client = new NFSClient(NFSClient.NFSVersion.v4);
                    break;
                default:
                    throw new ArgumentException("Unknown NFS version '" + _NfsSettings.Version.ToString() + "'.");
            }

            client.Connect(_NfsSettings.Ip, _NfsSettings.UserId, _NfsSettings.GroupId, 5000);
            return client;
        }

        private void DisconnectClient(NFSClient client)
        {
            client.Disconnect();
        }

        private void MountShare(NFSClient client)
        {
            List<string> devices = client.GetExportedDevices();
            if (devices != null && devices.Count > 0 && devices.Contains(_NfsSettings.Share))
            {
                client.MountDevice(_NfsSettings.Share);
                return;
            }

            throw new ArgumentException("The specified share '" + _NfsSettings.Share + "' was not found.");
        }

        private void UnmountShare(NFSClient client)
        {
            client.UnMountDevice();
        }

        private void Log(string msg)
        {
            if (!String.IsNullOrEmpty(msg))
                Logger?.Invoke(_Header + msg);
        }

        private string PathNormalizer(string path)
        {
            if (String.IsNullOrEmpty(path)) return null;
            if (path.Contains("/")) path = path.Replace("/", "\\");
            if (!path.StartsWith(".\\")) path = ".\\" + path;
            while (path.EndsWith("/")) path = path.Substring(0, path.Length - 1);
            return path;
        }

        private IEnumerable<BlobMetadata> EnumerateSubdirectory(EnumerationFilter filter, string baseDirectory, string filePrefix)
        {
            string path = PathNormalizer(baseDirectory);

            string keyPrefix = "";
            if (baseDirectory != ".") keyPrefix = baseDirectory;

            foreach (string item in _Client.GetItemList(path))
            {
                if (!String.IsNullOrEmpty(filePrefix) && !item.ToLower().StartsWith(filePrefix.ToLower())) continue;
                if (!String.IsNullOrEmpty(filter.Suffix) && !item.ToLower().EndsWith(filter.Suffix)) continue;

                NFSAttributes attrib = _Client.GetItemAttributes(PathNormalizer(baseDirectory + "/" + item));
                if (attrib == null) continue;

                if (attrib.Size < filter.MinimumSize || attrib.Size > filter.MaximumSize) continue;

                BlobMetadata md = new BlobMetadata
                {
                    Key = keyPrefix + item,
                    IsFolder = _Client.IsDirectory(PathNormalizer(baseDirectory + "/" + item)),
                    ContentType = "application/octet-stream",
                    ContentLength = attrib.Size,
                    CreatedUtc = attrib.CreateDateTime,
                    LastAccessUtc = attrib.LastAccessedDateTime,
                    LastUpdateUtc = attrib.ModifiedDateTime
                };

                if (md.IsFolder) md.Key += "/";
                md.Key = md.Key.Replace("//", "/");

                yield return md;
            }
        }

        private async Task<EmptyResult> EmptyDirectory(EmptyResult er, string path, int spaceCount)
        {
            string spaces = "";
            for (int i = 0; i < spaceCount; i++) spaces += " ";

            if (er == null) er = new EmptyResult();
            List<string> folders = new List<string>();

            EnumerationFilter filter = new EnumerationFilter();
            filter.Prefix = path;

            foreach (BlobMetadata md in Enumerate(filter))
            {
                if (md.IsFolder)
                {
                    string folder = (md.Key + "/").Replace("//", "/");

                    Log("emptying directory " + folder);
                    await EmptyDirectory(er, folder, spaceCount + 2);

                    while (folder.EndsWith("/")) folder = folder.Substring(0, folder.Length - 1);
                    folder = ".\\" + folder.Replace("/", "\\");

                    Log("deleting directory " + folder);
                    _Client.DeleteDirectory(folder);
                }
                else
                {
                    Log("deleting file " + md.Key);
                    _Client.DeleteFile(md.Key);
                    er.Blobs.Add(md);
                }
            }

            return er;
        }

        #endregion

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
    }
}